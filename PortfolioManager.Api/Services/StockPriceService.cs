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

    private static readonly TimeSpan _apiTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan _batchApiTimeout = TimeSpan.FromSeconds(12);

    public StockPriceService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public bool IsMarketOpen()
    {
        var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
        var istZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            return false;
        return now.TimeOfDay >= new TimeSpan(9, 15, 0) && now.TimeOfDay <= new TimeSpan(15, 30, 0);
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
        foreach (var chunk in symbols.Distinct().Chunk(50))
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
                Console.WriteLine($"[BatchPrice] Chunk failed: {ex.Message}");
            }
            await Task.Delay(200);
        }

        _cache.Set(
            BATCH_CACHE_KEY,
            prices,
            marketOpen ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12)
        );
        return prices;
    }

    public async Task<Dictionary<string, List<decimal>>> GetBatchSparklinesAsync(
        List<string> symbols
    )
    {
        var sanitized = symbols.Select(SanitizeTicker).Distinct().ToList();

        if (
            _cache.TryGetValue(
                SPARKLINE_CACHE_KEY,
                out Dictionary<string, List<decimal>>? sparklines
            ) && sparklines!.Any()
        )
        {
            // If every requested symbol is already cached, return as-is
            if (sanitized.All(s => sparklines!.ContainsKey(s)))
                return sparklines!;
            // New symbol added — bust cache so it gets included
            _cache.Remove(SPARKLINE_CACHE_KEY);
        }

        sparklines = new Dictionary<string, List<decimal>>();
        var semaphore = new SemaphoreSlim(5);
        var tasks = sanitized.Select(async s =>
        {
            await semaphore.WaitAsync();
            try
            {
                // FIX: "7d" is not a valid Yahoo Finance range. Valid values: 1d,5d,1mo,3mo,6mo,1y,2y,5y,10y,ytd,max
                var history = await GetHistoricalDataAsync(s, "5d");
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
            if (
                prices!.TryGetValue(SanitizeTicker(symbol), out decimal cachedPrice)
                && cachedPrice > 0
            )
                return cachedPrice;

        try
        {
            var encoded = Uri.EscapeDataString(SanitizeTicker(symbol));
            var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={encoded}";
            var response = await _httpClient.GetAsync(url).WaitAsync(_apiTimeout);
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync()
                );
                var result = doc.RootElement.GetProperty("quoteResponse").GetProperty("result");
                if (result.GetArrayLength() > 0)
                {
                    var q = result[0];
                    if (
                        q.TryGetProperty("regularMarketPrice", out var p)
                        && p.ValueKind == JsonValueKind.Number
                    )
                    {
                        decimal price = p.GetDecimal();
                        if (price > 0)
                            return price;
                    }
                }
            }
        }
        catch { }

        var data = await GetHistoricalDataAsync(symbol, "5d");
        return data?.Prices.LastOrDefault(p => p.Close > 0)?.Close ?? 0m;
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
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
            var q = res.GetProperty("indicators").GetProperty("quote")[0];
            var cls = q.GetProperty("close").EnumerateArray().ToList();
            var vol = q.GetProperty("volume").EnumerateArray().ToList();

            var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
            var istZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var prices = new List<PricePoint>();

            for (int i = 0; i < ts.Count; i++)
            {
                if (i < cls.Count && cls[i].ValueKind == JsonValueKind.Number)
                {
                    var ist = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime,
                        istZone
                    );
                    long v =
                        (i < vol.Count && vol[i].ValueKind == JsonValueKind.Number)
                            ? vol[i].GetInt64()
                            : 0;
                    prices.Add(new PricePoint(ist, cls[i].GetDecimal(), v));
                }
            }
            return new HistoricalData(prices);
        }
        catch
        {
            return new HistoricalData(new());
        }
    }

    public void InvalidateSparklineCache() => _cache.Remove(SPARKLINE_CACHE_KEY);

    private string SanitizeTicker(string s) =>
        s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
            ? s.ToUpper()
            : $"{s.ToUpper()}.NS";
}
