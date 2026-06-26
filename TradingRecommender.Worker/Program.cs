using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingRecommender.Application;
using TradingRecommender.Infrastructure;
using TradingRecommender.Worker.Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "TRADINGBOT_")
    .AddUserSecrets<Program>();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// -- Composition root (Clean Architecture) --
// Order matters: Application first (registers domain services),
// then Infrastructure (wires DB + HTTP + config), then Worker (wires Quartz).
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddQuartzScheduler(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
