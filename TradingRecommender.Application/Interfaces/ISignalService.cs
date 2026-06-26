using TradingRecommender.Domain.Configurations;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.Interfaces;

/// <summary>
/// High-level signal service. Fetches raw market data from
/// <see cref="IGoApiClient"/>, applies the configured foreign-flow
/// thresholds (<see cref="TradingSettings.ForeignFlow"/>), and returns
/// actionable <see cref="SignalResult"/>s for each symbol in the watch-list.
/// </summary>
public interface ISignalService
{
    /// <summary>
    /// Scans the entire market (default: IDX) and returns signals for
    /// symbols whose foreign-flow / volume cross the configured thresholds.
    /// </summary>
    Task<IReadOnlyList<SignalResult>> ScanMarketAsync(string market = "IDX", CancellationToken ct = default);

    /// <summary>
    /// Evaluates a single symbol against the live threshold configuration.
    /// </summary>
    Task<SignalResult?> EvaluateSymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Scans the configured <see cref="TradingSettings.Portfolio"/> watch-list only.
    /// </summary>
    Task<IReadOnlyList<SignalResult>> ScanPortfolioAsync(CancellationToken ct = default);
}

/// <summary>
/// Single-symbol signal result produced by <see cref="ISignalService"/>.
/// </summary>
/// <param name="Symbol">Ticker code, e.g. "BBCA".</param>
/// <param name="Strength">Buy / Sell / Neutral classification.</param>
/// <param name="Rationale">Human-readable explanation.</param>
/// <param name="NetBuyValue">Net foreign buy value (IDR).</param>
/// <param name="Volume">Daily traded volume.</param>
/// <param name="LastPrice">Latest available price (nullable if not fetched).</param>
/// <param name="DetectedAt">UTC timestamp when the signal was computed.</param>
public sealed record SignalResult(
    string Symbol,
    SignalStrength Strength,
    string Rationale,
    double NetBuyValue,
    long Volume,
    double? LastPrice,
    DateTime DetectedAt)
{
    /// <summary>True when the signal is actionable (Buy / Sell / Strong).</summary>
    public bool IsActionable =>
        Strength is SignalStrength.Buy
                  or SignalStrength.StrongBuy
                  or SignalStrength.Sell
                  or SignalStrength.StrongSell;
}