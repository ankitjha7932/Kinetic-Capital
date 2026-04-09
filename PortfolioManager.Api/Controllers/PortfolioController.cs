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
        if (string.IsNullOrEmpty(userId)) return BadRequest("User ID is required.");

        try
        {
            var holdings = await _db.Holdings.Where(h => h.UserId == userId).ToListAsync();
            
            // 🔥 FIX: Process all holdings in parallel to prevent 502 Timeout
            var tasks = holdings.Select(h => ProcessSingleHolding(h));
            var holdingResponses = (await Task.WhenAll(tasks)).ToList();

            // 4. Global Portfolio Aggregates
            var totalInv = holdingResponses.Any() ? Math.Round(holdingResponses.Sum(h => h.Quantity * h.AvgBuyPrice), 2) : 0;
            var totalCur = holdingResponses.Any() ? Math.Round(holdingResponses.Sum(h => h.Quantity * h.CurrentPrice), 2) : 0;
            var totalPnl = Math.Round(totalCur - totalInv, 2);
            var totalPnlPct = totalInv > 0 ? Math.Round((totalPnl / totalInv) * 100, 2) : 0;

            return Ok(new PortfolioSummaryResponse
            {
                UserId = userId,
                TotalHoldings = holdingResponses.Count,
                TotalInvested = totalInv,
                CurrentValue = totalCur,
                TotalPnl = totalPnl,
                TotalPnlPercent = totalPnlPct,
                Holdings = holdingResponses,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal Portfolio Summary Error: {ex}");
            return StatusCode(500, $"Internal Server Error: {ex.Message}");
        }
    }

    // 🔥 HELPER: Encapsulates the logic for a single stock with defensive error handling
    private async Task<HoldingResponse> ProcessSingleHolding(Holding h)
    {
        decimal livePrice = h.AvgBuyPrice;
        string? marketCapLabel = null;

        try
        {
            // 1. Fetch Price and DB Data in parallel for this specific stock
            var priceTask = _priceService.GetLivePriceAsync(h.Symbol);
            var fundamentalTask = _db.Stocks.FirstOrDefaultAsync(s => s.Symbol == h.Symbol);

            // Add a safety timeout of 8 seconds per stock so Yahoo doesn't hang the app
            var timeoutTask = Task.Delay(8000); 
            var completedTask = await Task.WhenAny(Task.WhenAll(priceTask, fundamentalTask), timeoutTask);

            if (completedTask != timeoutTask)
            {
                livePrice = await priceTask;
                var fundamental = await fundamentalTask;

                if (livePrice <= 0) livePrice = h.AvgBuyPrice;
                if (fundamental != null)
                {
                    marketCapLabel = GetMarketCapLabel(ParseMarketCap(fundamental.MarketCap));
                }
            }
            else
            {
                Console.WriteLine($"Timeout fetching data for {h.Symbol}. Using fallback values.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {h.Symbol}: {ex.Message}");
        }

        decimal unrealizedPnl = CalculatePnl(h.Quantity, h.AvgBuyPrice, livePrice);
        decimal pnlPercent = h.AvgBuyPrice > 0 ? Math.Round(((livePrice - h.AvgBuyPrice) / h.AvgBuyPrice) * 100, 2) : 0;

        return new HoldingResponse(
            h.Id,
            h.Symbol,
            h.Quantity,
            h.AvgBuyPrice,
            livePrice,
            unrealizedPnl,
            h.BuyDate,
            0.00m, // Change1D - can be enhanced later
            pnlPercent,
            h.Tags ?? "Equity",
            marketCapLabel
        );
    }

    [HttpGet("analysis")]
    public async Task<IActionResult> AnalyzeCurrentUser([FromQuery] string userId)
    {
        var summaryResult = await GetSummary(userId) as OkObjectResult;
        if (summaryResult?.Value is PortfolioSummaryResponse summary)
        {
            var healthResult = _health.Analyze(userId, summary.Holdings);
            var symbols = summary.Holdings.Select(h => h.Symbol).ToList();
            var sparklineMap = await _priceService.GetBatchSparklinesAsync(symbols);

            foreach (var pos in healthResult.Positions)
            {
                if (sparklineMap.TryGetValue(pos.Symbol, out var trend))
                {
                    pos.History = trend;
                }
            }
            return Ok(healthResult);
        }
        return BadRequest("Could not analyze portfolio.");
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("User not found.");

        var sectors = (user.PreferredSectors ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
    public async Task<IActionResult> GetHighInfusion() => Ok(await _marketService.GetHighInfusionStocksAsync());

    [HttpGet("ticker")]
    public async Task<IActionResult> GetTicker() => Ok(await _marketService.GetTickerDataAsync());

    [HttpDelete("holding/{id}")]
    public async Task<IActionResult> RemoveHolding(string id)
    {
        var holding = await _db.Holdings.FindAsync(id);
        if (holding == null) return NotFound();
        _db.Holdings.Remove(holding);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Asset removed" });
    }

    private double ParseMarketCap(string? mCapStr)
    {
        if (string.IsNullOrEmpty(mCapStr)) return 0;
        string cleanValue = mCapStr.Replace("Cr", "", StringComparison.OrdinalIgnoreCase).Replace(",", "").Trim();
        double.TryParse(cleanValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val);
        return val;
    }

    private string? GetMarketCapLabel(double mCapCr)
    {
        if (mCapCr <= 0) return null;
        if (mCapCr >= 20000) return "LARGE-CAP";
        if (mCapCr >= 5000) return "MID-CAP";
        if (mCapCr >= 500) return "SMALL-CAP";
        return "MICRO-CAP";
    }

    private decimal CalculatePnl(decimal quantity, decimal avgPrice, decimal currentPrice) => Math.Round(quantity * (currentPrice - avgPrice), 2);

    public record PortfolioHistoryPoint(DateTime Date, decimal Value);
}