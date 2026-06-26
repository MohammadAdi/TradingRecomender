namespace TradingRecommender.Domain;

/// <summary>
/// GOAPI integration configuration.
/// </summary>
public sealed class GoApiConfig
{
    public const string SectionKey = "GoApi";

    public string BaseUrl { get; set; } = "https://api.stockbit.com/api/v2/realtime";
    public string ApiKey { get; set; } = string.Empty;
}
