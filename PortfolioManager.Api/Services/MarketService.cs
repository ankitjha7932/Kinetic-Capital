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

    public async Task<List<MarketMomentum>> GetTickerDataAsync()
    {
        return _cache.Get<List<MarketMomentum>>(TickerCacheKey) ?? new List<MarketMomentum>();
    }

    /// <summary>
    /// Refreshes the ticker data by comparing current price to YESTERDAY'S close.
    /// </summary>
    public async Task RefreshTickerBatchAsync()
    {
        var results = new List<MarketMomentum>();
        foreach (var symbol in _tickerSymbols)
        {
            try
            {
                // 1. Fetch 5 days of data to guarantee we have yesterday's full candle
                var history = await _priceService.GetHistoricalDataAsync(symbol, "5d");

                if (history?.Prices == null || history.Prices.Count < 2)
                    continue;

                // 2. Sort descending by date to get the most recent sessions
                var sortedPrices = history.Prices.OrderByDescending(p => p.Date).ToList();

                // Latest is Today's current/live price
                var latest = sortedPrices.First();

                // Yesterday is the first price point with a different date than today
                var yesterday = sortedPrices.FirstOrDefault(p => p.Date.Date < latest.Date.Date);

                if (yesterday == null)
                    continue;

                decimal currentPrice = latest.Close;
                decimal previousClose = yesterday.Close;

                // 3. Formula: ((Current - PreviousClose) / PreviousClose) * 100
                decimal change =
                    ((currentPrice - previousClose) / (previousClose != 0 ? previousClose : 1))
                    * 100;

                results.Add(
                    new MarketMomentum(
                        symbol.Replace(".NS", ""),
                        Math.Round(currentPrice, 2),
                        latest.Volume,
                        0,
                        0,
                        0, // Ticker specific fields
                        Math.Round(change, 2)
                    )
                );
            }
            catch
            { /* Handle/Log error */
            }
        }

        if (results.Any())
        {
            _cache.Set(
                TickerCacheKey,
                results.OrderBy(r => r.Symbol).ToList(),
                TimeSpan.FromMinutes(30)
            );
        }
    }

    /// <summary>
    /// Scans for high infusion (Institutional Handover) with corrected daily % change.
    /// </summary>
    public async Task<List<MarketMomentum>> GetHighInfusionStocksAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<MarketMomentum>? cachedData))
            return cachedData!;

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

                    // Fetch 5d daily history to get Yesterday's Close
                    var dailyHistory = await _priceService.GetHistoricalDataAsync(f.Symbol, "5d");
                    decimal prevClose =
                        dailyHistory?.Prices?.Count > 1
                            ? dailyHistory
                                .Prices.OrderByDescending(p => p.Date)
                                .Skip(1)
                                .First()
                                .Close
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
