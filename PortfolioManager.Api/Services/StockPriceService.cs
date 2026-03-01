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

        // Essential: Yahoo requires a browser-like User-Agent even without a crumb
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
    }

    public async Task<JsonElement?> GetStockFundamentalsAsync(string symbol)
    {
        string ticker = symbol.ToUpper().EndsWith(".NS") ? symbol.ToUpper() : $"{symbol.ToUpper()}.NS";
        string modules = "summaryDetail,financialData,defaultKeyStatistics";
        
        try
        {
            // Direct call to query2 without the &crumb parameter
            string url = $"https://query2.finance.yahoo.com/v7/finance/quoteSummary/{ticker}?modules={modules}";

            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result");
                return result.GetArrayLength() > 0 ? result[0].Clone() : null;
            }
            else
            {
                _logger.LogWarning("Yahoo rejected unauthenticated request for {Symbol} with Status: {Status}", ticker, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fundamentals for {Symbol}", ticker);
        }
        return null;
    }

    public async Task<decimal> GetLivePriceAsync(string symbol)
    {
        var data = await GetHistoricalDataAsync(symbol, "1d", "1m");
        return data?.Prices.LastOrDefault()?.Close ?? 0m;
    }

    public async Task<HistoricalData> GetHistoricalDataAsync(string symbol, string range = "1y", string interval = "1d")
    {
        try
        {
            string ticker = symbol.ToUpper().EndsWith(".NS") ? symbol.ToUpper() : $"{symbol.ToUpper()}.NS";
            string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?range={range}&interval={interval}";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new HistoricalData(new());

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
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
                if (closes[i].ValueKind == JsonValueKind.Number)
                {
                    prices.Add(new PricePoint(
                        DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).DateTime,
                        closes[i].GetDecimal(),
                        volumes[i].ValueKind == JsonValueKind.Number ? volumes[i].GetInt64() : 0
                    ));
                }
            }
            return new HistoricalData(prices);
        }
        catch { return new HistoricalData(new()); }
    }
}