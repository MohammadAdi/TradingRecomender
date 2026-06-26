using Microsoft.Extensions.DependencyInjection;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Application.UseCases.ForeignFlow;
using TradingRecommender.Application.UseCases.Monitoring;
using TradingRecommender.Application.UseCases.Recommendations;
using TradingRecommender.Application.UseCases.Signals;

namespace TradingRecommender.Application;

/// <summary>
/// Registers Application-layer services and use-cases with the DI container.
/// All handlers are Scoped — they share a Unit of Work per Quartz-job execution.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Domain services
        services.AddScoped<ISignalEvaluator, ForeignFlowAnalyzer>();
        services.AddScoped<IRecommendationEngine, RecommendationEngine>();

        // High-level signal pipeline
        services.AddScoped<ISignalService, SignalService>();

        // Use-case handlers
        services.AddScoped<MonitorIHSGUseCase>();

        return services;
    }
}