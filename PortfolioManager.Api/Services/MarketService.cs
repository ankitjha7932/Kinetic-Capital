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
        var cached = _cache.Get<List<MarketMomentum>>(TickerCacheKey);
        if (cached != null && cached.Any())
            return cached;

        await RefreshTickerBatchAsync();
        return _cache.Get<List<MarketMomentum>>(TickerCacheKey) ?? new List<MarketMomentum>();
    }

    public async Task RefreshTickerBatchAsync()
    {
        var results = new List<MarketMomentum>();

        // FIX 1: Process ticker symbols sequentially with a small delay instead of
        // fire-and-forget. The ticker only has 10 symbols, so sequential is fine.
        // Firing 10 Yahoo Finance chart requests simultaneously from Render's shared IP
        // gets rate-limited, causing all of them to fail and returning an empty ticker.
        foreach (var symbol in _tickerSymbols)
        {
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(symbol, "5d");

                if (history?.Prices == null || history.Prices.Count < 2)
                    continue;

                var sortedPrices = history.Prices.OrderByDescending(p => p.Date).ToList();
                var latest = sortedPrices.First();
                var yesterday = sortedPrices.FirstOrDefault(p => p.Date.Date < latest.Date.Date);

                if (yesterday == null)
                    continue;

                decimal currentPrice = latest.Close;
                decimal previousClose = yesterday.Close;
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
                        0,
                        Math.Round(change, 2)
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticker] Failed for {symbol}: {ex.Message}");
            }

            // FIX 2: 150ms between each ticker symbol. Prevents Yahoo rate-limiting
            // on Render's shared IP. 10 symbols × 150ms = ~1.5s extra — acceptable.
            await Task.Delay(150);
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

    public async Task<List<MarketMomentum>> GetHighInfusionStocksAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<MarketMomentum>? cachedData))
            return cachedData!;

        // FIX 3: Reduced from 300 → 100 stocks. Scanning 300 stocks means 300 Yahoo Finance
        // calls (+ 300 more for 5d history). That's 600 HTTP requests on one Render request —
        // it will always 502. 100 stocks with the semaphore below is safe and still meaningful.
        var allFundamentals = await _fundamentalCollection
            .Find(_ => true)
            .Project(f => new { f.Symbol, f.MarketCap })
            .Limit(100)
            .ToListAsync();

        var results = new List<MarketMomentum>();

        // FIX 4: Reduced semaphore from 15 → 5. On Render free tier, 15 concurrent HTTP
        // requests saturates the outbound connection pool and causes socket exhaustion.
        // 5 concurrent is fast enough while staying within connection limits.
        var semaphore = new SemaphoreSlim(5);

        var tasks = allFundamentals.Select(async f =>
        {
            await semaphore.WaitAsync();
            try
            {
                // FIX 5: Wrapped each symbol's work in a per-symbol timeout. If Yahoo
                // hangs on one symbol, it doesn't hold the semaphore slot forever.
                await ProcessHighInfusionSymbol(f.Symbol, f.MarketCap, results)
                    .WaitAsync(TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HighInfusion] Timeout/error for {f.Symbol}: {ex.Message}");
            }
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

    // FIX 6: Extracted per-symbol logic into its own method so the timeout wrapper above
    // is clean and we can apply WaitAsync without nesting issues.
    private async Task ProcessHighInfusionSymbol(
        string symbol,
        string marketCapStr,
        List<MarketMomentum> results
    )
    {
        var intraday = await _priceService.GetHistoricalDataAsync(symbol, "1d");
        if (intraday == null || !intraday.Prices.Any())
            return;

        decimal totalValueTradedRaw = intraday.Prices.Sum(p => p.Close * p.Volume);
        decimal valueTradedCr = totalValueTradedRaw / 10000000m;

        var latest = intraday.Prices.Last();
        decimal currentPrice = latest.Close;
        long totalVolume = intraday.Prices.Sum(p => p.Volume);

        decimal marketCapCr = 0;
        if (!string.IsNullOrEmpty(marketCapStr) && marketCapStr != "N/A")
        {
            string cleanMcap = marketCapStr.Replace(" Cr", "").Replace(",", "").Trim();
            decimal.TryParse(cleanMcap, out marketCapCr);
        }

        if (marketCapCr <= 10)
            return;

        decimal handoverRatio = (valueTradedCr / marketCapCr) * 100;

        var dailyHistory = await _priceService.GetHistoricalDataAsync(symbol, "5d");
        decimal prevClose =
            dailyHistory?.Prices?.Count > 1
                ? dailyHistory.Prices.OrderByDescending(p => p.Date).Skip(1).First().Close
                : currentPrice;

        lock (results)
        {
            results.Add(
                new MarketMomentum(
                    symbol.Replace(".NS", "").Replace(".BO", ""),
                    Math.Round(currentPrice, 2),
                    totalVolume,
                    Math.Round(valueTradedCr, 2),
                    marketCapCr,
                    Math.Round(handoverRatio, 4),
                    Math.Round(
                        ((currentPrice - prevClose) / (prevClose != 0 ? prevClose : 1)) * 100,
                        2
                    )
                )
            );
        }
    }
}
