using TradingRecommender.Domain.Entities;

namespace TradingRecommender.Application.UseCases.Messages;

/// <summary>
/// Domain event message: a recommendation was produced and should be notified.
/// </summary>
public sealed class NotifyRecommendationProduced
{
    public TradingRecommendation Recommendation { get; set; } = null!;
}
