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

        var allFundamentals = await _fundamentalCollection
            .Find(_ => true)
            .Project(f => new { f.Symbol, f.MarketCap })
            .ToListAsync();

        var results = new List<MarketMomentum>();
        var semaphore = new SemaphoreSlim(15);

        var tasks = allFundamentals.Select(async f =>
        {
            await semaphore.WaitAsync();
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(f.Symbol, "5d");
                if (history == null || !history.Prices.Any())
                    return;

                var latest = history.Prices.Last();
                decimal price = (decimal)latest.Close;
                long volume = latest.Volume;

                decimal marketCapCr = 0;
                if (!string.IsNullOrEmpty(f.MarketCap) && f.MarketCap != "N/A")
                    decimal.TryParse(f.MarketCap.Replace(" Cr", "").Trim(), out marketCapCr);

                if (marketCapCr > 10)
                {
                    decimal valueTradedCr = (price * volume) / 10000000m;
                    decimal handoverRatio = (valueTradedCr / marketCapCr) * 100;

                    if (handoverRatio >= 5.0m)
                    {
                        decimal prevClose =
                            history.Prices.Count > 1 ? (decimal)history.Prices[^2].Close : price;
                        lock (results)
                        {
                            results.Add(
                                new MarketMomentum(
                                    f.Symbol.Replace(".NS", ""),
                                    Math.Round(price, 2),
                                    volume,
                                    Math.Round(valueTradedCr, 2),
                                    marketCapCr,
                                    Math.Round(handoverRatio, 2),
                                    Math.Round(((price - prevClose) / prevClose) * 100, 2)
                                )
                            );
                        }
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var finalResult = results.OrderByDescending(r => r.HandoverRatio).Take(30).ToList();
        _cache.Set(CacheKey, finalResult, TimeSpan.FromHours(4));

        return finalResult;
    }
}
