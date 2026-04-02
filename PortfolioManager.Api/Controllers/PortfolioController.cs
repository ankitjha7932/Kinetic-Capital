using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PortfolioHealthService _health;
    private readonly StockPriceService _priceService;
    private readonly NewsService _newsService;
    private readonly MarketService _marketService;

    public PortfolioController(
        AppDbContext db,
        PortfolioHealthService health,
        StockPriceService priceService,
        NewsService newsService,
        MarketService marketService
    )
    {
        _db = db;
        _health = health;
        _priceService = priceService;
        _newsService = newsService;
        _marketService = marketService;
    }

    [HttpGet("summary/{userId}")]
    public async Task<IActionResult> GetSummary(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required.");
        }

        try
        {
            var holdings = await _db.Holdings.Where(h => h.UserId == userId).ToListAsync();
            var holdingResponses = new List<HoldingResponse>();

            foreach (var h in holdings)
            {
                // 1. Fetch Live Price (Safe Fetch)
                decimal livePrice = 0;
                try
                {
                    livePrice = await _priceService.GetLivePriceAsync(h.Symbol);
                }
                catch
                { /* Log price service error if needed */
                }

                if (livePrice <= 0)
                    livePrice = h.AvgBuyPrice;

                // 2. Calculations
                decimal unrealizedPnl = CalculatePnl(h.Quantity, h.AvgBuyPrice, livePrice);
                decimal pnlPercent =
                    h.AvgBuyPrice > 0
                        ? Math.Round(((livePrice - h.AvgBuyPrice) / h.AvgBuyPrice) * 100, 2)
                        : 0;

                // Mocked 1D change
                decimal change1D = 0.85m;

                // 3. Fetch Fundamental Data (DEFENSIVE FETCH)
                // This is where the 'PeerSymbols' crash was happening.
                // We wrap it so one bad document doesn't kill the whole request.
                string? marketCapLabel = null;
                try
                {
                    var fundamental = await _db.Stocks.FirstOrDefaultAsync(s =>
                        s.Symbol == h.Symbol
                    );
                    if (fundamental != null)
                    {
                        marketCapLabel = GetMarketCapLabel(ParseMarketCap(fundamental.MarketCap));
                    }
                }
                catch (Exception ex)
                {
                    // Log the specific stock that is failing mapping
                    Console.WriteLine($"Mapping Error for {h.Symbol}: {ex.Message}");
                }

                holdingResponses.Add(
                    new HoldingResponse(
                        h.Id,
                        h.Symbol,
                        h.Quantity,
                        h.AvgBuyPrice,
                        livePrice,
                        unrealizedPnl,
                        h.BuyDate,
                        change1D,
                        pnlPercent,
                        h.Tags ?? "Equity",
                        marketCapLabel
                    )
                );
            }

            // 4. Global Portfolio Aggregates
            var totalInv = holdingResponses.Any()
                ? Math.Round(holdingResponses.Sum(h => h.Quantity * h.AvgBuyPrice), 2)
                : 0;
            var totalCur = holdingResponses.Any()
                ? Math.Round(holdingResponses.Sum(h => h.Quantity * h.CurrentPrice), 2)
                : 0;
            var totalPnl = Math.Round(totalCur - totalInv, 2);
            var totalPnlPct = totalInv > 0 ? Math.Round((totalPnl / totalInv) * 100, 2) : 0;

            // FINAL SUCCESS RETURN
            return Ok(
                new PortfolioSummaryResponse
                {
                    UserId = userId,
                    TotalHoldings = holdingResponses.Count,
                    TotalInvested = totalInv,
                    CurrentValue = totalCur,
                    TotalPnl = totalPnl,
                    TotalPnlPercent = totalPnlPct,
                    Holdings = holdingResponses,
                }
            );
        }
        catch (Exception ex)
        {
            // FINAL ERROR RETURN (Fixes CS0161)
            Console.WriteLine($"Fatal Portfolio Summary Error: {ex}");
            return StatusCode(500, $"Internal Server Error: {ex.Message}");
        }
    }

    [HttpGet("analysis")]
    public async Task<IActionResult> AnalyzeCurrentUser([FromQuery] string userId)
    {
        // 1. Get current portfolio data
        var summaryResult = await GetSummary(userId) as OkObjectResult;
        if (summaryResult?.Value is PortfolioSummaryResponse summary)
        {
            // 2. Run the Health/Advice analysis
            var healthResult = _health.Analyze(userId, summary.Holdings);

            // 3 Fetch 7-day trends for all symbols in the portfolio
            var symbols = summary.Holdings.Select(h => h.Symbol).ToList();
            var sparklineMap = await _priceService.GetBatchSparklinesAsync(symbols);

            // 4. Inject history into each position's advice object
            foreach (var pos in healthResult.Positions)
            {
                if (sparklineMap.TryGetValue(pos.Symbol, out var trend))
                {
                    pos.History = trend;
                }
            }

            // Return the healthResult which now contains per-position history
            return Ok(healthResult);
        }
        return BadRequest("Could not analyze portfolio.");
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound("User not found.");

        var sectors = (user.PreferredSectors ?? "").Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        return Ok(_health.SuggestStocks(user.RiskProfile ?? "Moderate", sectors));
    }

    [HttpGet("price/{symbol}")]
    public async Task<IActionResult> GetSinglePrice(string symbol)
    {
        decimal price = await _priceService.GetLivePriceAsync(symbol);
        return price <= 0 ? NotFound() : Ok(new { Symbol = symbol, Price = price });
    }

    [HttpGet("news/{symbol}")]
    public async Task<IActionResult> GetNews(string symbol)
    {
        var news = await _newsService.GetStockNewsAsync(symbol);
        return (news == null || !news.Any()) ? NotFound() : Ok(news);
    }

    [HttpGet("high-infusion")]
    public async Task<IActionResult> GetHighInfusion() =>
        Ok(await _marketService.GetHighInfusionStocksAsync());

    [HttpGet("ticker")]
    public async Task<IActionResult> GetTicker() => Ok(await _marketService.GetTickerDataAsync());

    [HttpDelete("holding/{id}")]
    public async Task<IActionResult> RemoveHolding(string id)
    {
        var holding = await _db.Holdings.FindAsync(id);
        if (holding == null)
            return NotFound();

        _db.Holdings.Remove(holding);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Asset removed" });
    }

    private double ParseMarketCap(string? mCapStr)
    {
        if (string.IsNullOrEmpty(mCapStr))
            return 0;
        string cleanValue = mCapStr
            .Replace("Cr", "", StringComparison.OrdinalIgnoreCase)
            .Replace(",", "")
            .Trim();
        double.TryParse(
            cleanValue,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double val
        );
        return val;
    }

    private string? GetMarketCapLabel(double mCapCr)
    {
        if (mCapCr <= 0)
            return null;
        if (mCapCr >= 20000)
            return "LARGE-CAP";
        if (mCapCr >= 5000)
            return "MID-CAP";
        if (mCapCr >= 500)
            return "SMALL-CAP";
        return "MICRO-CAP";
    }

    private decimal CalculatePnl(decimal quantity, decimal avgPrice, decimal currentPrice) =>
        Math.Round(quantity * (currentPrice - avgPrice), 2);

    public record PortfolioHistoryPoint(DateTime Date, decimal Value);
}
