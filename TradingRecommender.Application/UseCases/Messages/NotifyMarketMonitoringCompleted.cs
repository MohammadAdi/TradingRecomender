namespace TradingRecommender.Application.UseCases.Messages;

/// <summary>
/// Event raised when a market-monitoring cycle completes.
/// </summary>
public sealed class NotifyMarketMonitoringCompleted
{
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
