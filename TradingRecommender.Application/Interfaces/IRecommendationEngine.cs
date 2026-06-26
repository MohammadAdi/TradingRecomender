using TradingRecommender.Domain.Entities;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.Interfaces;

/// <summary>
/// Produces recommendations for the portfolio list.
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Generates a <see cref="TradingRecommendation"/> for the given symbol.
    /// </summary>
    Task<TradingRecommendation?> GenerateRecommendationAsync(
        string symbol,
        SignalStrength strength,
        double netBuyValue,
        double foreignFlow,
        long marketVolume,
        string rationale);
}
