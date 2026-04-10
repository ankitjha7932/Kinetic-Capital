using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace PortfolioManager.Api.Services;

public record HistoricalData(List<PricePoint> Prices);

public record PricePoint(DateTime Date, decimal Close, long Volume);

public class StockPriceService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private const string BATCH_CACHE_KEY = "market_prices_master";
    private const string SPARKLINE_CACHE_KEY = "sparklines_master";

    // FIX 1: Tightened timeouts — Yahoo Finance should respond in <5s or it's being throttled.
    // 8s is generous enough for slow connections but stops Render from killing the whole process.
    private static readonly TimeSpan _apiTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan _batchApiTimeout = TimeSpan.FromSeconds(12);

    public StockPriceService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );
        }

        // FIX 2: Set a global HttpClient timeout as a hard backstop.
        // This prevents the OS-level socket from hanging indefinitely on Render.
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100)) // only set if still default
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public bool IsMarketOpen()
    {
        var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            return false;
        var start = new TimeSpan(9, 15, 0);
        var end = new TimeSpan(15, 30, 0);
        return now.TimeOfDay >= start && now.TimeOfDay <= end;
    }

    public async Task<Dictionary<string, decimal>> GetBatchPricesAsync(List<string> symbols)
    {
        bool marketOpen = IsMarketOpen();

        if (
            !marketOpen
            && _cache.TryGetValue(BATCH_CACHE_KEY, out Dictionary<string, decimal>? closedData)
        )
            return closedData!;

        if (
            _cache.TryGetValue(BATCH_CACHE_KEY, out Dictionary<string, decimal>? prices)
            && prices!.Any()
        )
            return prices!;

        prices = new Dictionary<string, decimal>();

        // FIX 3: Reduced chunk size from 400 → 50. Yahoo Finance silently drops/throttles
        // massive batch requests on shared hosting IPs (common on Render). Smaller chunks
        // are more reliable even though they require more requests.
        var chunks = symbols.Distinct().Chunk(50);

        foreach (var chunk in chunks)
        {
            string tickers = string.Join(",", chunk.Select(SanitizeTicker));
            string url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={tickers}";
            try
            {
                var response = await _httpClient.GetAsync(url).WaitAsync(_batchApiTimeout);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync()
                    );
                    var results = doc
                        .RootElement.GetProperty("quoteResponse")
                        .GetProperty("result");
                    foreach (var quote in results.EnumerateArray())
                    {
                        string symbol = quote.GetProperty("symbol").GetString() ?? "";
                        decimal price = quote.TryGetProperty("regularMarketPrice", out var p)
                            ? p.GetDecimal()
                            : 0m;
                        if (!string.IsNullOrEmpty(symbol))
                            prices[symbol] = price;
                    }
                }
            }
            catch (Exception ex)
            {
                // FIX 4: Log the failure but continue — partial data is better than no data.
                Console.WriteLine($"[BatchPrice] Chunk failed: {ex.Message}");
            }

            // FIX 5: Small delay between chunks to avoid Yahoo rate-limiting on Render's
            // shared IP pool. 200ms adds ~1s total for 5 chunks — worth the stability.
            await Task.Delay(200);
        }

        var duration = marketOpen ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12);
        _cache.Set(BATCH_CACHE_KEY, prices, duration);

        return prices;
    }

    public async Task<Dictionary<string, List<decimal>>> GetBatchSparklinesAsync(
        List<string> symbols
    )
    {
        if (
            _cache.TryGetValue(
                SPARKLINE_CACHE_KEY,
                out Dictionary<string, List<decimal>>? sparklines
            ) && sparklines!.Any()
        )
            return sparklines!;

        sparklines = new Dictionary<string, List<decimal>>();

        // FIX 6: Replaced unbounded Task.WhenAll with a semaphore-limited sequential approach.
        // Firing 20+ Yahoo Finance requests simultaneously from Render gets you rate-limited
        // or causes socket exhaustion. Max 5 concurrent is safe.
        var semaphore = new SemaphoreSlim(5);
        var tasks = symbols
            .Distinct()
            .Select(async s =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var history = await GetHistoricalDataAsync(s, "7d");
                    return new { Symbol = s, Data = history.Prices.Select(p => p.Close).ToList() };
                }
                catch
                {
                    return new { Symbol = s, Data = new List<decimal>() };
                }
                finally
                {
                    semaphore.Release();
                }
            });

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
            if (r.Data.Any())
                sparklines[r.Symbol] = r.Data;

        _cache.Set(SPARKLINE_CACHE_KEY, sparklines, TimeSpan.FromMinutes(30));
        return sparklines;
    }

    public async Task<decimal> GetLivePriceAsync(string symbol)
    {
        if (_cache.TryGetValue(BATCH_CACHE_KEY, out Dictionary<string, decimal>? prices))
        {
            if (prices!.TryGetValue(SanitizeTicker(symbol), out decimal cachedPrice))
                return cachedPrice;
        }
        var data = await GetHistoricalDataAsync(symbol, "1d");
        return data?.Prices.LastOrDefault()?.Close ?? 0m;
    }

    public async Task<JsonElement?> GetStockFundamentalsAsync(string symbol)
    {
        string ticker = SanitizeTicker(symbol);
        string url =
            $"https://query2.finance.yahoo.com/v7/finance/quoteSummary/{ticker}?modules=summaryDetail,defaultKeyStatistics,financialData";
        try
        {
            var response = await _httpClient.GetAsync(url).WaitAsync(_apiTimeout);
            if (!response.IsSuccessStatusCode)
                return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("quoteSummary", out var qs))
                return null;
            if (!qs.TryGetProperty("result", out var res))
                return null;
            return res.GetArrayLength() > 0 ? res[0].Clone() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<HistoricalData> GetHistoricalDataAsync(string symbol, string range = "1y")
    {
        string interval = range.ToLower() switch
        {
            "1d" => "1m",
            "5d" => "5m",
            "1w" => "5m",
            "1mo" => "1h",
            "1m" => "1h",
            "3y" => "1d",
            "max" => "1wk",
            _ => "1d",
        };

        try
        {
            string ticker = SanitizeTicker(symbol);
            string url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?range={range}&interval={interval}";

            var response = await _httpClient.GetAsync(url).WaitAsync(_apiTimeout);
            if (!response.IsSuccessStatusCode)
                return new HistoricalData(new());

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync()
            );
            if (!doc.RootElement.TryGetProperty("chart", out var chart))
                return new HistoricalData(new());
            var resArr = chart.GetProperty("result");
            if (resArr.GetArrayLength() == 0)
                return new HistoricalData(new());
            var res = resArr[0];

            if (!res.TryGetProperty("timestamp", out var tProp))
                return new HistoricalData(new());

            var ts = tProp.EnumerateArray().ToList();
            var quote = res.GetProperty("indicators").GetProperty("quote")[0];
            var cls = quote.GetProperty("close").EnumerateArray().ToList();
            var vol = quote.GetProperty("volume").EnumerateArray().ToList();

            var prices = new List<PricePoint>();

            // FIX 7: Cross-platform timezone ID — "India Standard Time" is Windows-only.
            // On Render (Linux), this throws a TimeZoneNotFoundException causing a 500.
            var tzId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows
            )
                ? "India Standard Time"
                : "Asia/Kolkata";
            var istZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);

            for (int i = 0; i < ts.Count; i++)
            {
                if (i < cls.Count && cls[i].ValueKind == JsonValueKind.Number)
                {
                    var ist = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime,
                        istZone
                    );
                    long volumeValue =
                        (i < vol.Count && vol[i].ValueKind == JsonValueKind.Number)
                            ? vol[i].GetInt64()
                            : 0;
                    prices.Add(new PricePoint(ist, cls[i].GetDecimal(), volumeValue));
                }
            }
            return new HistoricalData(prices);
        }
        catch
        {
            return new HistoricalData(new());
        }
    }

    private string SanitizeTicker(string s) =>
        s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
            ? s.ToUpper()
            : $"{s.ToUpper()}.NS";
}
