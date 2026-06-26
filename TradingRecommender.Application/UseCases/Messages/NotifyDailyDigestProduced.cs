namespace TradingRecommender.Application.UseCases.Messages;

/// <summary>
/// Event raised when daily digest is produced and should be sent.
/// </summary>
public sealed class NotifyDailyDigestProduced
{
    public string DigestMessage { get; set; } = string.Empty;
}
