using Microsoft.EntityFrameworkCore;
using TradingRecommender.Application.Interfaces.Persistence;
using TradingRecommender.Domain.Common;

namespace TradingRecommender.Infrastructure.Persistence;

/// <summary>
/// Generic EF Core-backed repository. Each call uses the scoped DbContext
/// obtained from <see cref="AppDbContext"/>.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly AppDbContext _db;
    public Repository(AppDbContext db) => _db = db;

    public IQueryable<T> AsQueryable() => _db.Set<T>().AsQueryable();

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _db.Set<T>().AddAsync(entity, ct);

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);
}
