using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Application.Interfaces.Persistence;
using TradingRecommender.Domain;
using TradingRecommender.Domain.Configurations;
using TradingRecommender.Infrastructure.Http;
using TradingRecommender.Infrastructure.Notifications;
using TradingRecommender.Infrastructure.Persistence;

namespace TradingRecommender.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. Wires DbContext, repositories,
/// HttpClientFactory with named Polly pipelines, and config binding.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // -- Configuration binding (Options pattern) --
        services.Configure<DatabaseConfig>(configuration.GetSection(DatabaseConfig.SectionKey));
        services.Configure<GoApiConfig>(configuration.GetSection(GoApiConfig.SectionKey));
        services.Configure<TelegramConfig>(configuration.GetSection(TelegramConfig.SectionKey));
        services.Configure<ScheduleConfig>(configuration.GetSection(ScheduleConfig.SectionKey));
        services.Configure<PortfolioConfig>(configuration.GetSection(PortfolioConfig.SectionKey));
        services.Configure<ForeignFlowThresholds>(
            configuration.GetSection(ForeignFlowThresholds.SectionKey));

        // -- Unified TradingSettings (IOptions / IOptionsSnapshot / IOptionsMonitor) --
        services.AddOptions<TradingSettings>()
            .Bind(configuration.GetSection(TradingSettings.SectionKey))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // -- EF Core / PostgreSQL (scoped per request / per job) --
        var connectionString = configuration.GetSection(DatabaseConfig.SectionKey)
            .Get<DatabaseConfig>()?.ConnectionString
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(connectionString, npg => npg.MigrationsAssembly("TradingRecommender.Infrastructure")));

        // -- Unit of Work (Scoped) --
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // -- Generic Repository (Scoped, per DbContext lifetime) --
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // -- HttpClientFactory + Polly (named pipeline "goapi") --
        services.AddHttpClient<IGoApiClient, GoApiClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration.GetSection(GoApiConfig.SectionKey).Get<GoApiConfig>()?.BaseUrl
                    ?? "https://api.goapi.io/");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<RedactingLoggingHandler>()        // <-- redacts headers in logs
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILogger<GoApiClient>>();
                return ResiliencePolicies.GetRetryPolicy(logger);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILogger<GoApiClient>>();
                return ResiliencePolicies.GetCircuitBreakerPolicy(logger);
            });

        // -- Telegram Bot typed client (shared resilience) --
        services.AddHttpClient<ITelegramBotClient, TelegramBotClient>(c =>
            {
                c.BaseAddress = new Uri("https://api.telegram.org/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<RedactingLoggingHandler>()        // <-- redacts headers in logs
            .AddStandardResilienceHandler();

        // The redacting handler must be resolvable from the HttpClientFactory's
        // message-handler scope (Transient).
        services.AddTransient<RedactingLoggingHandler>();

        // NOTE: Quartz job factory is registered by AddQuartzScheduler in Worker
        // (it owns its own DI requirements and must be configured before AddQuartz).

        return services;
    }
}