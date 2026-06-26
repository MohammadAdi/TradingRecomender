# DI Lifetime Review — Trading Recommender Worker

## ✅ What's correct

| Component | Lifetime | Captures | Status |
|---|---|---|---|
| `IOptions<TradingSettings>` | Singleton | Snapshot POCO | ✅ Safe — POCOs are immutable from caller side |
| `IOptionsMonitor<TradingSettings>` | Singleton | Wraps `IConfiguration` | ✅ Safe — same source |
| `HttpClient` (typed, via `AddHttpClient`) | Singleton *handler* | Pooled `HttpMessageHandler` | ✅ Safe — managed by factory |
| `ITelegramBotClient` (typed) | Singleton handler | Pooled `HttpMessageHandler` | ✅ Safe |
| `IGoApiClient` (typed) | Singleton handler | Pooled `HttpMessageHandler` | ✅ Safe |
| `ILogger<T>` | Singleton | Logger factory | ✅ Safe |
| `Quartz scheduler` | Singleton (Quartz internal) | Job map | ✅ Safe — no scoped capture |
| `IJob` instances (IhsgMonitoringJob, DailyDigestJob) | **Transient** via `AddTransient` | Resolved per-fire from a fresh DI scope | ✅ Safe |
| `AppDbContext`, `IUnitOfWork` | **Scoped** | Created per Quartz execution via DI factory scope | ✅ Safe |
| `MonitorIHSGUseCase`, `AnalyzeForeignFlowUseCase`, `SignalService`, `RecommendationEngine` | **Scoped** | Same scope as DbContext | ✅ Safe |

## ⚠️ What was wrong & has been fixed

### Issue 1: Duplicate `IJobFactory` registration (FIXED)

**Before:**
```csharp
// Infrastructure/DependencyInjection.cs (BAD)
services.AddSingleton<Quartz.Spi.IJobFactory, ScopedJobFactory>();
```

**Problem:** Two factories were registered — `ScopedJobFactory` (via `AddSingleton`) AND `UseMicrosoftDependencyInjectionJobFactory()` (in `QuartzSchedulerExtensions`). The DI container's last registration wins, leading to ambiguous behaviour.

**Fix:** Removed `ScopedJobFactory` registration. Quartz now uses its built-in `UseMicrosoftDependencyInjectionJobFactory`, which creates a per-execution DI scope automatically.

### Issue 2: Scoped service captured by Singleton scheduler (FIXED)

**Before:**
```csharp
// Worker Jobs (BAD)
public IhsgMonitoringJob(IServiceScopeFactory scopeFactory, ...)
{
    _scopeFactory = scopeFactory;   // ← Captured, then re-scope per Execute
}
```

**Problem:** Each job was registered as Singleton by Quartz's default factory. Constructor-captured `IServiceScopeFactory` is OK (it's a Singleton), but manually creating a scope inside `Execute` was redundant when the DI factory would already have done so.

**Fix:** Removed `IServiceScopeFactory` capture from job constructors. Jobs are now `AddTransient`, resolved by Quartz's `UseMicrosoftDependencyInjectionJobFactory` from a per-fire scope. Scoped services (`AppDbContext`, `MonitorIHSGUseCase`, `ITelegramBotClient`) flow naturally.

### Issue 3: Quartz packages misplaced (FIXED)

**Before:** `Quartz.Extensions.Hosting` referenced from `Infrastructure` project (unused there).
**Fix:** Moved to `Worker.csproj` alongside `Quartz.Serialization.Newtonsoft` + `Quartz.Plugins` (needed for `UsePostgres`).

## Lifetime rules enforced going forward

1. **No constructor-injected `Scoped` service in any `Singleton` host.**
2. **Jobs are `Transient`** so each Quartz fire gets a fresh DI scope.
3. **All `DbContext` / `UnitOfWork` / use-case instances are `Scoped`** and travel inside the Quartz-created scope.
4. **HttpClients are typed & Singleton** — never `new HttpClient()` directly.
5. **`IOptions<T>` / `IOptionsMonitor<T>` are Singleton** but their bound POCOs are read-only at runtime.
