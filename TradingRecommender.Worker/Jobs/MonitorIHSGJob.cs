using Microsoft.Extensions.Logging;
using Quartz;
using TradingRecommender.Application.UseCases.Monitoring;

namespace TradingRecommender.Worker.Jobs;

/// <summary>
/// Quartz job wrapper that delegates IHSG monitoring to the use case.
/// [DisallowConcurrentExecution] prevents overlapping scans.
/// </summary>
[DisallowConcurrentExecution]
public class MonitorIHSGJob : IJob
{
    public const string JobKey = "monitor-ihsg-job";

    private readonly MonitorIHSGUseCase _useCase;
    private readonly ILogger<MonitorIHSGJob> _logger;

    public MonitorIHSGJob(MonitorIHSGUseCase useCase, ILogger<MonitorIHSGJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("IHSG monitoring job triggered at {Time}", DateTime.UtcNow);

        try
        {
            await _useCase.ExecuteAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("MonitorIHSGJob cancelled (expected during shutdown).");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonitorIHSGJob failed unexpectedly.");
            throw;
        }
    }
}