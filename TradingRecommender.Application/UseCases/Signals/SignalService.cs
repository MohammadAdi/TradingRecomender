using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Domain.Configurations;
using TradingRecommender.Domain.Enums;

namespace TradingRecommender.Application.UseCases.Signals;

/// <summary>
/// Default <see cref="ISignalService"/> implementation. Fetches raw market data
/// from <see cref="IGoApiClient"/>, applies the configured foreign-flow
/// thresholds (<see cref="TradingSettings.ForeignFlow"/>), and returns
/// actionable <see cref="SignalResult"/>s.
///
/// Performance:
///  • <see cref="JsonSerializerOptions"/> reused (singleton).
///  • Parallel evaluation of records (<see cref="ParallelOptions.MaxDegreeOfParallelism"/> = 4)
///    — limits EF/HTTP load while still finishing faster than serial.
/// </summary>
public sealed class SignalService : ISignalService
{
    /// <summary>
    /// Shared serializer options. Casing, number handling and property name
    /// policy set once, reused on every JSON roundtrip.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        // Consistent culture for JSON numbers/strings
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private const long IdrBillionsMultiplier = 1_000_000_000L;
    private const int MaxParallelEvaluations = 4;

    private readonly IGoApiClient _goApi;
    private readonly TradingSettings _settings;
    private readonly ILogger<SignalService> _logger;

    public SignalService(
        IGoApiClient goApi,
        IOptions<TradingSettings> settings,
        ILogger<SignalService> logger)
    {
        _goApi = goApi ?? throw new ArgumentNullException(nameof(goApi));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SignalResult>> ScanMarketAsync(
        string market = "IDX",
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting market scan for {Market}", market);

        var records = await _goApi.GetForeignBuysAsync(market, ct).ConfigureAwait(false);
        _logger.LogDebug("Received {Count} foreign-buy records", records.Count);

        var parallelOpts = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxParallelEvaluations,
            CancellationToken = ct
        };

        // Evaluate concurrently (pure CPU-bound mapping, no I/O).
        var results = new List<SignalResult>(records.Count);
        await Parallel.ForEachAsync(
            records,
            parallelOpts,
            (record, token) =>
            {
                var signal = EvaluateRecord(record);
                if (signal is not null)
                {
                    lock (results)   // List<T> not thread-safe
                        results.Add(signal);
                }
                return ValueTask.CompletedTask;
            })
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Scan complete: {Actionable}/{Total} actionable signals",
            results.Count(r => r.IsActionable), results.Count);

        return results;
    }

    public async Task<SignalResult?> EvaluateSymbolAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        // Avoid double-call: fetch price + flow concurrently.
        var priceTask = _goApi.GetPriceAsync(symbol, ct);
        var flowTask = _goApi.GetForeignBuysAsync("IDX", ct);
        await Task.WhenAll(priceTask, flowTask).ConfigureAwait(false);

        var price = await priceTask.ConfigureAwait(false);
        var all = await flowTask.ConfigureAwait(false);

        var record = all.FirstOrDefault(r =>
            string.Equals(r.TickerSymbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            _logger.LogDebug("No foreign-buy data for {Symbol}", symbol);
            return null;
        }

        return EvaluateRecord(record, price?.LastPrice);
    }

    public async Task<IReadOnlyList<SignalResult>> ScanPortfolioAsync(CancellationToken ct = default)
    {
        var symbols = _settings.Portfolio.Stocks
            .Select(s => s.Symbol)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("Scanning portfolio ({Count} symbols)", symbols.Count);

        var all = await ScanMarketAsync("IDX", ct).ConfigureAwait(false);
        var bySymbol = all.ToDictionary(r => r.Symbol, StringComparer.OrdinalIgnoreCase);

        var portfolioSignals = new List<SignalResult>(symbols.Count);
        foreach (var symbol in symbols)
        {
            if (bySymbol.TryGetValue(symbol, out var signal))
                portfolioSignals.Add(signal);
            // demoted to Debug — avoids log spam if a symbol is missing.
        }

        return portfolioSignals;
    }

    // -- Private helpers ------------------------------------------------

    /// <summary>
    /// Pure-function signal evaluation. No side-effects, easy to unit-test.
    /// </summary>
    private SignalResult? EvaluateRecord(ForeignBuyRecord record, double? lastPrice = null)
    {
        var f = _settings.ForeignFlow;
        var netBuy = record.NetBuyValue;
        var volume = record.Volume;

        SignalStrength strength;
        string rationale;

        if (netBuy >= 0)
        {
            // Net buy territory
            if (volume < f.VolumeFloor)
            {
                // Thin market — ignore regardless of direction
                return null;
            }

            strength = netBuy >= (double)f.BuyThreshold * IdrBillionsMultiplier
                ? SignalStrength.StrongBuy
                : SignalStrength.Buy;

            rationale =
                $"{record.TickerSymbol}: asing BELI neto Rp{netBuy / 1_000_000:0.00}M " +
                $"| Vol {volume:N0} (floor {f.VolumeFloor:N0})";
        }
        else
        {
            var absSell = Math.Abs(netBuy);
            if (volume < f.VolumeFloor)
                return null;

            strength = absSell >= (double)f.SellThreshold * IdrBillionsMultiplier
                ? SignalStrength.StrongSell
                : SignalStrength.Sell;

            rationale =
                $"{record.TickerSymbol}: asing JUAL neto Rp{absSell / 1_000_000:0.00}M " +
                $"| Vol {volume:N0} (floor {f.VolumeFloor:N0})";
        }

        return new SignalResult(
            Symbol: record.TickerSymbol,
            Strength: strength,
            Rationale: rationale,
            NetBuyValue: netBuy,
            Volume: volume,
            LastPrice: lastPrice,
            DetectedAt: DateTime.UtcNow);
    }
}