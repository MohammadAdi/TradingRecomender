using System.ComponentModel.DataAnnotations;

namespace TradingRecommender.Domain.Configurations;

/// <summary>
/// Unified, strongly-typed trading configuration. Maps 1:1 to the
/// "TradingSettings" section in <c>appsettings.json</c> and is consumed
/// via <c>IOptions&lt;TradingSettings&gt;</c> / <c>IOptionsMonitor</c>.
///
/// Validated at startup via <c>AddOptions&lt;TradingSettings&gt;().ValidateDataAnnotations().ValidateOnStart()</c>.
/// </summary>
public sealed class TradingSettings
{
    public const string SectionKey = "TradingSettings";

    [Required] public GoApiSettings GoApi { get; set; } = new();
    [Required] public TelegramSettings Telegram { get; set; } = new();
    [Required] public ScheduleSettings Schedule { get; set; } = new();
    [Required] public ForeignFlowSettings ForeignFlow { get; set; } = new();
    [Required] public PortfolioSettings Portfolio { get; set; } = new();
    [Required] public NotificationSettings Notification { get; set; } = new();
}

public sealed class GoApiSettings
{
    [Required, Url]
    public string BaseUrl { get; set; } = "https://api.goapi.io/v1/stock";

    [Required, MinLength(8)]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 15;

    [Range(0, 20)]
    public int RetryCount { get; set; } = 5;
}

public sealed class TelegramSettings
{
    [Required, MinLength(8)]
    public string BotToken { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string ChatId { get; set; } = string.Empty;

    [Required, RegularExpression("^(Markdown|MarkdownV2|HTML)$",
        ErrorMessage = "ParseMode must be Markdown, MarkdownV2, or HTML.")]
    public string ParseMode { get; set; } = "Markdown";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}

public sealed class ScheduleSettings
{
    [Required, RegularExpression(@"^\S+\s+\S+\s+\S+\s+\S+\s+\S+",
        ErrorMessage = "Cron must contain 5 fields.")]
    public string MarketScanCron { get; set; } = "0 9 * * MON-FRI";

    [Required, RegularExpression(@"^\S+\s+\S+\s+\S+\s+\S+\s+\S+",
        ErrorMessage = "Cron must contain 5 fields.")]
    public string DigestCron { get; set; } = "0 16 * * MON-FRI";

    public string? TimeZoneId { get; set; } = "Asia/Jakarta";
}

public sealed class ForeignFlowSettings
{
    [Range(0, double.MaxValue)] public decimal BuyThreshold { get; set; } = 2.0m;
    [Range(0, double.MaxValue)] public decimal SellThreshold { get; set; } = 2.0m;
    [Range(0, long.MaxValue)]  public long VolumeFloor { get; set; } = 5_000_000_000L;
    [Range(0, long.MaxValue)]  public long VolumeCeiling { get; set; } = 15_000_000_000L;
}

public sealed class PortfolioSettings
{
    public bool TrackPerformance { get; set; } = true;
    public IList<PortfolioStockEntry> Stocks { get; set; } = new List<PortfolioStockEntry>();
}

public sealed class NotificationSettings
{
    public TimeOnly QuietHoursStart { get; set; } = new(17, 0);
    public TimeOnly QuietHoursEnd { get; set; } = new(8, 0);

    [Range(1, 100)]
    public int MaxItemsPerDigest { get; set; } = 20;

    public bool OnlyActionable { get; set; } = true;
}
