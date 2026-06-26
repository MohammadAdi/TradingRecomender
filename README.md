# Trading Recommender Bot

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![Quartz Scheduler](https://img.shields.io/badge/Quartz-3.8.1-5B5BD2?style=for-the-badge)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?style=for-the-badge&logo=postgresql)
![Telegram Bot](https://img.shields.io/badge/Telegram-%230088CC?style=for-the-badge&logo=telegram&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)

## Deskripsi Aplikasi

Trading Recommender Bot adalah *background worker* otomatis berbasis .NET 8 yang memantau pasar saham Indonesia (IHSG), menganalisis arus dana asing (*foreign flow*), dan mengirimkan rekomendasi trading serta rangkuman harian langsung ke grup Telegram.

Bot ini berjalan sebagai *scheduled service* -- dijadwalkan setiap hari kerja melalui Quartz Scheduler, mengambil data dari GOAPI, mengevaluasi sinyal berdasarkan ambang batas yang dapat dikonfigurasi, menyimpan hasilnya ke PostgreSQL, dan mengirimkan notifikasi via Telegram Bot API.

Desain arsitektur mengikuti prinsip *Clean Architecture* dengan lapisan Domain, Application, Infrastructure, dan Worker yang terpisah secara tegas, sehingga mudah diuji, dikembangkan, dan di-deploy.

## Fitur Utama

- **Pemantauan IHSG Otomatis** -- Men-scan pasar saham setiap hari kerja pada jam 09:00 WIB untuk mendeteksi anomali arus asing beli/jual.
- **Analisis Arus Asing (*Foreign Flow*)** -- Mengevaluasi volume dan nilai净 beli asing dengan ambang batas yang dapat dikonfigurasi (*thresholds*).
- **Mesin Rekomendasi** -- Mengonversi sinyal mentah menjadi rekomendasi trading (Buy/Sell/Strong Buy/Strong Sell) dengan alasan kontekstual.
- **Rangkuman Harian via Telegram** -- Setiap pukul 16:00 WIB, bot mengirim ringkasan rekomendasi hari itu ke channel/grup Telegram.
- **Pemantauan Portofolio** -- Opsional: scan hanya saham-saham tertentu dalam portofolio yang terdaftar (contoh: BBCA, TLKM, ASII).
- **Resiliensi HTTP** -- Retry otomatis dengan Polly (retry + circuit breaker) pada panggilan ke GOAPI dan Telegram.
- **Penyimpanan Persisten** -- Semua rekomendasi dan snapshot pasar disimpan di PostgreSQL untuk historisasi dan audit.
- **Konfigurasi Fleksibel** -- Semua pengaturan (threshold, jadwal, kredensial) via `appsettings.json` atau environment variables.
- **Docker Support** -- Multi-stage Dockerfile dan `docker-compose.yml` siap-deploy dengan PostgreSQL.

## Arsitektur

Projek ini mengadopsi pola **Clean Architecture** dengan empat lapisan:

```
+---------------------------------------------------+
|                 Worker (Host)                      |
|           TradingRecommender.Worker              |
|  +-----------------+  +------------------------+  |
|  |   Quartz Job    |  |   Quartz Trigger       |  |
|  |   Scheduler     |  |   (Cron-based)         |  |
|  +--------+--------+  +-----------+------------+  |
|           |                       |               |
|           v                       v               |
|  +--------+--------+  +-----------+------------+  |
|  | MonitorIHSGJob  |  | DailyDigestJob       |  |
|  | (cron 09:00)    |  | (cron 16:00 WIB)     |  |
|  +--------+--------+  +-----------+------------+  |
+-----------|-----------------------|--------------+
            |   DI / Service Calls
+-----------v-----------------------v--------------+
|             Application Layer                     |
|        TradingRecommender.Application            |
|  +----------+---------+-------------+            |
|  | MonitorIHSG   | Signal        | Recommend. |  |
|  | UseCase       | Service       | Engine     |  |
|  +------+--------+-------+-------+-----+------+  |
|         |                |              |         |
|  +------v----------------v--------------v------+  |
|  |    ForeignFlow     | Interfaces  | Messages  |  |
|  |    Analyzer        | (DI contracts)|           |  |
|  +---------------------+---------------+---------+  |
+--------------------------|-------------------------+
                           |   Implementation
+--------------------------v-------------------------+
|           Infrastructure Layer                      |
|       TradingRecommender.Infrastructure            |
|  +-------------+  +-------------+  +-------------+ |
|  |  EF Core    |  |  HTTP /     |  |  Telegram   | |
|  |  DbContext  |  |  GOAPI      |  |  Bot Client | |
|  | + PostgreSQL|  |  + Polly    |  |             | |
|  +------+------+        +------+----+           | |
|         |               |               |        |
|  +------v---------------v---------------v-------+|
|  |  Unit of Work / Repository Pattern          ||
|  +---------------------------------------------+|
+--------------------------------------------------+
                           |
+--------------------------v------------------------+
|              Domain Layer                          |
|         TradingRecommender.Domain                  |
|  +--- Entities ---+  +--- Configs ---+            |
|  | TradingRec.    |  | GoApi, Telegr.|            |
|  | MarketSnapshot |  | TradingSett.  |            |
|  +--- Enums -----+  +--- Interfaces--+            |
|  | SignalStrength |                                |
|  | MonitorType    |                                |
|  +--------------+                                 |
+---------------------------------------------------+
```

Alur data:
1. **Worker** menjalankan Quartz scheduler, memicu job sesuai cron schedule.
2. **Job** (di Worker layer) memanggil use-case atau service di **Application layer**.
3. **Application layer** (use-case/service) memanggil **Domain layer** (entities, enums, config) dan **Interfaces**.
4. **Infrastructure layer** menyediakan implementasi interface: EF Core (database), GOAPI client (HTTP), Telegram bot client.

Tekanan ketergantungan: Worker -> Application <-> Infrastructure -> Domain. Domain tidak bergantung pada apapun.

## Tech Stack

| Kategori          | Teknologi                                           |
|-------------------|-----------------------------------------------------|
| **Runtime**       | .NET 8 (Worker Service)                             |
| **Scheduler**     | Quartz.NET 3.8.1 (PostgreSQL persistent job-store)  |
| **ORM**           | Entity Framework Core 8.0.4 + Npgsql                |
| **Database**      | PostgreSQL 16                                       |
| **Resilience**    | Polly + Microsoft.Extensions.Http.Resilience        |
| **HTTP Client**   | Typed HttpClient (HttpClientFactory)                |
| **Notifications** | Telegram Bot API                                    |
| **Market Data**   | GOAPI (https://goapi.io)                           |
| **Container**     | Docker (multi-stage build, Alpine runtime)          |
| **Orchestration** | Docker Compose v3.9                                 |

## Struktur Proyek

```
TradingRecommenderBot/
|
|-- TradingRecommender.Domain/          # Lapisan inti: entitas, enum, konfigurasi
|   |-- Entities/
|   |   |-- TradingRecommendation.cs    # Rekaman sinyal rekomendasi
|   |   |-- MarketSnapshot.cs           # Snapshot kondisi pasar
|   |   `-- BaseEntity.cs               # Base class (Id, CreatedAt, UpdatedAt)
|   |-- Configurations/
|   |   |-- DatabaseConfig.cs           # Konektor database
|   |   |-- GoApiConfig.cs              # Konfigurasi GOAPI
|   |   |-- TelegramConfig.cs           # Konfigurasi Telegram Bot
|   |   |-- ScheduleConfig.cs           # Ekspresi cron job
|   |   |-- TradingSettings.cs          # Semua settings trading (validated)
|   |   |-- ForeignFlowThresholds.cs    # Ambang batas arus asing
|   |   `-- PortfolioConfig.cs          # Daftar saham portofolio
|   |-- Enums/
|   |   |-- SignalStrength.cs           # StrongBuy, Buy, Neutral, Sell, StrongSell
|   |   `-- MonitorType.cs              # ForeignFlow, Volume, EmitenPerformance
|   `-- TradingRecommender.Domain.csproj
|
|-- TradingRecommender.Application/     # Business logic & use cases
|   |-- Interfaces/                     # Kontrak layanan (DIP)
|   |   |-- ISignalService.cs
|   |   |-- ISignalEvaluator.cs
|   |   |-- IRecommendationEngine.cs
|   |   |-- IGoApiClient.cs
|   |   |-- ITelegramBotClient.cs
|   |   `-- Persistence/IUnitOfWork.cs
|   |-- UseCases/
|   |   |-- Monitoring/MonitorIHSGUseCase.cs    # Orkestrasi pemantauan IHSG
|   |   |-- Signals/SignalService.cs            # Parsing & evaluasi sinyal pasar
|   |   |-- Recommendations/RecommendationEngine.cs  # Konversi sinyal ke rekomendasi
|   |   |-- ForeignFlow/ForeignFlowAnalyzer.cs  # Evaluasi ambang batas arus asing
|   |   `-- Messages/MarketMonitoringResult.cs  # DTO hasil monitoring
|   |-- DependencyInjection.cs
|   `-- TradingRecommender.Application.csproj
|
|-- TradingRecommender.Infrastructure/  # External concerns
|   |-- Persistence/
|   |   |-- AppDbContext.cs               # EF Core DbContext (PostgreSQL)
|   |   |-- Repository.cs                 # Generic repository pattern
|   |   `-- UnitOfWork.cs                 # Unit of work aggregator
|   |-- Http/
|   |   |-- GoApiClient.cs                # Klien HTTP ke GOAPI
|   |   |-- ResiliencePolicies.cs         # Retry + Circuit Breaker (Polly)
|   |   `-- RedactingLoggingHandler.cs    # Scrub sensitive headers dari log
|   |-- Notifications/
|   |   `-- TelegramBotClient.cs          # Klien Telegram Bot API
|   |-- DependencyInjection.cs
|   `-- TradingRecommender.Infrastructure.csproj
|
|-- TradingRecommender.Worker/          # Entry point / host
|   |-- Program.cs                      # Composition root
|   |-- appsettings.json                # Konfigurasi default
|   |-- appsettings.Development.json    # Konfigurasi development
|   |-- Quartz/
|   |   `-- QuartzSchedulerExtensions.cs # Setup scheduler + trigger
|   |-- Jobs/
|   |   |-- MonitorIHSGJob.cs           # Job scan pasar (09:00)
|   |   `-- DailyDigestJob.cs           # Job kirim digest (16:00)
|   |-- TradingRecommender.Worker.csproj
|   `-- wwwroot/                        # (kosong / placeholder)
|
|-- Dockerfile                          # Multi-stage Docker build
|-- docker-compose.yml                  # Orkestrasi + PostgreSQL
|-- .env.example                        # Template environment variables
|-- TradingRecommender.sln              # Solution file (4 project)
`-- docs/
    |-- CODE_REVIEW.md                  # Catatan review kode
    `-- LIFETIME_REVIEW.md              # Catatan lifecycle DI
```

## Cara Instalasi dan Menjalankan

### Persyaratan Sistem

- **.NET 8 SDK** (8.0.100 atau lebih baru)
- **PostgreSQL** 16 (jika menjalankan secara lokal tanpa Docker)
- **Docker & Docker Compose** (alternatif, direkomendasikan untuk produksi)
- Akun **GOAPI** (untuk data pasar saham)
- **Telegram Bot Token** (buat via @BotFather)
- **Telegram Chat ID** (grup/channel tujuan notifikasi)

### Konfigurasi

Salin file lingkungan dan isi dengan nilai Anda:

```bash
cp .env.example .env
```

Edit `.env` dengan editor teks. Nilai ini dibaca oleh Docker Compose dan di-*prefix*-kan `TRADINGBOT_` agar masuk ke konfigurasi .NET.

### Menjalankan Secara Lokal (tanpa Docker)

```powershell
# 1. Restore dan build
dotnet restore TradingRecommender.sln
dotnet build  TradingRecommender.sln

# 2. Pastikan PostgreSQL berjalan di localhost:5432
# 3. Edit appsettings.Development.json / appsettings.json
#    - Isi Database:ConnectionString (host, port, nama DB, user, password)
#    - Isi TradingSettings:GoApi:ApiKey
#    - Isi TradingSettings:Telegram:BotToken & ChatId

# 4. Jalankan migration database (jika perlu)
dotnet ef migrations add InitialCreate `
  --project TradingRecommender.Infrastructure `
  --startup-project TradingRecommender.Worker

dotnet ef database update `
  --project TradingRecommender.Infrastructure `
  --startup-project TradingRecommender.Worker

# 5. Jalankan worker
dotnet run --project TradingRecommender.Worker
```

Log keluaran menampilkan aktivitas scheduler, job trigger, dan koneksi database di konsol.

### Menggunakan Docker

```powershell
# 1. Build image
docker build -t trading-recommender-bot .

# 2. Jalankan (asumsi PostgreSQL sudah berjalan di localhost:5432)
docker run -d `
  --name tradingbot `
  -e TRADINGBOT__Database__ConnectionString="Host=localhost;Port=5432;Database=trading_recommender;Username=postgres;Password=yourpassword" `
  -e TRADINGBOT__TradingSettings__GoApi__ApiKey="your-goapi-key" `
  -e TRADINGBOT__TradingSettings__Telegram__BotToken="your-bot-token" `
  -e TRADINGBOT__TradingSettings__Telegram__ChatId="your-chat-id" `
  -e TZ=Asia/Jakarta `
  trading-recommender-bot
```

### Menggunakan Docker Compose (dengan PostgreSQL)

```powershell
# 1. Salin dan isi file .env
cp .env.example .env
# Edit .env -- isi POSTGRES_PASSWORD, GOAPI_API_KEY, TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID

# 2. Bangun dan jalankan semua layanan
docker compose up -d --build

# 3. Periksa status container
docker compose ps

# 4. Lihat log
docker compose logs -f tradingbot

# 5. Matikan semua layanan
docker compose down

# 6. Hapus volume PostgreSQL (hati-hati: ini menghapus seluruh data)
docker compose down -v
```

Dengan `docker-compose.yml`, database PostgreSQL dan bot berjalan dalam satu jaringan Docker. Bot secara otomatis menunggu PostgreSQL sehat sebelum memulai (`depends_on.postgres.condition: service_healthy`).

## Konfigurasi

Semua konfigurasi utama berasal dari `appsettings.json`. Di production, nilainya di-*override* lewat environment variable dengan awalan `TRADINGBOT__`.

### Kunci Environment Variable

| Environment Variable | Section | Default | Keterangan |
|---|---|---|---|
| `POSTGRES_USER` | -- | `tradingbot` | Nama user database (docker-compose) |
| `POSTGRES_PASSWORD` | -- | (wajib diisi) | Password database (docker-compose) |
| `TRADINGBOT__Database__ConnectionString` | `Database:ConnectionString` | `Host=localhost;Port=5432;Database=trading_recommender_dev;Username=postgres;Password=` | Koneksi ke PostgreSQL |
| `TRADINGBOT__TradingSettings__GoApi__BaseUrl` | `TradingSettings:GoApi:BaseUrl` | `https://api.goapi.io/v1/stock` | Endpoint GOAPI |
| `TRADINGBOT__TradingSettings__GoApi__ApiKey` | `TradingSettings:GoApi:ApiKey` | `""` (wajib diisi) | API key GOAPI |
| `TRADINGBOT__TradingSettings__GoApi__TimeoutSeconds` | `TradingSettings:GoApi:TimeoutSeconds` | `15` | Timeout HTTP ke GOAPI |
| `TRADINGBOT__TradingSettings__GoApi__RetryCount` | `TradingSettings:GoApi:RetryCount` | `5` | Jumlah retry gagal |
| `TRADINGBOT__TradingSettings__Telegram__BotToken` | `TradingSettings:Telegram:BotToken` | `""` (wajib diisi) | Token dari @BotFather |
| `TRADINGBOT__TradingSettings__Telegram__ChatId` | `TradingSettings:Telegram:ChatId` | `""` (wajib diisi) | Chat/channel ID tujuan |
| `TRADINGBOT__TradingSettings__Telegram__ParseMode` | `TradingSettings:Telegram:ParseMode` | `Markdown` | Markdown, MarkdownV2, atau HTML |
| `TRADINGBOT__TradingSettings__Schedule__MarketScanCron` | `TradingSettings:Schedule:MarketScanCron` | `0 9 * * MON-FRI` | Cron scan pasar (09:00) |
| `TRADINGBOT__TradingSettings__Schedule__DigestCron` | `TradingSettings:Schedule:DigestCron` | `0 16 * * MON-FRI` | Cron kirim digest (16:00) |
| `TRADINGBOT__TradingSettings__Schedule__TimeZoneId` | `TradingSettings:Schedule:TimeZoneId` | `Asia/Jakarta` | Zona waktu scheduler |
| `TRADINGBOT__TradingSettings__ForeignFlow__BuyThreshold` | `TradingSettings:ForeignFlow:BuyThreshold` | `2.0` | Minimal beli asing (miliar IDR) |
| `TRADINGBOT__TradingSettings__ForeignFlow__SellThreshold` | `TradingSettings:ForeignFlow:SellThreshold` | `2.0` | Minimal jual asing (miliar IDR) |
| `TRADINGBOT__TradingSettings__ForeignFlow__VolumeFloor` | `TradingSettings:ForeignFlow:VolumeFloor` | `5_000_000_000` | Volume minimum sah (lembar) |
| `TRADINGBOT__TradingSettings__ForeignFlow__VolumeCeiling` | `TradingSettings:ForeignFlow:VolumeCeiling` | `15_000_000_000` | Volume maksimum waspada |
| `TRADINGBOT__TradingSettings__Notification__QuietHoursStart` | `TradingSettings:Notification:QuietHoursStart` | `17:00` | Mulai jam tenang |
| `TRADINGBOT__TradingSettings__Notification__QuietHoursEnd` | `TradingSettings:Notification:QuietHoursEnd` | `08:00` | Selesai jam tenang |
| `TRADINGBOT__TradingSettings__Notification__MaxItemsPerDigest` | `TradingSettings:Notification:MaxItemsPerDigest` | `20` | Maksimal item per digest |
| `TRADINGBOT__TradingSettings__Notification__OnlyActionable` | `TradingSettings:Notification:OnlyActionable` | `true` | Hanya kirim sinyal actionable |
| `TZ` | -- | `Asia/Jakarta` | Container timezone |

### Contoh Override via Environment Variable (docker-compose)

```yaml
environment:
  TRADINGBOT__Database__ConnectionString: "Host=postgres;Port=5432;Database=trading_recommender;Username=postgres;Password=${POSTGRES_PASSWORD}"
  TRADINGBOT__TradingSettings__GoApi__ApiKey: "${GOAPI_API_KEY}"
  TRADINGBOT__TradingSettings__Telegram__BotToken: "${TELEGRAM_BOT_TOKEN}"
  TRADINGBOT__TradingSettings__Telegram__ChatId: "${TELEGRAM_CHAT_ID}"
  TRADINGBOT__TradingSettings__ForeignFlow__BuyThreshold: "2.0"
```

## Jobs & Penjadwalan

### 1. MonitorIHSGJob -- Scan Pasar IHSG

- **Schedule**: `0 9 * * MON-FRI` (setiap Senin-Jumat, pukul 09:00 WIB)
- **Deskripsi**: Memindai seluruh pasar saham yang terdaftar di GOAPI, mengevaluasi arus dana asing (beli/jual neto), memeriksa volume transaksi, dan menghasilkan sinyal rekomendasi.
- **Alur kerja**:
  1. Fetch data foreign buys dari GOAPI (`GET /foreign-buys?market=IDX`)
  2. Evaluasi setiap saham terhadap *threshold* yang dikonfigurasi (beli/jual neto >= 2 miliar IDR, volume >= 5 miliar lembar)
  3. Hasil evaluasi diteruskan ke `RecommendationEngine` untuk membuat entitas `TradingRecommendation`
  4. Semua rekomendasi disimpan ke database dalam satu *batch* (Unit of Work)
- **Fitur proteksi**: `[DisallowConcurrentExecution]` mencegah scan ganda yang tumpang-tindih.

### 2. DailyDigestJob -- Kirim Ringkasan Harian

- **Schedule**: `0 16 * * MON-FRI` (setiap Senin-Jumat, pukul 16:00 WIB)
- **Deskripsi**: Mengambil seluruh rekomendasi yang dibuat hari itu dari database, merangkumnya, dan mengirimkan via Telegram Bot API.
- **Alur kerja**:
  1. Query `Recommendations` tabel untuk entitas hari ini, urutkan descending, ambil maksimal 20
  2. Format ringkasan dengan simbol saham, tipe sinyal (Buy/Sell), dan alasan
  3. Kirim pesan ke Telegram (dengan *rate limiting* 1.2 detik antar pesan)
  4. Jika tidak ada rekomendasi hari itu, kirim pesan "tidak ada rekomendasi"
- **Fitur proteksi**: `[DisallowConcurrentExecution]`, *message sanitization* (truncasi 4096 karakter), *token-bucket rate limiter*.

## Database Schema

Database PostgreSQL menyimpan dua entitas utama yang dikelola oleh EF Core.

### Tabel `recommendations`

Direkam sebagai `TradingRecommendation` (entity).

| Kolom              | Tipe           | Keterangan                                      |
|--------------------|----------------|-------------------------------------------------|
| `id`               | `uuid` (PK)    | GUID unik, di-generate otomatis                  |
| `tickersymbol`     | `nvarchar(16)` | Kode saham, contoh: "BBCA", "TLKM"              |
| `strength`         | `smallint`     | Enum: StrongBuy=0, Buy=1, Neutral=2, Sell=3, StrongSell=4 |
| `currentprice`     | `numeric?`     | Harga saat ini (opsional)                        |
| `rationale`        | `nvarchar(1024)` | Alasan rekomendasi (maks 1024 karakter)         |
| `foreignflownet`   | `numeric`      | Nilai neto arus asing (IDR)                      |
| `marketvolume`     | `numeric`      | Volume pasar (lembar)                             |
| `context`          | `jsonb`        | Metadata tambahan (dictionary key-value)         |
| `createdat`        | `datetime2`    | Waktu pembuatan rekomendasi                      |
| `updatedat`        | `datetime2?`   | Waktu pembaruan terakhir                         |

**Index**: `idx_createdat` pada `CreatedAt`, `idx_tickersymbol` pada `TickerSymbol`.

### Tabel `market_snapshots`

Direkam sebagai `MarketSnapshot` (entity).

| Kolom              | Tipe           | Keterangan                                      |
|--------------------|----------------|-------------------------------------------------|
| `id`               | `uuid` (PK)    | GUID unik                                        |
| `timestamp`        | `datetime2`    | Waktu snapshot                                   |
| `monitortype`      | `smallint`     | Enum: ForeignFlow=0, Volume=1, EmitenPerformance=2 |
| `ihsgvalue`        | `numeric`      | Nilai indeks IHSG                                |
| `foreignflownet`   | `numeric`      | Arus asing neto                                  |
| `volume`           | `bigint`       | Volume transaksi                                 |
| `marketcaption`    | `numeric`      | Kapitalisasi pasar                               |
| `metadata`         | `jsonb`        | Metadata tambahan                                |
| `createdat`        | `datetime2`    | Waktu pembuatan                                  |
| `updatedat`        | `datetime2?`   | Waktu pembaruan terakhir                         |

**Index**: `idx_timestamp` pada `Timestamp`.

> **Catatan**: Jadwal persistensi Quartz juga menyimpan tabelnya sendiri di database yang sama dengan prefix `qrtz_` (didokumentasikan oleh Quartz.NET, tidak dikelola manual).

## API & External Services

### GOAPI (Market Data Provider)

- **Endpoint dasar**: `https://api.goapi.io/v1/stock`
- **Method**: `IGoApiClient` (typed HttpClient)
- **Endpoint**:
  - `GET /foreign-buys?market=IDX` -- daftar saham dengan transaksi asing beli
  - `GET /volume?market=IDX` -- ringkasan volume pasar
  - `GET /price/{symbol}` -- harga terkini per saham
- **Autentikasi**: Header `Authorization: Bearer {ApiKey}`
- **Resiliensi**: Retry (hingga 5x) + Circuit Breaker via Polly + `RedactingLoggingHandler` untuk mencegah kebocoran kunci API di log.

### Telegram Bot API

- **Endpoint**: `https://api.telegram.org/bot{token}/sendMessage`
- **Autentikasi**: Bot token dari `@BotFather`
- **Kirim pesan**: Payload JSON `{chat_id, text, parse_mode}`
- **Rate Limiting**: Semaphore-based token bucket -- minimum 1.2 detik antar pengiriman pesan
- **Safety**: Pesan dipotong maksimal 4096 karakter (batasan Telegram); header sensitif diskrab dari log.

### PostgreSQL

- **Driver**: Npgsql EF Core provider (`Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.2)
- **Persistent Job Store**: Quartz.NET menggunakan tabel yang sama dengan prefix `qrtz_` untuk menyimpan job dan trigger. Jadwal tetap ada walau worker restart.
- **Seeding Migration**: `HasConversion<int>()` untuk enum dan `HasColumnType("jsonb")` untuk dictionary.

## Pipeline Sinyal (Ringkasa Alur)

```
Quartz Cron Trigger (09:00 WIB)
    |
    v
MonitorIHSGJob [Transient]
    |
    +--> ISignalService.ScanPortfolioAsync()
    |        |
    |        +--> IGoApiClient.GetForeignBuysAsync()   [HTTP -> GOAPI]
    |        +--> SignalService.EvaluateRecord()       [Pure function]
    |        +--> returns IReadOnlyList<SignalResult>
    |
    +--> IRecommendationEngine.GenerateRecommendationAsync()
    |        |
    |        +--> maps SignalResult -> TradingRecommendation entity
    |        +--> returns TradingRecommendation?
    |
    +--> IUnitOfWork.Recommendations.AddAsync()
    |
    +--> IUnitOfWork.SaveChangesAsync()  [EF Core -> PostgreSQL]

Quartz Cron Trigger (16:00 WIB)
    |
    v
DailyDigestJob [Transient]
    |
    +--> AppDbContext.Recommendations.Where(...)  [read-only, AsNoTracking]
    |
    +--> ITelegramBotClient.SendAsync()
    |        |
    |        +--> RateLimitAsync()  [semaphore, 1.2s gap]
    |        +--> PostAsJsonAsync   [HTTP -> Telegram Bot API]
```

## License

MIT License. See the LICENSE file for details.
