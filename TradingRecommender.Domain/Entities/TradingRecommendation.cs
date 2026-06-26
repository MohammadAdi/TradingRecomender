namespace TradingRecommender.Domain.Entities;

using TradingRecommender.Domain.Enums;

/// <summary>
/// Recommendation signal emitted by the bot for a specific ticker.
/// </summary>
public class TradingRecommendation : Common.BaseEntity
{
    public string TickerSymbol { get; set; } = string.Empty;
    public SignalStrength Strength { get; set; }
    public decimal? CurrentPrice { get; set; }
    public string? Rationale { get; set; }
    public decimal ForeignFlowNet { get; set; }
    public decimal MarketVolume { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// String representation of the recommendation for display.
    /// </summary>
    public string SignalLabel
        => Strength switch
        {
            SignalStrength.StrongBuy => "Strong Buy",
            SignalStrength.Buy => "Buy",
            SignalStrength.Neutral => "Neutral",
            SignalStrength.Sell => "Sell",
            SignalStrength.StrongSell => "Strong Sell",
            _ => "Unknown"
        };
}
