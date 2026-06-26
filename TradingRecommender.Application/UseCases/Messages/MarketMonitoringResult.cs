using TradingRecommender.Domain.Entities;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.UseCases.Messages;

/// <summary>
/// Result message wrapping recommendations and snapshots from monitoring.
/// </summary>
public sealed class MarketMonitoringResult
{
    public IList<TradingRecommendation> Recommendations { get; set; } = [];
    public IList<MarketSnapshot> Snapshots { get; set; } = [];

    /// <summary>
    /// Whether any actionable signal was detected.
    /// </summary>
    public bool HasActionableSignals => Recommendations.Any(r =>
        r.Strength is SignalStrength.StrongBuy or SignalStrength.Buy or SignalStrength.StrongSell or SignalStrength.Sell);
}
