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

    public async Task<StockDetails?> GetStockDetailsAsync(
        string symbol,
        string range = "1y",
        string faceValue = "N/A"
    )
    {
        string ticker = symbol.ToUpper();
        string dbSymbol = ticker.EndsWith(".NS") ? ticker : $"{ticker}.NS";

        // 1. DYNAMIC FETCH RANGES
        var (fetchRange, cutoffMode) = range.ToLower() switch
        {
            "1d" => ("1d", "today"), // Fetch 1 day of 1m data
            "1w" => ("5d", "week"), // Fetch 5 days of 5m data
            "1m" => ("1y", "month"), // Fetch 1y for DMA context
            "3m" => ("2y", "3month"),
            "6m" => ("2y", "6month"),
            "1y" => ("2y", "year"),
            "3y" => ("5y", "3year"),
            "max" => ("max", "max"),
            _ => ("2y", "year"),
        };

        var historyTask = _priceService.GetHistoricalDataAsync(dbSymbol, fetchRange);
        var mongoTask = _fundamentalCollection
            .Find(f => f.Symbol == dbSymbol)
            .FirstOrDefaultAsync();

        await Task.WhenAll(historyTask, mongoTask);
        var history = await historyTask;
        var fundamentals = await mongoTask;

        if (history == null || !history.Prices.Any())
            return null;

        var allPrices = history.Prices.Select(p => p.Close).ToList();

        // 2. PRECISE CUTOFF LOGIC
        DateTime now = DateTime.UtcNow; // Service uses UTC internally for consistency
        TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        DateTime istNow = TimeZoneInfo.ConvertTimeFromUtc(now, istZone);

        DateTime cutoffDate = cutoffMode switch
        {
            // Start of today's session (9:15 AM IST)
            "today" => new DateTime(istNow.Year, istNow.Month, istNow.Day, 9, 15, 0),
            "week" => istNow.AddDays(-7),
            "month" => istNow.AddMonths(-1),
            "3month" => istNow.AddMonths(-3),
            "year" => istNow.AddMonths(-12),
            "3year" => istNow.AddMonths(-36),
            "max" => DateTime.MinValue,
            _ => istNow.AddMonths(-12),
        };

        // 3. MAP CHART DATA
        var chartPoints = history
            .Prices.Select(
                (p, i) =>
                    new ChartDataPoint
                    {
                        Date = p.Date,
                        Price = Math.Round(p.Close, 2),
                        Volume = p.Volume,
                        // DMAs will naturally be null for 1d/1w because there aren't enough points in the fetchRange
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
                    }
            )
            .Where(d => d.Date >= cutoffDate)
            .ToList();

        // Fallback: If "Today" has no data yet (pre-market), show the full 1d fetch
        if (range == "1d" && chartPoints.Count < 2)
            chartPoints = history
                .Prices.Select(p => new ChartDataPoint
                {
                    Date = p.Date,
                    Price = p.Close,
                    Volume = p.Volume,
                })
                .ToList();

        return new StockDetails
        {
            Symbol = ticker,
            Industry = fundamentals?.Industry ?? "N/A",
            Ratios = new FundamentalRatios
            {
                CurrentPrice = Math.Round(allPrices.Last(), 2),
                PriceChange = Math.Round(
                    allPrices.Last() - (allPrices.Count > 1 ? allPrices[^2] : allPrices.Last()),
                    2
                ),
                PriceChangePercent =
                    allPrices.Count > 1
                        ? Math.Round(((allPrices.Last() - allPrices[^2]) / allPrices[^2]) * 100, 2)
                        : 0,
                MarketCap = fundamentals?.MarketCap ?? "N/A",
                StockPE = fundamentals?.StockPE ?? "N/A",
                ROCE = fundamentals?.ROCE ?? "N/A",
                ROE = fundamentals?.ROE ?? "N/A",
                HistoricalHigh = Math.Round(allPrices.Max(), 2),
                HistoricalLow = Math.Round(allPrices.Min(), 2),
                FaceValue = faceValue,
            },
            ChartData = chartPoints,
            QuarterlyResults = fundamentals?.QuarterlyResults ?? new(),
            ProfitAndLoss = fundamentals?.ProfitAndLoss ?? new(),
            BalanceSheet = fundamentals?.BalanceSheet ?? new(),
            CashFlow = fundamentals?.CashFlow ?? new(),
            Peers = fundamentals?.Peers ?? new(),
        };
    }

    private string SanitizeTicker(string symbol) =>
        symbol.ToUpper().EndsWith(".NS") || symbol.ToUpper().EndsWith(".BO")
            ? symbol.ToUpper()
            : $"{symbol.ToUpper()}.NS";
}
