namespace TradingRecommender.Application.Interfaces;

/// <summary>
/// Contract for GOAPI data-fetching client.
/// </summary>
public interface IGoApiClient
{
    /// <summary>
    /// Fetches foreign-buy data for a list of symbols from the market.
    /// </summary>
    Task<IList<ForeignBuyRecord>> GetForeignBuysAsync(
        string market = "IDX",
        CancellationToken ct = default);

    /// <summary>
    /// Fetches volume data for all stocks in the market.
    /// </summary>
    Task<MarketVolumeInfo> GetMarketVolumeAsync(
        string market = "IDX",
        CancellationToken ct = default);

    /// <summary>
    /// Fetches real-time price info for a single symbol.
    /// </summary>
    Task<PriceInfo?> GetPriceAsync(string symbol, CancellationToken ct = default);
}

/// <summary>
/// Record of a single foreign-buy transaction.
/// </summary>
public record ForeignBuyRecord(
    string TickerSymbol,
    double Price,
    int Volume,
    double NetBuyValue,
    double CumulativeBuy,
    double CumulativeSell);

/// <summary>
/// Aggregate market volume info.
/// </summary>
public record MarketVolumeInfo(
    long MarketIndex,
    long IndexValue,
    long MarketValue,
    long MarketCapitalization);

/// <summary>
/// Real-time price info for a symbol.
/// </summary>
public record PriceInfo(
    string TickerSymbol,
    double LastPrice,
    long Volume,
    double Value,
    int Frequency);