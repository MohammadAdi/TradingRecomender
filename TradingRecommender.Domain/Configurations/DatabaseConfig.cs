namespace TradingRecommender.Domain.Configurations;

/// <summary>
/// Database connection configuration.
/// </summary>
public sealed class DatabaseConfig
{
    public const string SectionKey = "Database";

    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
