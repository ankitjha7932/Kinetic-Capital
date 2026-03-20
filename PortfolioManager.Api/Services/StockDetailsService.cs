using System.Text.Json;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class StockDetailsService
{
    private readonly StockPriceService _priceService;
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;

    public StockDetailsService(StockPriceService priceService, IMongoDatabase database)
    {
        _priceService = priceService;
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
    }

    public async Task<StockDetails?> GetStockDetailsAsync(string symbol, string range = "1y")
    {
        string ticker = symbol.ToUpper();
        string dbSymbol = SanitizeTicker(ticker);

        // 1. DATA PADDING STRATEGY
        // We fetch much more data than we show so the lines (DMA) are pre-calculated
        var (fetchRange, cutoffMode) = range.ToLower() switch
        {
            "1d" => ("1mo", "today"), // Fetch 1mo for volume avg context
            "1w" => ("1mo", "week"),
            "1m" => ("1y", "month"),
            "3m" => ("2y", "3month"),
            "6m" => ("2y", "6month"),
            "1y" => ("3y", "year"), // Fetch 3y to show a perfect 200 DMA for a 1y chart
            "3y" => ("5y", "3year"),
            _ => ("max", "max"),
        };

        // 2. CONCURRENT FETCH
        var historyTask = _priceService.GetHistoricalDataAsync(dbSymbol, fetchRange);
        var mongoTask = _fundamentalCollection
            .Find(f => f.Symbol == dbSymbol)
            .FirstOrDefaultAsync();
        var yahooSummaryTask = _priceService.GetStockFundamentalsAsync(dbSymbol);

        await Task.WhenAll(historyTask, mongoTask, yahooSummaryTask);

        var history = await historyTask;
        var fundamentals = await mongoTask;
        var yahooSummary = await yahooSummaryTask;

        if (history == null || !history.Prices.Any())
            return null;

        // 3. CALCULATE ALL DATA POINTS
        var allPrices = history.Prices.Select(p => p.Close).ToList();
        var allVolumes = history.Prices.Select(p => (double)p.Volume).ToList();

        // Calculate a 20-period volume average for the "Cash Flood" detection
        double avgVol =
            allVolumes.Count > 20 ? allVolumes.TakeLast(20).Average() : allVolumes.Average();

        // 4. PRECISE IST CUTOFF
        TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        DateTime istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        DateTime cutoffDate = cutoffMode switch
        {
            "today" => new DateTime(istNow.Year, istNow.Month, istNow.Day, 9, 15, 0),
            "week" => istNow.AddDays(-7),
            "month" => istNow.AddMonths(-1),
            "3month" => istNow.AddMonths(-3),
            "6month" => istNow.AddMonths(-6),
            "year" => istNow.AddMonths(-12),
            "3year" => istNow.AddMonths(-36),
            _ => DateTime.MinValue,
        };

        // 5. MAP & CALCULATE DMA (Using the full 'allPrices' list)
        var chartPoints = history
            .Prices.Select(
                (p, i) =>
                    new ChartDataPoint
                    {
                        Date = p.Date,
                        Price = Math.Round(p.Close, 2),
                        Volume = p.Volume,
                        // These will now have values even at the start of the visible chart
                        // because 'i' starts from the beginning of the 3-year fetch
                        DmA50 =
                            i < 49
                                ? null
                                : (decimal?)
                                    Math.Round(allPrices.Skip(i - 49).Take(50).Average(), 2),
                        DmA200 =
                            i < 199
                                ? null
                                : (decimal?)
                                    Math.Round(allPrices.Skip(i - 199).Take(200).Average(), 2),
                        IsVolumeSpike = avgVol > 0 && (double)p.Volume > (avgVol * 2),
                    }
            )
            .Where(d => d.Date >= cutoffDate) // <-- THIS is where we filter for the UI
            .ToList();

        // 6. ASSEMBLE RESULT
        return new StockDetails
        {
            Symbol = ticker,
            Industry = fundamentals?.Industry ?? "N/A",
            Ratios = new FundamentalRatios
            {
                CurrentPrice = Math.Round(allPrices.Last(), 2),
                MarketCap =
                    fundamentals?.MarketCap != null ? $"{fundamentals.MarketCap} Cr" : "N/A",

                // Waterfall for 52W stats (Summary API -> History fallback)
                High52W =
                    Math.Round(ParseDecimal(yahooSummary, "fiftyTwoWeekHigh"), 2) != 0
                        ? Math.Round(ParseDecimal(yahooSummary, "fiftyTwoWeekHigh"), 2)
                        : Math.Round(allPrices.Max(), 2),

                Low52W =
                    Math.Round(ParseDecimal(yahooSummary, "fiftyTwoWeekLow"), 2) != 0
                        ? Math.Round(ParseDecimal(yahooSummary, "fiftyTwoWeekLow"), 2)
                        : Math.Round(allPrices.Min(), 2),

                FaceValue = fundamentals?.FaceValue ?? "N/A",
                StockPE = fundamentals?.StockPE ?? "N/A",
                ROCE = fundamentals?.ROCE != null ? $"{fundamentals.ROCE}%" : "N/A",
                ROE = fundamentals?.ROE != null ? $"{fundamentals.ROE}%" : "N/A",
                DividendYield = fundamentals?.DividendYield ?? "N/A",
            },
            ChartData = chartPoints,
            QuarterlyResults = fundamentals?.QuarterlyResults ?? new(),
            ProfitAndLoss = fundamentals?.ProfitAndLoss ?? new(),
            BalanceSheet = fundamentals?.BalanceSheet ?? new(),
            CashFlow = fundamentals?.CashFlow ?? new(),
            Peers = fundamentals?.Peers ?? new(),
        };
    }

    private decimal ParseDecimal(JsonElement? summary, string propertyName)
    {
        if (!summary.HasValue)
            return 0;
        try
        {
            var sd = summary.Value.GetProperty("summaryDetail");
            if (sd.TryGetProperty(propertyName, out var prop))
                return prop.GetProperty("raw").GetDecimal();
        }
        catch { }
        return 0;
    }

    private string SanitizeTicker(string s) =>
        s.ToUpper().EndsWith(".NS") ? s.ToUpper() : $"{s.ToUpper()}.NS";
}
