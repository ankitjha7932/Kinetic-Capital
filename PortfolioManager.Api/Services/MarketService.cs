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
                // Range "5d" is correct for getting enough points for an average
                var history = await _priceService.GetHistoricalDataAsync(f.Symbol, "5d");
                if (history == null || history.Prices == null || history.Prices.Count < 2)
                    return;

                var latest = history.Prices.Last();
                var pricesList = history.Prices.Select(p => p.Close).ToList();
                var volumesList = history.Prices.Select(p => (decimal)p.Volume).ToList();

                decimal price = latest.Close;
                long volume = latest.Volume;

                // Robust parsing for Market Cap (handles "1,234.50 Cr" or "500")
                decimal marketCapCr = 0;
                if (!string.IsNullOrEmpty(f.MarketCap) && f.MarketCap != "N/A")
                {
                    string cleanMcap = f.MarketCap.Replace(" Cr", "").Replace(",", "").Trim();
                    decimal.TryParse(cleanMcap, out marketCapCr);
                }

                if (marketCapCr > 10)
                {
                    decimal valueTradedCr = (price * volume) / 10000000m;
                    decimal handoverRatio = (valueTradedCr / marketCapCr) * 100;

                    // Industry Standard: Compare today's volume vs 5-day average volume
                    decimal avgVolume = volumesList.Take(volumesList.Count - 1).Average();
                    decimal volumeShock = (decimal)volume / (avgVolume > 0 ? avgVolume : 1);

                    // REDUCED THRESHOLD: 0.5% of MCAP or 2x Volume Shock
                    if (handoverRatio >= 0.5m || volumeShock >= 2.0m)
                    {
                        decimal prevClose = history.Prices[^2].Close;

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
            catch (Exception ex)
            {
                // Log error for specific symbol if needed
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Sort by HandoverRatio but only take the top performers
        var finalResult = results.OrderByDescending(r => r.HandoverRatio).Take(30).ToList();
        _cache.Set(CacheKey, finalResult, TimeSpan.FromMinutes(30)); // Reduced cache time for more "Live" feel

        return finalResult;
    }
}
