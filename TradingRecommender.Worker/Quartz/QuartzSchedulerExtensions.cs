using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using TradingRecommender.Domain;
using TradingRecommender.Domain.Configurations;
using TradingRecommender.Worker.Jobs;

namespace TradingRecommender.Worker.Quartz;

/// <summary>
/// Wires Quartz into the DI container with persistent PostgreSQL job-store
/// (schedules survive process restarts) and registers the configured cron
/// triggers read from <c>TradingSettings.Schedule</c>.
///
/// <para>
/// Lifetime design:
/// </para>
/// <list type="bullet">
///   <item>Job classes are registered as <b>Transient</b> — Quartz creates a
///         fresh instance per fire.</item>
///   <item>Quartz's <see cref="UseMicrosoftDependencyInjectionJobFactory"/>
///         creates a fresh <see cref="IServiceScope"/> for each execution,
///         so scoped dependencies (DbContext, UnitOfWork, use-cases,
///         HttpClient) are disposed automatically when the job returns.</item>
///   <item>The scheduler itself is registered as Singleton by Quartz — it
///         must never capture scoped services in its constructor.</item>
/// </list>
/// </summary>
public static class QuartzSchedulerExtensions
{
    public static IServiceCollection AddQuartzScheduler(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var schedule = configuration
            .GetSection(ScheduleConfig.SectionKey)
            .Get<ScheduleConfig>() ?? new ScheduleConfig();

        // -- Register job classes as Transient so Quartz's DI factory can
        //    resolve them from a per-execution scope without capturing
        //    any Scoped service into the scheduler singleton.
        services.AddTransient<MonitorIHSGJob>();
        services.AddTransient<DailyDigestJob>();

        services.AddQuartz(q =>
        {
            // DI-aware job factory is the default in Quartz 3.5+.

            // -- Persistent PostgreSQL job-store --
            // Schedules live in the same database as application data; the
            // Quartz schema is auto-created on first start.
            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UsePostgres(opts =>
                {
                    var connectionString = configuration
                        .GetSection(DatabaseConfig.SectionKey)
                        .Get<DatabaseConfig>()?.ConnectionString
                        ?? throw new InvalidOperationException(
                            "Database:ConnectionString is required for Quartz job-store.");

                    opts.ConnectionString = connectionString;
                    opts.TablePrefix = "qrtz_";
                });
                // Quartz 3.7+ uses System.Text.Json by default; Newtonsoft.Json package was removed.
            });

            // -- IHSG monitoring job --
            var monitoringKey = new JobKey(MonitorIHSGJob.JobKey);
            q.AddJob<MonitorIHSGJob>(j => j
                .WithIdentity(monitoringKey)
                .StoreDurably()
                .WithDescription("Scan IHSG: foreign flow + volume + portfolio"));

            q.AddTrigger(t => t
                .WithIdentity($"{MonitorIHSGJob.JobKey}-trigger")
                .ForJob(monitoringKey)
                .WithCronSchedule(schedule.MarketScanCron)
                .WithDescription("Daily IHSG market scan"));

            // -- Daily digest job --
            var digestKey = new JobKey(DailyDigestJob.JobKey);
            q.AddJob<DailyDigestJob>(j => j
                .WithIdentity(digestKey)
                .StoreDurably()
                .WithDescription("Send daily digest to Telegram"));

            q.AddTrigger(t => t
                .WithIdentity($"{DailyDigestJob.JobKey}-trigger")
                .ForJob(digestKey)
                .WithCronSchedule(schedule.DigestCron)
                .WithDescription("Daily digest push to Telegram"));
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}