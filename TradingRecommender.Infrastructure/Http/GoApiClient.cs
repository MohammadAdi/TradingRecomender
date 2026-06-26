using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingRecommender.Application.Interfaces;
using TradingRecommender.Domain;

namespace TradingRecommender.Infrastructure.Http;

/// <summary>
/// HTTP client for GOAPI. Uses a typed <see cref="HttpClient"/> registered
/// via HttpClientFactory and survives transient errors via the named Polly
/// resilience pipeline (<c>"goapi"</c>).
/// </summary>
public class GoApiClient : IGoApiClient
{
    public const string HttpClientName = "goapi";
    public const string ResiliencePipeline = "goapi";

    private readonly HttpClient _http;
    private readonly GoApiConfig _config;
    private readonly ILogger<GoApiClient> _logger;

    public GoApiClient(
        HttpClient http,
        IOptions<GoApiConfig> config,
        ILogger<GoApiClient> logger)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;
        _http.BaseAddress = new Uri(_config.BaseUrl);
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
    }

    public async Task<IList<ForeignBuyRecord>> GetForeignBuysAsync(
        string market = "IDX",
        CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching foreign-buy records for {Market}", market);
        var path = $"/foreign-buys?market={market}";
        var result = await _http.GetFromJsonAsync<List<ForeignBuyRecord>>(path, ct)
                     ?? new List<ForeignBuyRecord>();
        return result;
    }

    public async Task<MarketVolumeInfo> GetMarketVolumeAsync(
        string market = "IDX",
        CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching market volume for {Market}", market);
        var path = $"/volume?market={market}";
        var result = await _http.GetFromJsonAsync<MarketVolumeInfo>(path, ct)
                     ?? new MarketVolumeInfo(0, 0, 0, 0);
        return result;
    }

    public async Task<PriceInfo?> GetPriceAsync(string symbol, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching price for {Symbol}", symbol);
        var path = $"/price/{symbol}";
        try
        {
            return await _http.GetFromJsonAsync<PriceInfo>(path, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Price lookup failed for {Symbol}", symbol);
            return null;
        }
    }
}