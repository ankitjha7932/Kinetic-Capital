using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class MarketService
{
    private readonly StockPriceService _priceService;
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
    private readonly IMongoCollection<IndexMapping> _indexCollection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MarketService> _logger;
    private readonly HttpClient _httpClient;

    // TTL-aware cache: stores data + timestamp together
    private static readonly ConcurrentDictionary<
        string,
        (MarketMomentum Data, DateTime CachedAt)
    > _stockCache = new();
    private const int StockCacheTtlMinutes = 5;

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

    // Single canonical normalizer — used everywhere for cache keys
    private static string Normalize(string symbol) =>
        symbol.Replace(".NS", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

    public MarketService(
        StockPriceService priceService,
        IMongoClient mongoClient,
        IMemoryCache cache,
        ILogger<MarketService> logger,
        HttpClient httpClient
    )
    {
        _priceService = priceService;
        var database = mongoClient.GetDatabase("KineticCapitalDB");
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
        _indexCollection = database.GetCollection<IndexMapping>("IndexConstituents");
        _cache = cache;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<IndexMoversResponse> GetIndexMoversAsync(string indexName)
    {
        var mapping = await _indexCollection
            .Find(x => x.IndexName == indexName.ToUpperInvariant())
            .FirstOrDefaultAsync();

        if (mapping == null || !mapping.Symbols.Any())
            return IndexMoversResponse.NotFound(indexName);

        var results = new List<MarketMomentum>();
        var missing = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var sym in mapping.Symbols)
        {
            var key = Normalize(sym);
            if (
                _stockCache.TryGetValue(key, out var cached)
                && (now - cached.CachedAt).TotalMinutes < StockCacheTtlMinutes
            )
            {
                results.Add(cached.Data);
            }
            else
            {
                missing.Add(sym);
            }
        }

        if (missing.Any())
        {
            foreach (var batch in missing.Chunk(10))
            {
                var freshData = await FetchSparkDataAsync(batch.ToList());
                foreach (var item in freshData)
                {
                    _stockCache[Normalize(item.Symbol)] = (item, DateTime.UtcNow);
                    results.Add(item);
                }
                await Task.Delay(200);
            }
        }

        return new IndexMoversResponse
        {
            Index = indexName.ToUpperInvariant(),
            TotalStocks = results.Count,
            Gainers1D = results
                .Where(x => x.ChangePercent > 0)
                .OrderByDescending(x => x.ChangePercent)
                .ToList(),
            Losers1D = results
                .Where(x => x.ChangePercent < 0)
                .OrderBy(x => x.ChangePercent)
                .ToList(),
            VolumeShockers = results
                .Where(x => x.MarketCapCr > 0)
                .OrderByDescending(x => x.Handover)
                .ToList(),
            TopReturnsWeekly = results
                .Where(x => x.Return1W > 0)
                .OrderByDescending(x => x.Return1W)
                .ToList(),
            TopReturnsMonthly = results
                .Where(x => x.Return1M > 0)
                .OrderByDescending(x => x.Return1M)
                .ToList(),
            LastUpdated = DateTime.UtcNow,
        };
    }

    private async Task<List<MarketMomentum>> FetchSparkDataAsync(List<string> symbols)
    {
        var list = new List<MarketMomentum>();
        if (symbols == null || symbols.Count == 0)
            return list;
        var joinedSymbols = string.Join(",", symbols.Select(s => Uri.EscapeDataString(s)));
        var url =
            $"https://query1.finance.yahoo.com/v7/finance/spark?symbols={joinedSymbols}&range=1mo&interval=1d";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Spark API returned {Status} for symbols: {Symbols}",
                    response.StatusCode,
                    joinedSymbols
                );
                return list;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync()
            );

            // Defensive JSON parsing — API shape changes won't crash the service
            if (
                !doc.RootElement.TryGetProperty("spark", out var spark)
                || !spark.TryGetProperty("result", out var sparkRoot)
            )
            {
                _logger.LogWarning("Unexpected Spark API response shape.");
                return list;
            }

            // Use strongly-typed MongoDB filter for efficiency
            var dbData = await _fundamentalCollection
                .Find(Builders<StockFundamental>.Filter.In(f => f.Symbol, symbols))
                .Project(f => new
                {
                    f.Symbol,
                    f.CompanyName,
                    f.MarketCap,
                })
                .ToListAsync();

            var nameLookup = dbData.ToDictionary(x => x.Symbol, x => x.CompanyName);
            var mcapLookup = dbData.ToDictionary(x => x.Symbol, x => x.MarketCap);

            foreach (var entry in sparkRoot.EnumerateArray())
            {
                if (
                    !entry.TryGetProperty("response", out var respArr)
                    || respArr.GetArrayLength() == 0
                )
                    continue;

                var responseObj = respArr[0];

                if (
                    !responseObj.TryGetProperty("meta", out var meta)
                    || !responseObj.TryGetProperty("indicators", out var indicatorsRoot)
                )
                    continue;

                var quoteArr = indicatorsRoot.GetProperty("quote");
                if (quoteArr.GetArrayLength() == 0)
                    continue;

                var indicators = quoteArr[0];

                if (!indicators.TryGetProperty("close", out var closeElement))
                    continue;

                var closePrices = closeElement
                    .EnumerateArray()
                    .Where(x => x.ValueKind != JsonValueKind.Null)
                    .Select(x => Math.Round(x.GetDecimal(), 2))
                    .ToList();

                if (closePrices.Count == 0)
                    continue;

                var rawSym = meta.GetProperty("symbol").GetString() ?? string.Empty;
                var displaySym = Normalize(rawSym);

                // Last 10 data points for sparkline
                var sparklineData = closePrices.TakeLast(10).ToList();

                // --- Price calculations ---
                var currentPrice = meta.TryGetProperty("regularMarketPrice", out var p)
                    ? p.GetDecimal()
                    : closePrices.Last();

                var previousClose = closePrices.Count >= 2 ? closePrices[^2] : currentPrice;

                // Safe division — guard all denominators
                var changePercent =
                    previousClose != 0 ? ((currentPrice - previousClose) / previousClose) * 100 : 0;

                var price1W = closePrices.Count >= 6 ? closePrices[^6] : closePrices[0];
                var return1W = price1W != 0 ? ((currentPrice - price1W) / price1W) * 100 : 0;

                var price1M = closePrices[0];
                var return1M = price1M != 0 ? ((currentPrice - price1M) / price1M) * 100 : 0;

                // --- Market cap & volume ---
                var volume = meta.TryGetProperty("regularMarketVolume", out var v)
                    ? v.GetInt64()
                    : 0L;

                var mcapCr = meta.TryGetProperty("marketCap", out var m)
                    ? m.GetDecimal() / 10_000_000m
                    : 0m;

                // Fallback to DB market cap if Yahoo didn't return one
                if (mcapCr == 0 && mcapLookup.TryGetValue(rawSym, out var dbCapStr))
                    mcapCr = ParseMarketCap(dbCapStr);

                var valTradedCr = (currentPrice * volume) / 10_000_000m;
                var handover = mcapCr > 0 ? (valTradedCr / mcapCr) * 100 : 0;

                list.Add(
                    new MarketMomentum(
                        displaySym,
                        nameLookup.GetValueOrDefault(rawSym, displaySym),
                        Math.Round(currentPrice, 2),
                        volume,
                        Math.Round(valTradedCr, 2),
                        Math.Round(mcapCr, 2),
                        Math.Round(handover, 4),
                        Math.Round(changePercent, 2),
                        Math.Round(currentPrice - previousClose, 2),
                        Math.Round(previousClose, 2),
                        Math.Round(return1W, 2),
                        Math.Round(return1M, 2),
                        sparklineData
                    )
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spark fetch failed for symbols: {Symbols}", joinedSymbols);
        }

        return list;
    }

    public async Task RefreshTickerBatchAsync()
    {
        var results = new List<MarketMomentum>();

        foreach (var symbol in _tickerSymbols)
        {
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(symbol, "5d");

                if (history?.Prices == null || history.Prices.Count < 2)
                    continue;

                var sorted = history.Prices.OrderByDescending(p => p.Date).ToList();

                var latest = sorted[0];
                var previous = sorted.FirstOrDefault(p => p.Date.Date < latest.Date.Date);

                if (previous == null)
                    continue;

                decimal currentPrice = latest.Close;
                decimal previousClose = previous.Close;

                decimal dayChange = currentPrice - previousClose;

                decimal changePercent = previousClose != 0 ? (dayChange / previousClose) * 100 : 0;

                // 🔥 Weekly return (approx from 5d data)
                decimal return1W =
                    sorted.Count >= 5
                        ? ((currentPrice - sorted[^5].Close) / sorted[^5].Close) * 100
                        : 0;

                // 🔥 Monthly return not available → keep 0
                decimal return1M = 0;

                // 🔥 Sparkline (last 5 closes)
                var sparkline = sorted
                    .Take(5)
                    .Select(p => Math.Round(p.Close, 2))
                    .Reverse()
                    .ToList();

                results.Add(
                    new MarketMomentum(
                        symbol.Replace(".NS", "").Replace(".BO", ""), // Symbol
                        symbol.Replace(".NS", ""), // CompanyName (fallback)
                        Math.Round(currentPrice, 2), // Price
                        latest.Volume, // Volume
                        0, // ValueTradedCr
                        0, // MarketCapCr
                        0, // Handover
                        Math.Round(changePercent, 2), // ChangePercent
                        Math.Round(dayChange, 2), // DayChange
                        Math.Round(previousClose, 2), // PreviousClose
                        Math.Round(return1W, 2), // Return1W
                        Math.Round(return1M, 2), // Return1M
                        sparkline // Sparkline
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticker] Failed for {symbol}: {ex.Message}");
            }

            await Task.Delay(150); // 🔥 keep this
        }

        if (results.Any())
        {
            var finalData = results.OrderBy(r => r.Symbol).ToList();

            _cache.Set(TickerCacheKey, finalData, TimeSpan.FromMinutes(30));

            Console.WriteLine($"[Ticker] Updated {finalData.Count} stocks");
        }
        else
        {
            Console.WriteLine("[Ticker] No data fetched");
        }
    }

    private static decimal ParseMarketCap(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "N/A")
            return 0;

        var clean = s.Replace("Cr", "", StringComparison.OrdinalIgnoreCase).Replace(",", "").Trim();

        return decimal.TryParse(clean, out var val) ? val : 0;
    }

    public async Task ImportIndexSymbolsAsync(string indexName, string csvContent)
    {
        var sanitizedSymbols = new HashSet<string>();
        var lines = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var cols = line.Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim().Replace("\"", ""))
                .ToList();

            if (cols.Count < 3)
                continue;

            var rawSymbol = cols[2].ToUpperInvariant();
            if (rawSymbol == "SYMBOL" || string.IsNullOrEmpty(rawSymbol))
                continue;

            sanitizedSymbols.Add(
                rawSymbol.EndsWith(".NS", StringComparison.OrdinalIgnoreCase)
                    ? rawSymbol
                    : $"{rawSymbol}.NS"
            );
        }

        var update = Builders<IndexMapping>
            .Update.Set(x => x.Symbols, sanitizedSymbols.ToList())
            .Set(x => x.LastUpdated, DateTime.UtcNow);

        await _indexCollection.UpdateOneAsync(
            Builders<IndexMapping>.Filter.Eq(x => x.IndexName, indexName.ToUpperInvariant()),
            update,
            new UpdateOptions { IsUpsert = true }
        );
    }

    public async Task<List<MarketMomentum>> GetTickerDataAsync()
    {
        if (
            _cache.TryGetValue(TickerCacheKey, out List<MarketMomentum>? cached)
            && cached != null
            && cached.Any()
        )
        {
            return cached;
        }

        await RefreshTickerBatchAsync();

        return _cache.Get<List<MarketMomentum>>(TickerCacheKey) ?? new List<MarketMomentum>();
    }
}
