namespace TradingRecommender.Domain.Entities;

using TradingRecommender.Domain.Common;
using TradingRecommender.Domain.Enums;

/// <summary>
/// Snapshot of IHSG market metrics at a point in time.
/// </summary>
public class MarketSnapshot : BaseEntity
{
    public DateTime Timestamp { get; set; }
    public MonitorType MonitorType { get; set; }
    public decimal IhsgValue { get; set; }
    public decimal ForeignFlowNet { get; set; }
    public long Volume { get; set; }
    public decimal MarketCap { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
