using Microsoft.EntityFrameworkCore;
using TradingRecommender.Domain.Entities;

namespace TradingRecommender.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext backed by PostgreSQL. Holds the aggregate roots
/// whose state the bot persists across scheduler invocations.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TradingRecommendation> Recommendations => Set<TradingRecommendation>();
    public DbSet<MarketSnapshot> Snapshots => Set<MarketSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TradingRecommendation>(b =>
        {
            b.ToTable("recommendations");
            b.HasKey(x => x.Id);
            b.Property(x => x.TickerSymbol).HasMaxLength(16).IsRequired();
            b.Property(x => x.Rationale).HasMaxLength(1024);
            b.Property(x => x.Context).HasColumnType("jsonb");
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.TickerSymbol);
        });

        modelBuilder.Entity<MarketSnapshot>(b =>
        {
            b.ToTable("market_snapshots");
            b.HasKey(x => x.Id);
            b.Property(x => x.MonitorType).HasConversion<int>();
            b.Property(x => x.Metadata).HasColumnType("jsonb");
            b.HasIndex(x => x.Timestamp);
        });
    }
}
