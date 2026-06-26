using TradingRecommender.Domain.Entities;

namespace TradingRecommender.Application.Interfaces.Persistence;

/// <summary>
/// Unit of Work for transactions across repositories.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<TradingRecommendation> Recommendations { get; }
    IRepository<MarketSnapshot> Snapshots { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Generic repository contract.
/// </summary>
public interface IRepository<T> where T : Domain.Common.BaseEntity
{
    IQueryable<T> AsQueryable();
    Task AddAsync(T entity, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
