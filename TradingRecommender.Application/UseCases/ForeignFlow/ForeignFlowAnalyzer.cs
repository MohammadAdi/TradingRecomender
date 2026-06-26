using TradingRecommender.Application.Interfaces;
using TradingRecommender.Domain;

namespace TradingRecommender.Application.UseCases.ForeignFlow;

/// <summary>
/// Maps raw foreign-flow records into actionable signals using the configured
/// thresholds. Kept in the Application layer so it can be unit-tested without
/// any infrastructure dependencies.
/// </summary>
public sealed class ForeignFlowAnalyzer : ISignalEvaluator
{
    public SignalEvaluation Evaluate(
        string symbol,
        ForeignBuyRecord record,
        ForeignFlowThresholds config)
    {
        var netBuy = record.NetBuyValue;

        var strength = config.Evaluate((decimal)netBuy, record.Volume);

        var rationale = strength switch
        {
            Domain.Enums.SignalStrength.Buy or Domain.Enums.SignalStrength.StrongBuy =>
                $"{symbol}: asing BELI neto Rp{netBuy / 1_000_000:0.00}M (Vol {record.Volume:N0})",
            Domain.Enums.SignalStrength.Sell or Domain.Enums.SignalStrength.StrongSell =>
                $"{symbol}: asing JUAL neto Rp{Math.Abs(netBuy) / 1_000_000:0.00}M (Vol {record.Volume:N0})",
            _ => $"{symbol}: tidak ada sinyal (Vol {record.Volume:N0})"
        };

        return new SignalEvaluation(
            symbol,
            strength,
            rationale,
            netBuy,
            record.Volume);
    }
}