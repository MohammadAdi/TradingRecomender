using TradingRecommender.Domain;
using TradingRecommender.Domain.Entities;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.Interfaces;

/// <summary>
/// Evaluates foreign-flow metrics into trading signals.
/// </summary>
public interface ISignalEvaluator
{
    /// <summary>
    /// Evaluates a single symbol given its foreign-buy stats and thresholds.
    /// </summary>
    SignalEvaluation Evaluate(string symbol, ForeignBuyRecord record, ForeignFlowThresholds config);
}

/// <summary>
/// Result of a signal evaluation.
/// </summary>
public record SignalEvaluation(
    string Symbol,
    SignalStrength Strength,
    string Rationale,
    double NetBuyValue,
    double Volume,
    IReadOnlyDictionary<string, object> ExtraContext = null!);
