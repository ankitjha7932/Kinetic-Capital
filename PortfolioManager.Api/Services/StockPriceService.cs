using System.Net;
using System.Text.Json;

namespace PortfolioManager.Api.Services;

public record HistoricalData(List<PricePoint> Prices);

public record PricePoint(DateTime Date, decimal Close, long Volume);

public class StockPriceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockPriceService> _logger;

    public StockPriceService(HttpClient httpClient, ILogger<StockPriceService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Yahoo Finance requires a browser-like User-Agent
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            );
        }
    }

    /// <summary>
    /// RESTORED: Fetches the most recent price for Portfolio calculations.
    /// </summary>
    public async Task<decimal> GetLivePriceAsync(string symbol)
    {
        try
        {
            var data = await GetHistoricalDataAsync(symbol, "1d");
            return data?.Prices.LastOrDefault()?.Close ?? 0m;
        }
        catch
        {
            return 0m;
        }
    }

    /// <summary>
    /// Fetches 52W High/Low and Dividends for the Stock Description section.
    /// </summary>
    public async Task<JsonElement?> GetStockFundamentalsAsync(string symbol)
    {
        string ticker = SanitizeTicker(symbol);
        string url =
            $"https://query2.finance.yahoo.com/v7/finance/quoteSummary/{ticker}?modules=summaryDetail,defaultKeyStatistics,financialData";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result");
            return result.GetArrayLength() > 0 ? result[0].Clone() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches historical price action for charts.
    /// </summary>
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

        TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        try
        {
            string ticker = SanitizeTicker(symbol);
            string url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?range={range}&interval={interval}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new HistoricalData(new());

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync()
            );
            var res = doc.RootElement.GetProperty("chart").GetProperty("result")[0];

            if (!res.TryGetProperty("timestamp", out var tProp))
                return new HistoricalData(new());

            var ts = tProp.EnumerateArray().ToList();
            var quote = res.GetProperty("indicators").GetProperty("quote")[0];
            var cls = quote.GetProperty("close").EnumerateArray().ToList();
            var vol = quote.GetProperty("volume").EnumerateArray().ToList();

            var prices = new List<PricePoint>();
            for (int i = 0; i < ts.Count; i++)
            {
                if (i < cls.Count && cls[i].ValueKind == JsonValueKind.Number)
                {
                    var ist = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime,
                        istZone
                    );

                    long volumeValue = 0;
                    if (i < vol.Count && vol[i].ValueKind == JsonValueKind.Number)
                        volumeValue = vol[i].GetInt64();

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
