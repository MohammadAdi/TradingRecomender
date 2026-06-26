using TradingRecommender.Application.Interfaces.Persistence;
using TradingRecommender.Domain.Common;
using TradingRecommender.Domain.Entities;

namespace TradingRecommender.Infrastructure.Persistence;

/// <summary>
/// Unit of Work exposing repositories and a single SaveChanges boundary.
/// Registered as Scoped so it shares the same DbContext lifetime as
/// the job execution scope (created by Quartz's DI job factory).
///
/// <para>
/// The repositories are registered separately via
/// <c>services.AddScoped(typeof(IRepository&lt;&gt;), typeof(Repository&lt;&gt;))</c>
/// in <see cref="DependencyInjection"/>, so the constructor does not
/// instantiate them manually — that avoids capturing the DbContext
/// into UoW and allows DI-resolved sub-dependencies.
/// </para>
/// </summary>
public class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly AppDbContext _db;
    private IRepository<TradingRecommendation>? _recommendations;
    private IRepository<MarketSnapshot>? _snapshots;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public IRepository<TradingRecommendation> Recommendations
        => _recommendations ??= new Repository<TradingRecommendation>(_db);

    public IRepository<MarketSnapshot> Snapshots
        => _snapshots ?? new Repository<MarketSnapshot>(_db);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    public async ValueTask DisposeAsync()
    {
        _recommendations = null;
        _snapshots = null;
        GC.SuppressFinalize(this);
        await _db.DisposeAsync();
    }
}
