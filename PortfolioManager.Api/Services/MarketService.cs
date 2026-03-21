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
        // 1. Check Cache (Keep for 4 hours as requested)
        if (_cache.TryGetValue(CacheKey, out List<MarketMomentum>? cachedData))
            return cachedData!;

        var allFundamentals = await _fundamentalCollection
            .Find(_ => true)
            .Project(f => new { f.Symbol, f.MarketCap })
            .ToListAsync();

        var results = new List<MarketMomentum>();
        var semaphore = new SemaphoreSlim(15); // Process 15 stocks at a time to avoid Yahoo blocking

        var tasks = allFundamentals.Select(async f =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Fetch 1 month of Daily data to get 'Today's' total daily volume
                var history = await _priceService.GetHistoricalDataAsync(f.Symbol, "1mo");
                if (history == null || history.Prices.Count < 2)
                    return;

                var latest = history.Prices.Last();
                decimal price = latest.Close;
                long totalDailyVolume = latest.Volume;

                // Robust parsing for Market Cap Cr
                decimal marketCapCr = 0;
                if (!string.IsNullOrEmpty(f.MarketCap) && f.MarketCap != "N/A")
                {
                    string cleanMcap = f.MarketCap.Replace(" Cr", "").Replace(",", "").Trim();
                    decimal.TryParse(cleanMcap, out marketCapCr);
                }

                if (marketCapCr > 1) // Ignore penny shells
                {
                    // Value Traded today in Crores
                    decimal valueTradedCr = (price * totalDailyVolume) / 10000000m;

                    // HANDOVER RATIO: What % of the company changed hands today?
                    decimal handoverRatio = (valueTradedCr / marketCapCr) * 100;

                    decimal prevClose = history.Prices[^2].Close;

                    lock (results)
                    {
                        results.Add(
                            new MarketMomentum(
                                f.Symbol.Replace(".NS", ""),
                                Math.Round(price, 2),
                                totalDailyVolume,
                                Math.Round(valueTradedCr, 2),
                                marketCapCr,
                                Math.Round(handoverRatio, 4), // High precision for sorting
                                Math.Round(((price - prevClose) / prevClose) * 100, 2)
                            )
                        );
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // SORT BY MAXIMUM VOLUME VS MARKET CAP (Handover Ratio)
        var finalResult = results
            .OrderByDescending(r => r.HandoverRatio)
            .Take(50) // Top 50 "Infusion" stocks
            .ToList();

        // Cache for 4 hours (Effect of volume takes time to show)
        _cache.Set(CacheKey, finalResult, TimeSpan.FromHours(4));

        return finalResult;
    }
}
