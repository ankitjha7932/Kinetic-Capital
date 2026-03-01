using System.Text.Json;

namespace PortfolioManager.Api.Services;

public class StockDetailsService
{
    private readonly StockPriceService _priceService;

    public StockDetailsService(StockPriceService priceService)
    {
        _priceService = priceService;
    }

    private string MapValue(JsonElement? data, string yahooPath, string finnhubPath, bool isPercent = false)
    {
        if (data == null) return "N/A";

        try
        {
            var root = data.Value;

            // 1. Try Yahoo Mapping (Nested with 'fmt')
            var yahooParts = yahooPath.Split(':');
            if (root.TryGetProperty(yahooParts[0], out var section))
            {
                if (section.TryGetProperty(yahooParts[1], out var val) && 
                    val.TryGetProperty("fmt", out var fmt))
                {
                    return fmt.GetString() ?? "N/A";
                }
            }

            // 2. Try Finnhub Mapping (Direct raw value)
            // Note: Finnhub returns Market Cap in MILLIONS. 
            if (root.TryGetProperty(finnhubPath, out var finVal) && finVal.ValueKind != JsonValueKind.Null)
            {
                double val = finVal.GetDouble();

                if (finnhubPath == "marketCapitalization")
                {
                    // Convert Millions to Absolute for Cr formatting
                    val *= 1000000; 
                }

                if (isPercent)
                {
                    // If the value is a ratio (like 0.15 for 15%), format as percent
                    return (val > 1 ? val : val * 100).ToString("N2") + "%";
                }

                if (val > 10000000) 
                {
                    return (val / 10000000).ToString("N2") + " Cr";
                }

                return Math.Round(val, 2).ToString();
            }
        }
        catch { return "N/A"; }

        return "N/A";
    }

    public async Task<object?> GetStockDetailsAsync(string symbol, string range = "1y", string faceValue = "N/A")
    {
        var (fetchRange, interval, cutoffMonths) = range.ToLower() switch
        {
            "1m" => ("3y", "1d", 1),
            "3m" => ("3y", "1d", 3),
            "6m" => ("3y", "1d", 6),
            "1y" => ("3y", "1d", 12),
            "3y" => ("10y", "1wk", 36),
            "5y" => ("10y", "1wk", 60),
            _ => ("3y", "1d", 12)
        };

        var historyTask = _priceService.GetHistoricalDataAsync(symbol, fetchRange, interval);
        var fundamentalsTask = _priceService.GetStockFundamentalsAsync(symbol);

        await Task.WhenAll(historyTask, fundamentalsTask);

        var history = await historyTask;
        var fundamentals = await fundamentalsTask;

        if (history == null || !history.Prices.Any()) return null;

        var allPrices = history.Prices.Select(p => p.Close).ToList();
        decimal currentPrice = allPrices.Last();
        decimal prevPrice = allPrices.Count > 1 ? allPrices[^2] : currentPrice;
        DateTime cutoffDate = DateTime.UtcNow.AddMonths(-cutoffMonths);

        return new
        {
            Symbol = symbol.ToUpper(),
            LastUpdate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Ratios = new
            {
                // Mapping: (Data, YahooPath, FinnhubPath, IsPercent)
                MarketCap = MapValue(fundamentals, "summaryDetail:marketCap", "marketCapitalization"),
                CurrentPrice = Math.Round(currentPrice, 2),
                PriceChange = Math.Round(currentPrice - prevPrice, 2),
                PriceChangePercent = Math.Round(((currentPrice - prevPrice) / prevPrice) * 100, 2),
                High52W = allPrices.Where((p, idx) => history.Prices[idx].Date >= DateTime.UtcNow.AddYears(-1)).Max(),
                Low52W = allPrices.Where((p, idx) => history.Prices[idx].Date >= DateTime.UtcNow.AddYears(-1)).Min(),
                HistoricalHigh = allPrices.Max(),
                HistoricalLow = allPrices.Min(),
                StockPE = MapValue(fundamentals, "summaryDetail:trailingPE", "peExclExtraTTM"),
                BookValue = MapValue(fundamentals, "defaultKeyStatistics:bookValue", "bookValuePerShareAnnual"),
                DividendYield = MapValue(fundamentals, "summaryDetail:dividendYield", "dividendYieldIndicatedAnnual", true),
                ROCE = MapValue(fundamentals, "financialData:returnOnAssets", "roceTTM", true),
                ROE = MapValue(fundamentals, "financialData:returnOnEquity", "roeTTM", true),
                FaceValue = faceValue
            },
            ChartData = history.Prices.Select((p, i) => new
            {
                Date = p.Date.ToString("yyyy-MM-dd"),
                Price = Math.Round(p.Close, 2),
                dmA50 = i < 49 ? null : (decimal?)Math.Round(allPrices.Skip(i - 49).Take(50).Average(), 2),
                dmA200 = i < 199 ? null : (decimal?)Math.Round(allPrices.Skip(i - 199).Take(200).Average(), 2),
                Volume = p.Volume
            }).Where(d => DateTime.Parse(d.Date) >= cutoffDate).ToList()
        };
    }
}