# Code Review — Trading Recommender Bot

**Tanggal:** 2026-06-25  
**Reviewer:** Claude Sonnet 4.6  
**Scope:** Full repo (Domain, Application, Infrastructure, Worker, Docker)

Severity: 🔴 Critical · 🟠 High · 🟡 Medium · 🟢 Low / informational

---

## 🔴 CRITICAL

### 1. Bot token tersimpan polos di field `Authorization` header + `LogDebug` bisa bocor

**File:** [GoApiClient.cs:33](TradingRecommender.Infrastructure/Http/GoApiClient.cs#L33)

```csharp
_http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
```

- `HttpClient.DefaultRequestHeaders` untuk `Authorization` benar secara sintaks, **tetapi**:
  - Header ini diwarisi ke semua request — tidak masalah karena semua request ke GOAPI memang butuh auth.
  - **Bahaya**: jika salah satu path dibangun tanpa `/` prefix yang benar, header bisa ikut terkirim ke `Referer` atau di-log oleh middleware debugging.
- Lebih penting: banyak paket `.AddHttpClient` builder **menyimpan** header untuk logging ketika ada `DelegatingHandler` yang menulis ke log. Saat ini kita tidak menuliskan header ke log, tapi risikonya tetap ada.

**Fix:** pastikan `Authorization` header hanya dipasang lewat builder HttpClient (`ConfigureHttpClient`), dan **scrub** header dari setiap request log. Saya tambahkan `RedactingLoggingHandler` di bawah.

### 2. `ApiKey` / `BotToken` bisa ter-ekspos lewat exception message

**File:** [TelegramBotClient.cs:56](TradingRecommender.Infrastructure/Notifications/TelegramBotClient.cs#L56)

```csharp
var body = await response.Content.ReadAsStringAsync();
_logger.LogError("Telegram send failed: {Status} {Body}", response.StatusCode, body);
```

Telegram mengembalikan body error yang **menyertakan kembali** sebagian token (mis. `{"ok":false,"error_code":401,"description":"Unauthorized"}` — tapi ada juga endpoint lain yang echoes path). Aman untuk `sendMessage`, **tetapi** pola "log response body" adalah anti-pattern secara umum.

**Fix:** log status code saja, log body hanya di `Debug` level, dan tambahkan `OnFailure` delegating handler yang scrub `Authorization`.

### 3. `appsettings.json` menyimpan connection string plaintext dengan password

**File:** [appsettings.json:11](TradingRecommender.Worker/appsettings.json#L11)

```json
"ConnectionString": "Host=localhost;Port=5432;Database=trading_recommender;Username=postgres;Password=postgres"
```

Meskipun ini placeholder, polanya berbahaya — `.dockerignore` sudah mengecualikan `appsettings.*.Production.json`, tapi appsettings.json default akan ter-commit ke git.

**Fix:** kosongkan password di `appsettings.json`, override via env var di dev/prod.

---

## 🟠 HIGH

### 4. `MonitorIHSGUseCase` belum menggunakan `ISignalService` — logika duplikat

**File:** [MonitorIHSGUseCase.cs](TradingRecommender.Application/UseCases/Monitoring/MonitorIHSGUseCase.cs)

Use case ini masih panggil `_goApi` + `_signalEvaluator` + `_recommendationEngine` manual, padahal `ISignalService.ScanMarketAsync()` melakukan hal yang sama.

**Fix:** refactor `MonitorIHSGUseCase` untuk delegate ke `ISignalService`.

### 5. N+1 pattern di `MonitorIHSGUseCase` — tiap record di-evaluate serial

**File:** [MonitorIHSGUseCase.cs:43-63](TradingRecommender.Application/UseCases/Monitoring/MonitorIHSGUseCase.cs#L43-L63)

Foreach loop synchronous untuk tiap record. Untuk IHSG yang punya ~700 ticker, ini blocking.

**Fix:** gunakan `Parallel.ForEachAsync` dengan `MaxDegreeOfParallelism` dibatasi.

### 6. SaveChanges per-loop di `RecommendationEngine` + UoW SaveChanges di akhir

Pola saat ini OK (batch di akhir via `_unitOfWork.SaveChangesAsync()`), **tapi** belum optimal karena `_recommendationEngine.GenerateRecommendationAsync` mengembalikan entity satu per satu — kalau ada 50 sinyal, EF akan track 50 entity dan insert 50 row dalam satu `INSERT batch`. Ini sudah cukup baik; tidak perlu diubah kecuali dataset besar.

**Fix:** None untuk saat ini, sudah batched.

### 7. `Repository<T>` di-`new` di `UnitOfWork` constructor — bocor ke caller

**File:** [UnitOfWork.cs:17-18](TradingRecommender.Infrastructure/Persistence/UnitOfWork.cs#L17-L18)

```csharp
Recommendations = new Repository<TradingRecommendation>(_db);
Snapshots = new Repository<MarketSnapshot>(_db);
```

`IRepository<T>` juga generic — bisa di-register via DI:

```csharp
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

**Fix:** register `IRepository<>` secara generic, hapus `new Repository<>(_db)` di UoW.

### 8. `LogDebug` di path request dapat bocor data sensitif saat DEBUG

**File:** [GoApiClient.cs:40-41](TradingRecommender.Infrastructure/Http/GoApiClient.cs#L40-L41)

`_logger.LogDebug("Fetching price for {Symbol}", symbol);` — symbol aman. Tapi `_logger.LogDebug("Fetching foreign-buy records for {Market}", market);` di loop akan spam log jika records banyak.

**Fix:** kurangi verbosity untuk inner loops.

### 9. `OnRetry` di Polly cetak `outcome.Exception` — bisa stack trace besar

**File:** [ResiliencePolicies.cs:27-30](TradingRecommender.Infrastructure/Http/ResiliencePolicies.cs#L27-L30)

```csharp
logger.LogWarning(outcome.Exception, "GOAPI retry ...");
```

`HttpRequestException` bisa berisi header response yang **mencantumkan** `Authorization`. Stack trace tidak, tapi `Exception.ToString()` di beberapa tipe inner exception bisa.

**Fix:** log exception type saja, bukan exception object. Saya tambahkan message-only logging.

### 10. `TelegramBotClient` tidak validasi panjang `message`

Telegram Bot API punya limit **4096 karakter** per pesan. `DailyDigestJob` menulis `recs.Take(20)` — 20 rekomendasi dengan rationale bisa >4096 char.

**Fix:** truncate atau split messages. Saya tambahkan helper.

---

## 🟡 MEDIUM

### 11. `decimal` vs `double` di `EvaluateRecord`

**File:** [SignalService.cs:132](TradingRecommender.Application/UseCases/Signals/SignalService.cs#L132)

```csharp
strength = netBuy >= f.BuyThreshold * 1_000_000_000m
```

`netBuy` adalah `double`, `BuyThreshold * 1B` adalah `decimal`. Compiler akan implicit-convert `double → decimal` (allowed) — tapi bisa hilang presisi untuk nilai besar.

**Fix:** gunakan `decimal` end-to-end untuk uang. Konversi `record.NetBuyValue` di awal.

### 12. `ConfigureAwait(false)` tidak diterapkan di library code

**File:** [SignalService.cs:41, 66, 70, 94](TradingRecommender.Application/UseCases/Signals/SignalService.cs#L41)

Aplikasi Worker tidak punya SynchronizationContext, jadi `ConfigureAwait(false)` hanya bersifat stylistic. **Tidak masalah** untuk Worker, tapi baik untuk portabilitas library.

**Fix:** None (Worker context).

### 13. `MonitorIHSGUseCase.ExecuteAsync` tidak handle `OperationCanceledException`

Kalau job dibatalkan oleh host shutdown, kita log error dan re-throw — benar. **Tapi** `OperationCanceledException` adalah **expected cancellation**, bukan failure.

**Fix:** catch `OperationCanceledException` separately, log info-level.

### 14. `MarketMonitoringResult.HasActionableSignals` query banyak di LINQ

**File:** [MarketMonitoringResult.cs:17-18](TradingRecommender.Application/UseCases/Messages/MarketMonitoringResult.cs#L17-L18)

```csharp
public bool HasActionableSignals => Recommendations.Any(r =>
    r.Strength is SignalStrength.StrongBuy or SignalStrength.Buy ...);
```

Setiap akses query ulang collection. Cheap (in-memory), tapi bisa di-cache sebagai bool.

**Fix:** computed property backed by field, atau biarkan (tidak masalah untuk ukuran kecil).

### 15. `EF Core` query tanpa `AsNoTracking` di read-only path

**File:** [DailyDigestJob.cs:37](TradingRecommender.Worker/Jobs/DailyDigestJob.cs#L37)

`db.Recommendations.Where(...).ToListAsync()` — EF default-nya tracking. Karena ini hanya baca, tambahkan `.AsNoTracking()`.

**Fix:** tambahkan `.AsNoTracking()`.

### 16. Missing `[Required]` validation di `TradingSettings`

**File:** [TradingSettings.cs](TradingRecommender.Domain/Configurations/TradingSettings.cs)

`.ValidateDataAnnotations().ValidateOnStart()` di-add, tapi POCO tidak punya `[Required]` attributes. Validate-on-start selalu pass.

**Fix:** tambahkan DataAnnotation attributes, atau ganti dengan `Validate(...)` manual.

### 17. Connection string bisa di-include di exception message saat DB connection fails

Npgsql exceptions kadang echo connection string di `.Message`. Logger default akan menulis exception.

**Fix:** wrap DbContext factory di logger filter, atau set `EnableSensitiveDataLogging(false)` (sudah false by default di production).

### 18. `appsettings.json` punya `GoApi.BaseUrl` hardcoded — bisa di-override attacker

Tidak masalah, tapi BaseUrl sebaiknya divalidasi sebagai absolute URI saat startup.

**Fix:** tambahkan URI validation.

### 19. Tidak ada rate limiting di TelegramBotClient

Tidak ada anti-flood. Kalau loop menghasilkan >30 pesan/menit, Telegram bisa rate-limit IP kita → 429.

**Fix:** tambahkan token bucket / semaphore.

---

## 🟢 LOW / INFORMATIONAL

### 20. `_logger.LogWarning` di `EvaluateSymbolAsync` bisa spam kalau symbol typo

**File:** [SignalService.cs:76](TradingRecommender.Application/UseCases/Signals/SignalService.cs#L76)

`No foreign-buy data for {Symbol}` — kalau caller pakai symbol yang salah terus-menerus, log akan penuh.

**Fix:** demote ke Debug, atau rate-limit.

### 21. Magic number `1_000_000_000m` di dua tempat

**File:** [SignalService.cs:132, 146](TradingRecommender.Application/UseCases/Signals/SignalService.cs#L132)

Angka "rupiah billions threshold conversion" muncul 2x. Taruh sebagai konstanta.

### 22. `StockTicker` entity dideklarasikan tapi tidak pernah dipakai

**File:** [StockTicker.cs](TradingRecommender.Domain/Entities/StockTicker.cs)

Dead code. Hapus atau gunakan di use case.

### 23. `AnalyzeForeignFlowUseCase` di Application.UseCases.Monitoring dipakai tapi tidak punya behaviour

**File:** [AnalyzeForeignFlowUseCase.cs](TradingRecommender.Application/UseCases/Monitoring/AnalyzeForeignFlowUseCase.cs)

Dead code — tidak dipakai lagi (yang dipakai adalah `ForeignFlowAnalyzer` di UseCases.ForeignFlow).

### 24. `Notify*Produced` message classes tidak punya subscriber

Message classes di Application/UseCases/Messages/ tidak ada handler-nya. Dead code atau in-progress mediator pattern.

### 25. `IUnitOfWork.DisposeAsync` di UnitOfWork bisa dobel dispose

`UnitOfWork` meng-`await _db.DisposeAsync()` tapi `_db` juga Scoped dan akan di-dispose otomatis oleh container. **Double dispose tapi aman di EF Core** (DbContext.Dispose idempotent).

### 26. `IRepository<T>.AsQueryable()` bocor IQueryable — ef bisa execute setelah scope disposal

Caller bisa menyimpan `query` lalu enumerate di luar scope → `ObjectDisposedException`.

**Fix:** return `Task<IReadOnlyList<T>>` dari repo method, atau dokumentasikan lifetime.

### 27. `PortfolioConfig.Stocks` mutable list — bisa dimodifikasi runtime

Hati-hati untuk data statis; lebih baik `IReadOnlyList<T>` dengan builder.

---

## ⚡ PERFORMANCE

### 28. `ScanMarketAsync` → `ScanPortfolioAsync` → loop linear pada dictionary

**File:** [SignalService.cs:95](TradingRecommender.Application/UseCases/Signals/SignalService.cs#L95)

`ToDictionary` O(N), `TryGetValue` O(1). Total O(N). OK untuk ukuran kecil.

**Fix:** None.

### 29. Tidak ada HTTP response caching

GOAPI di-panggil tiap kali job trigger. Bisa cache `foreign-buys` untuk 5 menit (Redis atau `IMemoryCache`).

**Fix:** tambahkan `IDistributedCache` atau `IMemoryCache` dengan TTL.

### 30. JSON serialization per-request

`GetFromJsonAsync<List<ForeignBuyRecord>>` setiap kali — `JsonSerializerOptions` di-recreate per call (implisit). Untuk list kecil ini tidak masalah; untuk dataset besar tambahkan singleton `JsonSerializerOptions`.

**Fix:** supply static `JsonSerializerOptions` singleton.

### 31. EF Core change-tracking untuk read-only queries

**File:** [DailyDigestJob.cs:37](TradingRecommender.Worker/Jobs/DailyDigestJob.cs#L37)

Tracking entities that we only read.

**Fix:** `.AsNoTracking()`.

### 32. `INSERT batch` lewat EF Core — bisa lebih cepat dengan COPY

Untuk bulk insert, `Npgsql` punya `COPY` mode. Saat ini tidak dipakai — pakai jika >1000 rows/sec.

---

## ✅ WHAT'S GOOD

- ✅ **Lifetime management** sudah benar (singleton/scoped/transient konsisten).
- ✅ **Polly retry + circuit breaker** di GOAPI client.
- ✅ **HttpClientFactory** + typed clients (tidak ada `new HttpClient()`).
- ✅ **Clean Architecture** layer separation jelas.
- ✅ **Quartz PostgreSQL job-store** (schedules persistent).
- ✅ **IOptions + IOptionsSnapshot** siap untuk hot-reload.
- ✅ **Docker multi-stage** + non-root + Alpine.
- ✅ **DB indexes** di `TickerSymbol`, `CreatedAt`, `Timestamp`.
- ✅ **JSONB** untuk flexible `Context` / `Metadata`.
- ✅ **`IServiceScopeFactory`** digunakan benar di job.
