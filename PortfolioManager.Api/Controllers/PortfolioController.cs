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
        try
        {
            var holdings = await _db.Holdings.Where(h => h.UserId == userId).ToListAsync();

            var holdingResponses = new List<HoldingResponse>();

            foreach (var h in holdings)
            {
                decimal livePrice = await _priceService.GetLivePriceAsync(h.Symbol);

                if (livePrice <= 0)
                    livePrice = h.AvgBuyPrice;

                // Fetch frm StockFundmental COllection
                var fundamental = await _db.Stocks.FirstOrDefaultAsync(s => s.Symbol == h.Symbol);

                double mCapValue = 0;
                if (fundamental != null && !string.IsNullOrEmpty(fundamental.MarketCap))
                {
                    string cleanValue = fundamental
                        .MarketCap.Replace("Cr", "", StringComparison.OrdinalIgnoreCase)
                        .Replace(",", "")
                        .Trim();

                    double.TryParse(
                        cleanValue,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out mCapValue
                    );
                }

                string? marketCapLabel = GetMarketCapLabel(mCapValue);

                holdingResponses.Add(
                    new HoldingResponse(
                        h.Id,
                        h.Symbol,
                        h.Quantity,
                        h.AvgBuyPrice,
                        livePrice,
                        CalculatePnl(h.Quantity, h.AvgBuyPrice, livePrice),
                        h.BuyDate,
                        h.Tags ?? "",
                        marketCapLabel
                    )
                );
            }

            var totalInvested = Math.Round(
                holdingResponses.Sum(h => h.Quantity * h.AvgBuyPrice),
                2
            );
            var currentValue = Math.Round(
                holdingResponses.Sum(h => h.Quantity * h.CurrentPrice),
                2
            );

            return Ok(
                new PortfolioSummaryResponse
                {
                    UserId = userId,
                    TotalHoldings = holdingResponses.Count,
                    TotalInvested = totalInvested,
                    CurrentValue = currentValue,
                    TotalPnl = Math.Round(currentValue - totalInvested, 2),
                    Holdings = holdingResponses,
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating summary: {ex.Message}");
        }
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

    // api/portfolio/analysis?userId={id}
    [HttpGet("analysis")]
    public async Task<IActionResult> AnalyzeCurrentUser([FromQuery] string userId)
    {
        var holdings = await _db.Holdings.Where(h => h.UserId == userId).ToListAsync();

        var holdingResponses = new List<HoldingResponse>();

        foreach (var h in holdings)
        {
            decimal livePrice = await _priceService.GetLivePriceAsync(h.Symbol);
            if (livePrice <= 0)
                livePrice = h.AvgBuyPrice;

            var fundamental = await _db.Stocks.FirstOrDefaultAsync(s => s.Symbol == h.Symbol);
            double mCapValue = 0;

            if (fundamental != null && !string.IsNullOrEmpty(fundamental.MarketCap))
            {
                string cleanValue = fundamental
                    .MarketCap.Replace("Cr", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(",", "")
                    .Trim();
                double.TryParse(
                    cleanValue,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out mCapValue
                );
            }

            string? marketCapLabel = GetMarketCapLabel(mCapValue);

            holdingResponses.Add(
                new HoldingResponse(
                    h.Id,
                    h.Symbol,
                    h.Quantity,
                    h.AvgBuyPrice,
                    livePrice,
                    CalculatePnl(h.Quantity, h.AvgBuyPrice, livePrice),
                    h.BuyDate,
                    h.Tags ?? "",
                    marketCapLabel
                )
            );
        }

        var result = _health.Analyze(userId, holdingResponses);
        return Ok(result);
    }

    // api/portfolio/suggestions?userId={id}
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound("User not found.");

        var sectorString = user.PreferredSectors ?? "";
        var sectors = sectorString.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        var result = _health.SuggestStocks(user.RiskProfile ?? "Moderate", sectors);
        return Ok(result);
    }

    // api/portfolio/price/{symbol}
    [HttpGet("price/{symbol}")]
    public async Task<IActionResult> GetSinglePrice(string symbol)
    {
        decimal price = await _priceService.GetLivePriceAsync(symbol);
        if (price <= 0)
            return NotFound("Could not fetch price for this symbol.");

        return Ok(new { Symbol = symbol, Price = price });
    }

    [HttpGet("news/{symbol}")]
    public async Task<IActionResult> GetNews(string symbol)
    {
        var news = await _newsService.GetStockNewsAsync(symbol);

        if (news == null || !news.Any())
            return NotFound(new { message = "No news found for this symbol." });

        return Ok(news);
    }

    [HttpGet("high-infusion")]
    public async Task<IActionResult> GetHighInfusion()
    {
        Console.WriteLine(">>> HIT: portfolio/high-infusion endpoint called");
        var result = await _marketService.GetHighInfusionStocksAsync();
        return Ok(result);
    }

    [HttpGet("ticker")]
    public async Task<IActionResult> GetTicker()
    {
        return Ok(await _marketService.GetTickerDataAsync());
    }

    private decimal CalculatePnl(decimal quantity, decimal avgPrice, decimal currentPrice) =>
        Math.Round(quantity * (currentPrice - avgPrice), 2);
}
