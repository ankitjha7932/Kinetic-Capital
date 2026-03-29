using System.Text.Json;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services
{
    public class StockDetailsService
    {
        private readonly StockPriceService _priceService;
        private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
        private readonly IStockAnalysisService _analysisService;

        private static readonly Dictionary<string, string> _faceValueCache = new();

        // Object used to synchronize access to the dictionary during initialization
        private static readonly object _csvLock = new();

        public StockDetailsService(
            StockPriceService priceService,
            IMongoDatabase database,
            IStockAnalysisService analysisService
        )
        {
            _priceService = priceService;
            _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
            _analysisService = analysisService;

            // Thread-safe initialization of the static cache
            if (!_faceValueCache.Any())
            {
                lock (_csvLock)
                {
                    // Double-check to ensure another thread didn't fill it while we were waiting for the lock
                    if (!_faceValueCache.Any())
                    {
                        string fileName = Path.Combine("Data", "EQUITY_L.csv");
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                        if (!File.Exists(path))
                            path = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                        if (File.Exists(path))
                        {
                            try
                            {
                                var lines = File.ReadAllLines(path).Skip(1);
                                foreach (var line in lines)
                                {
                                    var parts = line.Split(',');
                                    if (parts.Length >= 8)
                                    {
                                        string symbolKey = parts[0]
                                            .Trim()
                                            .ToUpper()
                                            .Replace("\"", "");
                                        string faceVal = parts[7].Trim().Replace("\"", "");
                                        // Using indexed assignment is safe within the lock
                                        _faceValueCache[symbolKey] = faceVal;
                                    }
                                }
                            }
                            catch
                            { /* Silent fail to prevent API crash on file lock */
                            }
                        }
                    }
                }
            }
        }

        public async Task<StockDetails?> GetStockDetailsAsync(string symbol, string range = "1d")
        {
            string ticker = symbol.ToUpper();
            string dbSymbol = SanitizeTicker(ticker);
            string rawSymbol = ticker.Split('.')[0];

            var (fetchRange, cutoffMode) = range.ToLower() switch
            {
                "1d" => ("5d", "today"),
                "1w" => ("1mo", "week"),
                "1m" => ("1y", "month"),
                "3m" => ("2y", "3month"),
                "6m" => ("2y", "6month"),
                "1y" => ("3y", "year"),
                "3y" => ("5y", "3year"),
                _ => ("3y", "year"),
            };

            var historyTask = _priceService.GetHistoricalDataAsync(dbSymbol, fetchRange);
            var mongoTask = _fundamentalCollection
                .Find(f => f.Symbol == dbSymbol)
                .FirstOrDefaultAsync();
            var summaryTask = _priceService.GetStockFundamentalsAsync(dbSymbol);

            await Task.WhenAll(historyTask, mongoTask, summaryTask);

            var history = await historyTask;
            var fundamentals = await mongoTask;
            var yahooSummary = await summaryTask;

            if (history == null || !history.Prices.Any())
                return null;

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
                _ => istNow.AddMonths(-12),
            };

            var allPrices = history.Prices.OrderBy(p => p.Date).ToList();

            var chartPoints = allPrices
                .Select(
                    (p, i) =>
                        new ChartDataPoint
                        {
                            Date = p.Date,
                            Price = Math.Round(p.Close, 2),
                            Volume = p.Volume,
                            DmA50 =
                                i < 49
                                    ? null
                                    : (decimal?)
                                        Math.Round(
                                            allPrices
                                                .Skip(i - 49)
                                                .Take(50)
                                                .Select(x => x.Close)
                                                .Average(),
                                            2
                                        ),
                            DmA200 =
                                i < 199
                                    ? null
                                    : (decimal?)
                                        Math.Round(
                                            allPrices
                                                .Skip(i - 199)
                                                .Take(200)
                                                .Select(x => x.Close)
                                                .Average(),
                                            2
                                        ),
                        }
                )
                .Where(d => d.Date >= cutoffDate)
                .ToList();

            if (cutoffMode == "today" && chartPoints.Count < 5)
            {
                var lastActiveDate = allPrices.Last().Date.Date;
                chartPoints = allPrices
                    .Where(p => p.Date.Date == lastActiveDate)
                    .Select(p => new ChartDataPoint
                    {
                        Date = p.Date,
                        Price = Math.Round(p.Close, 2),
                        Volume = p.Volume,
                    })
                    .ToList();
            }

            decimal currentPrice = allPrices.Last().Close;
            decimal dailyChange = 0;
            decimal dailyPct = 0;

            if (yahooSummary.HasValue)
            {
                try
                {
                    var pObj = yahooSummary.Value.GetProperty("price");
                    currentPrice = pObj.GetProperty("regularMarketPrice")
                        .GetProperty("raw")
                        .GetDecimal();
                    dailyChange = pObj.GetProperty("regularMarketChange")
                        .GetProperty("raw")
                        .GetDecimal();
                    dailyPct =
                        pObj.GetProperty("regularMarketChangePercent")
                            .GetProperty("raw")
                            .GetDecimal() * 100;
                }
                catch { }
            }

            if (dailyChange == 0)
            {
                var latestPoint = allPrices.Last();
                var prevSessionPoint = allPrices.LastOrDefault(p =>
                    p.Date.Date < latestPoint.Date.Date
                );
                decimal referencePrevClose = prevSessionPoint?.Close ?? allPrices.First().Close;
                dailyChange = currentPrice - referencePrevClose;
                dailyPct = referencePrevClose != 0 ? (dailyChange / referencePrevClose) * 100 : 0;
            }

            decimal periodHigh = chartPoints.Any() ? chartPoints.Max(p => p.Price) : currentPrice;
            decimal periodLow = chartPoints.Any() ? chartPoints.Min(p => p.Price) : currentPrice;
            decimal periodStartPrice = chartPoints.Any() ? chartPoints.First().Price : currentPrice;
            decimal periodReturn =
                (periodStartPrice > 0)
                    ? ((currentPrice - periodStartPrice) / periodStartPrice) * 100
                    : 0;

            decimal high52 = ParseDecimal(yahooSummary, "fiftyTwoWeekHigh");
            decimal low52 = ParseDecimal(yahooSummary, "fiftyTwoWeekLow");
            if (high52 == 0)
                high52 = allPrices.TakeLast(Math.Min(allPrices.Count, 252)).Max(p => p.Close);
            if (low52 == 0)
                low52 = allPrices.TakeLast(Math.Min(allPrices.Count, 252)).Min(p => p.Close);

            return new StockDetails
            {
                Symbol = ticker,
                Industry = fundamentals?.Industry ?? "N/A",
                LastUpdate = istNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Ratios = new FundamentalRatios
                {
                    CurrentPrice = Math.Round(currentPrice, 2),
                    PriceChange = Math.Round(dailyChange, 2),
                    PriceChangePercent = Math.Round(dailyPct, 2),
                    MarketCap =
                        fundamentals?.MarketCap != null ? $"{fundamentals.MarketCap} Cr" : "N/A",
                    High52W = Math.Round(high52, 2),
                    Low52W = Math.Round(low52, 2),
                    HistoricalHigh = Math.Round(allPrices.Max(p => p.Close), 2),
                    HistoricalLow = Math.Round(allPrices.Min(p => p.Close), 2),
                    StockPE = fundamentals?.StockPE ?? "N/A",
                    ROCE = fundamentals?.ROCE != null ? $"{fundamentals.ROCE}%" : "N/A",
                    ROE = fundamentals?.ROE != null ? $"{fundamentals.ROE}%" : "N/A",
                    DividendYield = fundamentals?.DividendYield ?? "0.00",
                    // Read access is now safe because initialization is locked
                    FaceValue =
                        _faceValueCache.GetValueOrDefault(rawSymbol)
                        ?? fundamentals?.FaceValue
                        ?? "N/A",
                },
                ChartData = chartPoints,
                PeriodHigh = Math.Round(periodHigh, 2),
                PeriodLow = Math.Round(periodLow, 2),
                PeriodReturn = Math.Round(periodReturn, 2),
                QuarterlyResults = fundamentals?.QuarterlyResults ?? new(),
                ProfitAndLoss = fundamentals?.ProfitAndLoss ?? new(),
                BalanceSheet = fundamentals?.BalanceSheet ?? new(),
                CashFlow = fundamentals?.CashFlow ?? new(),
                Peers = fundamentals?.Peers ?? new(),
                Shareholding = fundamentals?.Shareholding ?? new(),
            };
        }

        private decimal ParseDecimal(JsonElement? summary, string prop)
        {
            if (!summary.HasValue)
                return 0;
            try
            {
                var sd = summary.Value.GetProperty("summaryDetail");
                return sd.GetProperty(prop).GetProperty("raw").GetDecimal();
            }
            catch
            {
                return 0;
            }
        }

        private string SanitizeTicker(string s) =>
            s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
                ? s.ToUpper()
                : $"{s.ToUpper()}.NS";
    }
}
