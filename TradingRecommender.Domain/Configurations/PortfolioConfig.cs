namespace TradingRecommender.Domain;

/// <summary>
/// Portfolio configuration.
/// </summary>
public sealed class PortfolioConfig
{
    public const string SectionKey = "Portfolio";

    /// <summary>
    /// Stocks in the portfolio to monitor.
    /// </summary>
    public IList<PortfolioStockEntry> Stocks { get; set; } = new List<PortfolioStockEntry>();

    /// <summary>
    /// Whether to evaluate portfolio positions against recommendations.
    /// </summary>
    public bool TrackPerformance { get; set; } = true;
}

/// <summary>
/// Individual stock entry in the portfolio config.
/// </summary>
public sealed class PortfolioStockEntry
{
    /// <summary>
    /// Stock symbol, e.g. "BBCA", "TLKM".
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for display.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Quantity held in the portfolio.
    /// </summary>
    public decimal PositionQty { get; set; }
}
