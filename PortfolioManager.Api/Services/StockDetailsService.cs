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

        public StockDetailsService(
            StockPriceService priceService,
            IMongoDatabase database,
            IStockAnalysisService analysisService
        )
        {
            _priceService = priceService;
            _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
            _analysisService = analysisService;
        }

        public async Task<StockDetails?> GetStockDetailsAsync(string symbol, string range = "1y")
        {
            string ticker = symbol.ToUpper();
            string dbSymbol = SanitizeTicker(ticker);

            // 1. DATA PADDING STRATEGY
            // We fetch extra data (3y) so that the 200 DMA line is pre-calculated
            // even if the user only asks for a 1-month chart.
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

            // 2. CONCURRENT FETCH
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

            // 3. IST TIMEZONE & FILTERING LOGIC
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

            // 4. MAP CHART DATA & DMA (Using full padded list for calculations)
            var allPrices = history.Prices.Select(p => p.Close).ToList();
            var chartPoints = history
                .Prices.Select(
                    (p, i) =>
                        new ChartDataPoint
                        {
                            Date = p.Date,
                            Price = Math.Round(p.Close, 2),
                            Volume = p.Volume,
                            // DMA: Calculate using the full list, but filter the points later
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
                .Where(d => d.Date >= cutoffDate) // Filter for the specific range requested
                .ToList();

            // 5. FALLBACK: IF MARKET CLOSED (Weekend/Holiday)
            if (cutoffMode == "today" && chartPoints.Count < 5)
            {
                var lastActiveDate = history.Prices.Last().Date.Date;
                chartPoints = history
                    .Prices.Where(p => p.Date.Date == lastActiveDate)
                    .Select(p => new ChartDataPoint
                    {
                        Date = p.Date,
                        Price = Math.Round(p.Close, 2),
                        Volume = p.Volume,
                    })
                    .ToList();
            }

            // 6. EXTRACT LIVE PRICE & CHANGE FROM YAHOO SUMMARY
            decimal curPrice = allPrices.Last(),
                pChange = 0,
                pChangePct = 0;
            if (yahooSummary.HasValue)
            {
                try
                {
                    var priceObj = yahooSummary.Value.GetProperty("price");
                    curPrice = priceObj
                        .GetProperty("regularMarketPrice")
                        .GetProperty("raw")
                        .GetDecimal();
                    pChange = priceObj
                        .GetProperty("regularMarketChange")
                        .GetProperty("raw")
                        .GetDecimal();
                    pChangePct =
                        priceObj
                            .GetProperty("regularMarketChangePercent")
                            .GetProperty("raw")
                            .GetDecimal() * 100;
                }
                catch
                {
                    // Fallback to last close if property missing
                    curPrice = allPrices.Last();
                }
            }

            // 7. ASSEMBLE FINAL OBJECT
            var details = new StockDetails
            {
                Symbol = ticker,
                Industry = fundamentals?.Industry ?? "N/A",
                LastUpdate = istNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Ratios = new FundamentalRatios
                {
                    CurrentPrice = Math.Round(curPrice, 2),
                    PriceChange = Math.Round(pChange, 2),
                    PriceChangePercent = Math.Round(pChangePct, 2),
                    MarketCap =
                        fundamentals?.MarketCap != null ? $"{fundamentals.MarketCap} Cr" : "N/A",
                    High52W = ParseDecimal(yahooSummary, "fiftyTwoWeekHigh"),
                    Low52W = ParseDecimal(yahooSummary, "fiftyTwoWeekLow"),
                    HistoricalHigh = Math.Round(allPrices.Max(), 2),
                    HistoricalLow = Math.Round(allPrices.Min(), 2),
                    StockPE = fundamentals?.StockPE ?? "N/A",
                    ROCE = fundamentals?.ROCE != null ? $"{fundamentals.ROCE}%" : "N/A",
                    ROE = fundamentals?.ROE != null ? $"{fundamentals.ROE}%" : "N/A",
                    DividendYield = fundamentals?.DividendYield ?? "N/A",
                    FaceValue = fundamentals?.FaceValue ?? "N/A",
                },
                ChartData = chartPoints,
                QuarterlyResults = fundamentals?.QuarterlyResults ?? new(),
                ProfitAndLoss = fundamentals?.ProfitAndLoss ?? new(),
                BalanceSheet = fundamentals?.BalanceSheet ?? new(),
                CashFlow = fundamentals?.CashFlow ?? new(),
                Peers = fundamentals?.Peers ?? new(),
            };

            // 8. RUN AUTOMATED TECHNICAL ANALYSIS
            // This populates the "Strongly Bullish / Bearish" rating and reasons
            details.Analysis = _analysisService.AnalyzeStock(details);

            return details;
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

        private string SanitizeTicker(string s) => s.ToUpper().EndsWith(".NS") ? s : $"{s}.NS";
    }
}
