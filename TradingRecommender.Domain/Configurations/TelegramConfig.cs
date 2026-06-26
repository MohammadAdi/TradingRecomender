namespace TradingRecommender.Domain;

/// <summary>
/// Telegram notification configuration.
/// </summary>
public sealed class TelegramConfig
{
    public const string SectionKey = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}
