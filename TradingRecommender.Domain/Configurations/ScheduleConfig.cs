namespace TradingRecommender.Domain;

/// <summary>
/// Daily schedule configuration (cron-like expressions).
/// </summary>
public sealed class ScheduleConfig
{
    public const string SectionKey = "Schedule";

    /// <summary>
    /// Cron expression for market monitoring (e.g. "0 9 * * MON-FRI").
    /// </summary>
    public string MarketScanCron { get; set; } = "0 9 * * MON-FRI";

    /// <summary>
    /// Cron expression for sending daily digest (e.g. "0 16 * * MON-FRI").
    /// </summary>
    public string DigestCron { get; set; } = "0 16 * * MON-FRI";
}
