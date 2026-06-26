using TradingRecommender.Application.Interfaces;
using TradingRecommender.Domain.Entities;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.UseCases.Recommendations;

/// <summary>
/// Default <see cref="IRecommendationEngine"/>. Translates a raw signal evaluation
/// into a persistable <see cref="TradingRecommendation"/> aggregate.
/// </summary>
public class RecommendationEngine : IRecommendationEngine
{
    public Task<TradingRecommendation?> GenerateRecommendationAsync(
        string symbol,
        SignalStrength strength,
        double netBuyValue,
        double foreignFlow,
        long marketVolume,
        string rationale)
    {
        if (strength == SignalStrength.Neutral)
            return Task.FromResult<TradingRecommendation?>(null);

        var rec = new TradingRecommendation
        {
            TickerSymbol = symbol.ToUpperInvariant(),
            Strength = strength,
            Rationale = rationale,
            ForeignFlowNet = (decimal)foreignFlow,
            MarketVolume = (decimal)marketVolume,
            Context = new Dictionary<string, object>
            {
                ["net_buy_value"] = netBuyValue,
                ["generated_at"] = DateTime.UtcNow
            }
        };

        return Task.FromResult<TradingRecommendation?>(rec);
    }
}