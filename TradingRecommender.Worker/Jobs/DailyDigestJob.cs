using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Infrastructure.Persistence;

namespace TradingRecommender.Worker.Jobs;

/// <summary>
/// Quartz job that summarises the day's recommendations and pushes them
/// to Telegram. Activated by Quartz's DI job factory — scoped services
/// (AppDbContext, ITelegramBotClient) are guaranteed a fresh lifetime.
/// </summary>
[DisallowConcurrentExecution]
public class DailyDigestJob : IJob
{
    public const string JobKey = "daily-digest-job";

    private readonly AppDbContext _db;
    private readonly ITelegramBotClient _telegram;
    private readonly ILogger<DailyDigestJob> _logger;

    public DailyDigestJob(
        AppDbContext db,
        ITelegramBotClient telegram,
        ILogger<DailyDigestJob> logger)
    {
        _db = db;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Daily digest job started at {Time}", DateTime.UtcNow);

        var today = DateTime.UtcNow.Date;

        // AsNoTracking: this is a read-only projection.
        // Where+OrderBy+Take: avoid scanning the whole table.
        var recs = await _db.Recommendations
            .AsNoTracking()
            .Where(r => r.CreatedAt >= today)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        if (recs.Count == 0)
        {
            await _telegram.SendAsync(
                $"📭 Tidak ada rekomendasi hari ini ({today:yyyy-MM-dd}).");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 *Daily Digest — {today:yyyy-MM-dd}*");
        sb.AppendLine($"Total sinyal: {recs.Count}");
        sb.AppendLine();

        foreach (var r in recs)
        {
            sb.AppendLine($"• *{r.TickerSymbol}* — _{r.SignalLabel}_");
            sb.AppendLine($"   {r.Rationale}");
        }

        await _telegram.SendAsync(sb.ToString());
        _logger.LogInformation("Daily digest sent ({Count} recommendations).", recs.Count);
    }
}