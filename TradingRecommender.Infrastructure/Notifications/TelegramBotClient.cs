using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Domain;

namespace TradingRecommender.Infrastructure.Notifications;

/// <summary>
/// Minimal Telegram Bot HTTP client. Registered as a typed client via
/// <c>AddHttpClient&lt;ITelegramBotClient, TelegramBotClient&gt;()</c>.
/// Reuses the default <c>"default"</c> HttpClient resilience pipeline.
///
/// Security:
///  - Never logs request/response bodies containing tokens.
///  - Token bucket rate-limiter prevents Telegram 429 flood responses.
/// </summary>
public class TelegramBotClient : ITelegramBotClient
{
    public const string HttpClientName = "telegram";
    public const string ResiliencePipeline = "telegram";
    public const int MaxMessageLength = 4096;

    private readonly HttpClient _http;
    private readonly TelegramConfig _config;
    private readonly ILogger<TelegramBotClient> _logger;

    /// <summary>
    /// Rate limiter: 1 message per 1.2s minimum (Telegram API allows ~30/min,
    /// we give ourselves headroom). Wrapped in a SemaphoreSlim to serialize.
    /// </summary>
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastSend = DateTime.MinValue;

    public TelegramBotClient(
        HttpClient http,
        IOptions<TelegramConfig> config,
        ILogger<TelegramBotClient> logger)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string message, string? parseMode = null)
    {
        if (string.IsNullOrWhiteSpace(_config.BotToken) ||
            string.IsNullOrWhiteSpace(_config.ChatId))
        {
            _logger.LogDebug("Telegram not configured; skipping send.");
            return false;
        }

        parseMode ??= "Markdown";
        message = SanitizeMessage(message);

        await RateLimitAsync();

        var url = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";
        var payload = new
        {
            chat_id = _config.ChatId,
            text = message,
            parse_mode = parseMode
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                // SECURITY: never log body — it may contain the token or chatId.
                _logger.LogError("Telegram send failed: {Status}", response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log type only; HttpRequestException Message can echo sensitive URLs.
            _logger.LogError(ex, "Telegram send threw {ExceptionType}", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Enforces minimum 1.2s gap between sends to prevent Telegram 429 rate-limit.
    /// </summary>
    private async Task RateLimitAsync()
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastSend;
            if (elapsed < TimeSpan.FromMilliseconds(1200))
            {
                var waitMs = (int)(TimeSpan.FromMilliseconds(1200) - elapsed).TotalMilliseconds;
                await Task.Delay(waitMs);
            }
            _lastSend = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Truncates to Telegram's 4096-char hard limit, appending a note if truncated.
    /// </summary>
    private static string SanitizeMessage(string input)
    {
        if (input.Length <= MaxMessageLength)
            return input;

        return input[..(MaxMessageLength - 40)]
             + "\n\n── truncated for 4096-char limit ──";
    }
}