using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class MarketService
{
    private readonly StockPriceService _priceService;
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "MarketVolumeInfusion";

    // 1. ADDED FOR TICKER
    public const string TickerCacheKey = "MarketTickerData";
    private readonly string[] _tickerSymbols =
    {
        "BAJFINANCE.NS",
        "BHARTIARTL.NS",
        "HDFCBANK.NS",
        "HINDUNILVR.NS",
        "INDIGO.NS",
        "ITC.NS",
        "MARUTI.NS",
        "RELIANCE.NS",
        "SBIN.NS",
        "TCS.NS",
    };

    public MarketService(
        StockPriceService priceService,
        IMongoDatabase database,
        IMemoryCache cache
    )
    {
        _priceService = priceService;
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
        _cache = cache;
    }

    // 2. ADDED FOR TICKER: Getter for the Controller
    public async Task<List<MarketMomentum>> GetTickerDataAsync()
    {
        return _cache.Get<List<MarketMomentum>>(TickerCacheKey) ?? new List<MarketMomentum>();
    }

    // 3. ADDED FOR TICKER: Refresher for the Background Worker
    public async Task RefreshTickerBatchAsync()
    {
        var results = new List<MarketMomentum>();
        foreach (var symbol in _tickerSymbols)
        {
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(symbol, "5d");
                if (history?.Prices?.Count < 2)
                    continue;

                var latest = history.Prices.Last();
                var prev = history.Prices[history.Prices.Count - 2];

                decimal price = latest.Close;
                decimal change = ((price - prev.Close) / (prev.Close != 0 ? prev.Close : 1)) * 100;

                results.Add(
                    new MarketMomentum(
                        symbol.Replace(".NS", ""),
                        Math.Round(price, 2),
                        latest.Volume,
                        0,
                        0,
                        0, // Unused fields for ticker
                        Math.Round(change, 2)
                    )
                );
            }
            catch { }
        }

        if (results.Any())
            _cache.Set(
                TickerCacheKey,
                results.OrderBy(r => r.Symbol).ToList(),
                TimeSpan.FromHours(24)
            );
    }

    public async Task<List<MarketMomentum>> GetHighInfusionStocksAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<MarketMomentum>? cachedData))
            return cachedData!;

        // Fetch symbols to scan
        var allFundamentals = await _fundamentalCollection
            .Find(_ => true)
            .Project(f => new { f.Symbol, f.MarketCap })
            .Limit(300)
            .ToListAsync();

        var results = new List<MarketMomentum>();
        var semaphore = new SemaphoreSlim(15);

        var tasks = allFundamentals.Select(async f =>
        {
            await semaphore.WaitAsync();
            try
            {
                var intraday = await _priceService.GetHistoricalDataAsync(f.Symbol, "1d");
                if (intraday == null || !intraday.Prices.Any())
                    return;

                decimal totalValueTradedRaw = intraday.Prices.Sum(p => p.Close * p.Volume);
                decimal valueTradedCr = totalValueTradedRaw / 10000000m;

                var latest = intraday.Prices.Last();
                decimal currentPrice = latest.Close;
                long totalVolume = intraday.Prices.Sum(p => p.Volume);

                decimal marketCapCr = 0;
                if (!string.IsNullOrEmpty(f.MarketCap) && f.MarketCap != "N/A")
                {
                    string cleanMcap = f.MarketCap.Replace(" Cr", "").Replace(",", "").Trim();
                    decimal.TryParse(cleanMcap, out marketCapCr);
                }

                if (marketCapCr > 10)
                {
                    decimal handoverRatio = (valueTradedCr / marketCapCr) * 100;

                    // Fetch 5d to get a simple prev close for the prcent change showing
                    var history = await _priceService.GetHistoricalDataAsync(f.Symbol, "5d");
                    decimal prevClose =
                        history?.Prices?.Count > 1
                            ? history.Prices[history.Prices.Count - 2].Close
                            : currentPrice;

                    lock (results)
                    {
                        results.Add(
                            new MarketMomentum(
                                f.Symbol.Replace(".NS", "").Replace(".BO", ""),
                                Math.Round(currentPrice, 2),
                                totalVolume,
                                Math.Round(valueTradedCr, 2),
                                marketCapCr,
                                Math.Round(handoverRatio, 4),
                                Math.Round(
                                    ((currentPrice - prevClose) / (prevClose != 0 ? prevClose : 1))
                                        * 100,
                                    2
                                )
                            )
                        );
                    }
                }
            }
            catch { }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var finalResult = results.OrderByDescending(r => r.HandoverRatio).Take(30).ToList();

        if (finalResult.Any())
            _cache.Set(CacheKey, finalResult, TimeSpan.FromHours(1));

        return finalResult;
    }
}
