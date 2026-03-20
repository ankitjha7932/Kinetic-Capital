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
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
        );
    }

    // RESTORED: PortfolioController needs this for current valuations
    public async Task<decimal> GetLivePriceAsync(string symbol)
    {
        // We fetch 1d range (intraday) to get the most recent price point
        var data = await GetHistoricalDataAsync(symbol, "1d");
        return data?.Prices.LastOrDefault()?.Close ?? 0m;
    }

    public async Task<HistoricalData> GetHistoricalDataAsync(string symbol, string range = "1y")
    {
        // FIX: Map both the user ranges AND the "fetch ranges" to the correct intervals
        string interval = range.ToLower() switch
        {
            "1d" => "1m", // Today's minute data
            "5d" => "5m", // High-res padding for 1d/1w
            "1w" => "5m", // Weekly high-res
            "1mo" => "1h", // Hourly padding for 1m
            "1m" => "1h", // Monthly hourly
            "3y" => "1d", // Daily for 3y
            "max" => "1wk", // Weekly for Max
            _ => "1d", // Default to daily for 6m, 1y, 2y, etc.
        };

        TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        try
        {
            string ticker =
                symbol.ToUpper().EndsWith(".NS") || symbol.ToUpper().EndsWith(".BO")
                    ? symbol.ToUpper()
                    : $"{symbol.ToUpper()}.NS";

            string url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?range={range}&interval={interval}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new HistoricalData(new());

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync()
            );
            var result = document.RootElement.GetProperty("chart").GetProperty("result")[0];

            if (!result.TryGetProperty("timestamp", out var timestampProp))
                return new HistoricalData(new());

            var timestamps = timestampProp.EnumerateArray().ToList();
            var indicators = result.GetProperty("indicators").GetProperty("quote")[0];
            var closes = indicators.GetProperty("close").EnumerateArray().ToList();
            var volumes = indicators.GetProperty("volume").EnumerateArray().ToList();

            var prices = new List<PricePoint>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                if (i < closes.Count && closes[i].ValueKind == JsonValueKind.Number)
                {
                    var istDate = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime,
                        istZone
                    );

                    prices.Add(
                        new PricePoint(
                            istDate,
                            closes[i].GetDecimal(),
                            i < volumes.Count && volumes[i].ValueKind == JsonValueKind.Number
                                ? volumes[i].GetInt64()
                                : 0
                        )
                    );
                }
            }
            return new HistoricalData(prices);
        }
        catch
        {
            return new HistoricalData(new());
        }
    }

    private string SanitizeTicker(string symbol) =>
        symbol.ToUpper().EndsWith(".NS") || symbol.ToUpper().EndsWith(".BO")
            ? symbol.ToUpper()
            : $"{symbol.ToUpper()}.NS";
}
