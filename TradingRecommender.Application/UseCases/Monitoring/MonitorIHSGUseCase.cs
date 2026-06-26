using Microsoft.Extensions.Logging;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Application.Interfaces.Persistence;
using TradingRecommender.Domain;
using TradingRecommender.Domain.Configurations;
using TradingRecommender.Domain.Entities;

namespace TradingRecommender.Application.UseCases.Monitoring;

/// <summary>
/// Orchestrates the IHSG monitoring cycle:
///  1. Delegate the heavy lifting (GOAPI fetch + threshold evaluation)
///     to <see cref="ISignalService"/>.
///  2. Translate <see cref="SignalResult"/>s into <see cref="Domain.Entities.TradingRecommendation"/>
///     aggregates via <see cref="IRecommendationEngine"/>.
///  3. Persist the batch through <see cref="IUnitOfWork"/> in a single
///     <c>SaveChangesAsync</c> roundtrip.
///
/// Cancellation: <see cref="OperationCanceledException"/> is treated as
/// expected shutdown and logged at Information, not as an error.
/// </summary>
public sealed class MonitorIHSGUseCase
{
    private readonly ISignalService _signals;
    private readonly IRecommendationEngine _engine;
    private readonly IUnitOfWork _uow;
    private readonly PortfolioConfig _portfolio;
    private readonly ILogger<MonitorIHSGUseCase> _logger;

    public MonitorIHSGUseCase(
        ISignalService signals,
        IRecommendationEngine engine,
        IUnitOfWork uow,
        PortfolioConfig portfolio,
        ILogger<MonitorIHSGUseCase> logger)
    {
        _signals = signals;
        _engine = engine;
        _uow = uow;
        _portfolio = portfolio;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            var scanScope = _portfolio.Stocks.Count > 0
                ? await _signals.ScanPortfolioAsync(ct).ConfigureAwait(false)
                : await _signals.ScanMarketAsync("IDX", ct).ConfigureAwait(false);

            var added = 0;

            foreach (var signal in scanScope)
            {
                if (!signal.IsActionable)
                    continue;

                var rec = await _engine.GenerateRecommendationAsync(
                    symbol: signal.Symbol,
                    strength: signal.Strength,
                    netBuyValue: signal.NetBuyValue,
                    foreignFlow: signal.NetBuyValue,
                    marketVolume: signal.Volume,
                    rationale: signal.Rationale).ConfigureAwait(false);

                if (rec is not null)
                {
                    await _uow.Recommendations.AddAsync(rec, ct).ConfigureAwait(false);
                    added++;
                }
            }

            if (added > 0)
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Monitor IHSG cycle finished: persisted {Count} new recommendations.",
                added);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Monitor IHSG cycle cancelled (shutdown).");
            throw;
        }
    }
}