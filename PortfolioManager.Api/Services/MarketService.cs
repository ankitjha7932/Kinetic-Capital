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

        // ── Market calendar status ────────────────────────────────────────────
        var mktStatus = MarketCalendar.GetCurrentStatus();

        // ── HOLIDAY / WEEKEND FIX ─────────────────────────────────────────────
        // When the market is closed (holiday or weekend), Yahoo's spark endpoint
        // returns the same close price for the last two entries, so
        // (currentPrice - previousClose) == 0 → changePercent = 0%.
        //
        // Instead we fetch the live quote for every stock that has changePercent == 0
        // and the market is closed. Yahoo's /v7/finance/quote endpoint always returns
        // regularMarketChangePercent relative to the *actual* previous session close,
        // which is exactly what we want to display.
        //
        // For performance we do a single batch quote call for all zero-change stocks.
        if (!mktStatus.IsOpen)
        {
            var zeroStocks = results.Where(r => r.ChangePercent == 0m).ToList();
            if (zeroStocks.Any())
            {
                var corrected = await FetchLiveBatchQuotesAsync(
                    zeroStocks.Select(s => s.Symbol + ".NS").ToList()
                );
                for (int i = 0; i < results.Count; i++)
                {
                    if (corrected.TryGetValue(Normalize(results[i].Symbol), out var q))
                    {
                        // Replace only the change-related fields; keep sparkline/mcap etc.
                        results[i] = results[i] with
                        {
                            Price = q.Price > 0 ? q.Price : results[i].Price,
                            ChangePercent = q.ChangePercent,
                            DayChange = q.DayChange,
                            PreviousClose = q.PreviousClose,
                        };
                    }
                }
            }
        }

        // ── Build response (all tabs) ─────────────────────────────────────────
        //
        // IMPORTANT: On holidays/weekends gainers/losers still work correctly
        // because we now have real changePercent from the live quote. For
        // gainers/losers we include ALL stocks with non-zero change so the
        // frontend never shows empty tabs.
        //
        // We keep the existing > 0 / < 0 filters but fall back to ALL stocks
        // ordered by absolute change when the lists would be empty (e.g. if
        // Yahoo returns 0 for everyone somehow).

        var gainers = results
            .Where(x => x.ChangePercent > 0)
            .OrderByDescending(x => x.ChangePercent)
            .ToList();

        var losers = results.Where(x => x.ChangePercent < 0).OrderBy(x => x.ChangePercent).ToList();

        // Fallback: if both are empty (shouldn't happen after the fix, but be safe)
        if (!gainers.Any() && !losers.Any() && results.Any())
        {
            gainers = results
                .OrderByDescending(x => x.ChangePercent)
                .Take(results.Count / 2)
                .ToList();
            losers = results.OrderBy(x => x.ChangePercent).Take(results.Count / 2).ToList();
        }

        return new IndexMoversResponse
        {
            Index = indexName.ToUpperInvariant(),
            TotalStocks = results.Count,
            Gainers1D = gainers,
            Losers1D = losers,
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
            // ── Include market status in every response ─────────────────────
            // The frontend uses IsLiveData to show "prev close" pills and
            // the closed-market banner. This is the single source of truth.
            MarketStatus = BuildMarketStatusPayload(mktStatus),
            LastUpdated = DateTime.UtcNow,
        };
    }

    // ── Live batch quote fetch ────────────────────────────────────────────────
    // Fetches regularMarketPrice/Change/ChangePercent/PreviousClose for a list
    // of symbols in one HTTP request. Used to fix holiday changePercent = 0.
    private async Task<
        Dictionary<
            string,
            (decimal Price, decimal ChangePercent, decimal DayChange, decimal PreviousClose)
        >
    > FetchLiveBatchQuotesAsync(List<string> nsSymbols)
    {
        var result = new Dictionary<string, (decimal, decimal, decimal, decimal)>(
            StringComparer.OrdinalIgnoreCase
        );
        if (!nsSymbols.Any())
            return result;

        // Yahoo allows up to ~50 symbols per /v7/quote call
        foreach (var batch in nsSymbols.Chunk(50))
        {
            var joined = string.Join(",", batch.Select(Uri.EscapeDataString));
            var url =
                $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={joined}&fields=regularMarketPrice,regularMarketChange,regularMarketChangePercent,regularMarketPreviousClose";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                );
                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    continue;

                using var doc = await JsonDocument.ParseAsync(
                    await resp.Content.ReadAsStreamAsync()
                );
                if (!doc.RootElement.TryGetProperty("quoteResponse", out var qr))
                    continue;
                if (!qr.TryGetProperty("result", out var arr))
                    continue;

                foreach (var q in arr.EnumerateArray())
                {
                    decimal Get(string key) =>
                        q.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number
                            ? p.GetDecimal()
                            : 0m;

                    var sym = q.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                    var key = Normalize(sym);

                    result[key] = (
                        Get("regularMarketPrice"),
                        Get("regularMarketChangePercent"),
                        Get("regularMarketChange"),
                        Get("regularMarketPreviousClose")
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[BatchQuote] Failed: {Msg}", ex.Message);
            }

            await Task.Delay(150); // rate-limit guard between batches
        }

        return result;
    }

    // ── Spark data fetch ──────────────────────────────────────────────────────
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
                _logger.LogWarning("Spark API returned {Status}", response.StatusCode);
                return list;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync()
            );

            if (
                !doc.RootElement.TryGetProperty("spark", out var spark)
                || !spark.TryGetProperty("result", out var sparkRoot)
            )
            {
                _logger.LogWarning("Unexpected Spark API response shape.");
                return list;
            }

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
                var sparklineData = closePrices.TakeLast(10).ToList();

                // ── Price from meta (most recent) ─────────────────────────────
                var currentPrice = meta.TryGetProperty("regularMarketPrice", out var p)
                    ? p.GetDecimal()
                    : closePrices.Last();
                decimal changePercent,
                    dayChange,
                    previousClose;

                if (
                    meta.TryGetProperty("regularMarketChangePercent", out var rcp)
                    && rcp.ValueKind == JsonValueKind.Number
                    && rcp.GetDecimal() != 0m
                )
                {
                    // Use Yahoo's pre-computed values — most accurate path
                    changePercent = Math.Round(rcp.GetDecimal(), 2);
                    dayChange = meta.TryGetProperty("regularMarketChange", out var rc)
                        ? Math.Round(rc.GetDecimal(), 2)
                        : 0m;
                    previousClose =
                        meta.TryGetProperty("previousClose", out var pc) ? pc.GetDecimal()
                        : meta.TryGetProperty("chartPreviousClose", out var cpc) ? cpc.GetDecimal()
                        : closePrices.Count >= 2 ? closePrices[^2]
                        : currentPrice;
                }
                else
                {
                    // Fallback: derive from close price array (open market, intraday)
                    previousClose = closePrices.Count >= 2 ? closePrices[^2] : currentPrice;
                    dayChange = currentPrice - previousClose;
                    changePercent =
                        previousClose != 0 ? Math.Round((dayChange / previousClose) * 100, 2) : 0m;
                }

                // ── Period returns ────────────────────────────────────────────
                var price1W = closePrices.Count >= 6 ? closePrices[^6] : closePrices[0];
                var return1W =
                    price1W != 0 ? Math.Round(((currentPrice - price1W) / price1W) * 100, 2) : 0m;
                var price1M = closePrices[0];
                var return1M =
                    price1M != 0 ? Math.Round(((currentPrice - price1M) / price1M) * 100, 2) : 0m;

                // ── Volume & market cap ───────────────────────────────────────
                var volume = meta.TryGetProperty("regularMarketVolume", out var v)
                    ? v.GetInt64()
                    : 0L;
                var mcapCr = meta.TryGetProperty("marketCap", out var m)
                    ? m.GetDecimal() / 10_000_000m
                    : 0m;
                if (mcapCr == 0 && mcapLookup.TryGetValue(rawSym, out var dbCapStr))
                    mcapCr = ParseMarketCap(dbCapStr);

                var valTradedCr = (currentPrice * volume) / 10_000_000m;
                var handover = mcapCr > 0 ? Math.Round((valTradedCr / mcapCr) * 100, 4) : 0m;

                list.Add(
                    new MarketMomentum(
                        displaySym,
                        nameLookup.GetValueOrDefault(rawSym, displaySym),
                        Math.Round(currentPrice, 2),
                        volume,
                        Math.Round(valTradedCr, 2),
                        Math.Round(mcapCr, 2),
                        handover,
                        changePercent,
                        dayChange,
                        Math.Round(previousClose, 2),
                        return1W,
                        return1M,
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

    // ── Market status serialisation ───────────────────────────────────────────
    private MarketStatusPayload BuildMarketStatusPayload(MarketStatus mktStatus)
    {
        // Map internal session types to user-friendly status strings
        string displayStatus = mktStatus.SessionType switch
        {
            "OPEN" => "Open",
            "HOLIDAY" => "Holiday",
            "WEEKEND" => "Closed",
            "PRE_MARKET" => "Pre-market",
            "POST_MARKET" => "Post-market",
            _ => "Closed",
        };

        // Construct the message: either the specific reason (like "Holi") or a generic status
        string displayMessage = mktStatus.IsLiveData
            ? "Market is currently trading live."
            : (mktStatus.ClosedReason ?? $"Market is currently {displayStatus.ToLower()}.");

        return new MarketStatusPayload
        {
            Status = displayStatus,
            IsLiveData = mktStatus.IsLiveData,
            Message = displayMessage,
            LastClosingDate = mktStatus.PreviousSessionDate.ToDateTime(TimeOnly.MinValue),
        };
    }

    // ── Ticker refresh ────────────────────────────────────────────────────────
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

                decimal return1W =
                    sorted.Count >= 5
                        ? ((currentPrice - sorted[^5].Close) / sorted[^5].Close) * 100
                        : 0;

                var sparkline = sorted
                    .Take(5)
                    .Select(p => Math.Round(p.Close, 2))
                    .Reverse()
                    .ToList();

                results.Add(
                    new MarketMomentum(
                        symbol.Replace(".NS", "").Replace(".BO", ""),
                        symbol.Replace(".NS", ""),
                        Math.Round(currentPrice, 2),
                        latest.Volume,
                        0,
                        0,
                        0,
                        Math.Round(changePercent, 2),
                        Math.Round(dayChange, 2),
                        Math.Round(previousClose, 2),
                        Math.Round(return1W, 2),
                        0,
                        sparkline
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Ticker] Failed for {Symbol}: {Msg}", symbol, ex.Message);
            }

            await Task.Delay(150);
        }

        if (results.Any())
        {
            _cache.Set(
                TickerCacheKey,
                results.OrderBy(r => r.Symbol).ToList(),
                TimeSpan.FromMinutes(30)
            );
            _logger.LogInformation("[Ticker] Updated {Count} stocks", results.Count);
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
            && cached?.Any() == true
        )
            return cached;

        await RefreshTickerBatchAsync();
        return _cache.Get<List<MarketMomentum>>(TickerCacheKey) ?? new List<MarketMomentum>();
    }
}
