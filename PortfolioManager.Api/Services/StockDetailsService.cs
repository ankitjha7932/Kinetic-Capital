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

    public async Task<StockDetails?> GetStockDetailsAsync(
        string symbol,
        string range = "1y",
        string faceValue = "N/A"
    )
    {
        string ticker = symbol.ToUpper();
        string dbSymbol = ticker.EndsWith(".NS") ? ticker : $"{ticker}.NS";

        var (fetchRange, interval, cutoffMonths) = range.ToLower() switch
        {
            "1m" => ("3y", "1d", 1),
            "3m" => ("3y", "1d", 3),
            "6m" => ("3y", "1d", 6),
            "1y" => ("3y", "1d", 12),
            _ => ("3y", "1d", 12),
        };

        var historyTask = _priceService.GetHistoricalDataAsync(dbSymbol, fetchRange, interval);
        var mongoTask = _fundamentalCollection
            .Find(f => f.Symbol == dbSymbol)
            .FirstOrDefaultAsync();

        await Task.WhenAll(historyTask, mongoTask);
        var history = await historyTask;
        var fundamentals = await mongoTask;

        if (history == null || !history.Prices.Any())
            return null;

        var allPrices = history.Prices.Select(p => (decimal)p.Close).ToList();
        decimal currentPrice = allPrices.Last();
        decimal prevPrice = allPrices.Count > 1 ? allPrices[^2] : currentPrice;
        DateTime cutoffDate = DateTime.UtcNow.AddMonths(-cutoffMonths);

        return new StockDetails
        {
            Symbol = ticker,
            Industry = fundamentals?.Industry ?? "N/A",
            LastUpdate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),

            // Map Ratios
            Ratios = new FundamentalRatios
            {
                CurrentPrice = Math.Round(currentPrice, 2),
                PriceChange = Math.Round(currentPrice - prevPrice, 2),
                PriceChangePercent = Math.Round(((currentPrice - prevPrice) / prevPrice) * 100, 2),
                MarketCap =
                    (fundamentals?.MarketCap ?? "N/A")
                    + (fundamentals?.MarketCap != null ? " Cr" : ""),
                StockPE = fundamentals?.StockPE ?? "N/A",
                ROCE = fundamentals?.ROCE ?? "N/A",
                ROE = fundamentals?.ROE ?? "N/A",
                BookValue = fundamentals?.BookValue ?? "N/A",
                DividendYield = fundamentals?.DividendYield ?? "N/A",
                FaceValue = faceValue,
                High52W = Math.Round(
                    allPrices
                        .Where((p, idx) => history.Prices[idx].Date >= DateTime.UtcNow.AddYears(-1))
                        .DefaultIfEmpty(currentPrice)
                        .Max(),
                    2
                ),
                Low52W = Math.Round(
                    allPrices
                        .Where((p, idx) => history.Prices[idx].Date >= DateTime.UtcNow.AddYears(-1))
                        .DefaultIfEmpty(currentPrice)
                        .Min(),
                    2
                ),
                HistoricalHigh = Math.Round(allPrices.Max(), 2),
                HistoricalLow = Math.Round(allPrices.Min(), 2),
            },

            // Map Financial Tables
            QuarterlyResults = fundamentals?.QuarterlyResults ?? new(),
            ProfitAndLoss = fundamentals?.ProfitAndLoss ?? new(),
            BalanceSheet = fundamentals?.BalanceSheet ?? new(),
            CashFlow = fundamentals?.CashFlow ?? new(),
            Peers = fundamentals?.Peers ?? new(),

            // Map Chart Data with DMAs
            ChartData = history
                .Prices.Select(
                    (p, i) =>
                        new ChartDataPoint
                        {
                            Date = p.Date,
                            Price = Math.Round((decimal)p.Close, 2),
                            Volume = p.Volume,
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
                .ToList(),
        };
    }
}
