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
