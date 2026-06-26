namespace TradingRecommender.Application.Interfaces;

/// <summary>
/// Contract for Telegram notification client.
/// </summary>
public interface ITelegramBotClient
{
    /// <summary>
    /// Sends a text message to the configured chat.
    /// </summary>
    Task<bool> SendAsync(string message, string? parseMode = "Markdown");
}
