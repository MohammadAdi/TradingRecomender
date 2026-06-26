namespace TradingRecommender.Domain;

using TradingRecommender.Domain.Enums;

/// <summary>
/// Configuration for foreign-flow threshold monitoring.
/// </summary>
public sealed class ForeignFlowThresholds
{
    public const string SectionKey = "Monitoring:ForeignFlow";

    /// <summary>
    /// Minimum net foreign buying (IDR billions) to trigger a Buy signal.
    /// </summary>
    public decimal BuyThreshold { get; set; } = 2.0m;

    /// <summary>
    /// Minimum net foreign selling (absolute) to trigger a Sell signal.
    /// </summary>
    public decimal SellThreshold { get; set; } = 2.0m;

    /// <summary>
    /// Floor level for market volume (shares) considered healthy.
    /// </summary>
    public long VolumeFloor { get; set; } = 5_000_000_000L;

    /// <summary>
    /// Ceiling level for market volume — if exceeded, caution / reduced signal strength.
    /// </summary>
    public long VolumeCeiling { get; set; } = 15_000_000_000L;

    public SignalStrength Evaluate(decimal netForeignFlowBillions, long volume)
    {
        // Bullish: strong buying
        if (netForeignFlowBillions >= BuyThreshold && volume >= VolumeFloor)
            return SignalStrength.Buy;

        // Bearish: strong selling
        if (netForeignFlowBillions <= -SellThreshold && volume >= VolumeFloor)
            return SignalStrength.Sell;

        // Caution on extreme volume
        if (volume > VolumeCeiling)
            return SignalStrength.Neutral;

        return SignalStrength.Neutral;
    }
}
