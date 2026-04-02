using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HoldingsController : ControllerBase
{
    private readonly IMongoCollection<Holding> _holdings;
    private readonly StockPriceService _priceService;

    public HoldingsController(IMongoDatabase database, StockPriceService priceService)
    {
        _holdings = database.GetCollection<Holding>("Holdings");
        _priceService = priceService;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet("me")]
    public async Task<IActionResult> GetMyHoldings()
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var holdings = await _holdings.Find(h => h.UserId == userId).ToListAsync();

        var symbols = holdings.Select(h => h.Symbol).ToList();
        var priceMap = await _priceService.GetBatchPricesAsync(symbols);

        var responses = holdings
            .Select(h =>
            {
                // Get price from map, fallback to buy price if missing
                priceMap.TryGetValue(h.Symbol, out decimal currentPrice);
                if (currentPrice <= 0)
                    currentPrice = h.AvgBuyPrice;

                decimal pnl = h.Quantity * (currentPrice - h.AvgBuyPrice);
                decimal pnlPercent =
                    h.AvgBuyPrice > 0 ? (currentPrice - h.AvgBuyPrice) / h.AvgBuyPrice * 100 : 0;

                return new HoldingResponse(
                    h.Id!,
                    h.Symbol,
                    h.Quantity,
                    h.AvgBuyPrice,
                    currentPrice,
                    Math.Round(pnl, 2),
                    h.BuyDate,
                    0.5m, // Change1D Placeholder
                    Math.Round(pnlPercent, 2),
                    h.Tags ?? "Equity",
                    "N/A" // MarketCapLabel Placeholder
                );
            })
            .ToList();

        return Ok(responses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHolding([FromBody] HoldingRequest request)
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var symbol = request.Symbol.ToUpper().Trim();
        var existingHolding = await _holdings
            .Find(h => h.UserId == userId && h.Symbol == symbol)
            .FirstOrDefaultAsync();

        if (existingHolding != null)
        {
            decimal totalQuantity = existingHolding.Quantity + request.Quantity;
            decimal newAvgPrice =
                (
                    (existingHolding.Quantity * existingHolding.AvgBuyPrice)
                    + (request.Quantity * request.AvgBuyPrice)
                ) / totalQuantity;

            var update = Builders<Holding>
                .Update.Set(h => h.Quantity, totalQuantity)
                .Set(h => h.AvgBuyPrice, Math.Round(newAvgPrice, 2))
                .Set(h => h.BuyDate, DateTime.UtcNow);

            await _holdings.UpdateOneAsync(h => h.Id == existingHolding.Id, update);
            return Ok(
                new
                {
                    message = "Holding updated",
                    symbol,
                    totalQuantity,
                }
            );
        }

        var holding = new Holding
        {
            UserId = userId,
            Symbol = symbol,
            Quantity = request.Quantity,
            AvgBuyPrice = request.AvgBuyPrice,
            BuyDate = request.PurchaseDate ?? DateTime.UtcNow,
            Tags = request.Tags ?? "Equity",
        };

        await _holdings.InsertOneAsync(holding);
        return CreatedAtAction(nameof(GetHolding), new { id = holding.Id }, holding);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHolding(string id)
    {
        string userIdString = GetUserId();
        if (!ObjectId.TryParse(id, out _) || !ObjectId.TryParse(userIdString, out _))
            return BadRequest(new { message = "Invalid ID format" });

        var result = await _holdings.DeleteOneAsync(h => h.Id == id && h.UserId == userIdString);
        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHolding(string id)
    {
        string userId = GetUserId();
        var h = await _holdings.Find(h => h.Id == id && h.UserId == userId).FirstOrDefaultAsync();
        if (h == null)
            return NotFound();

        decimal currentPrice = await _priceService.GetLivePriceAsync(h.Symbol);
        if (currentPrice <= 0)
            currentPrice = h.AvgBuyPrice;

        decimal pnl = h.Quantity * (currentPrice - h.AvgBuyPrice);
        decimal pnlPercent =
            h.AvgBuyPrice > 0 ? (currentPrice - h.AvgBuyPrice) / h.AvgBuyPrice * 100 : 0;

        return Ok(
            new HoldingResponse(
                h.Id!,
                h.Symbol,
                h.Quantity,
                h.AvgBuyPrice,
                currentPrice,
                Math.Round(pnl, 2),
                h.BuyDate,
                -1.2m,
                Math.Round(pnlPercent, 2),
                h.Tags ?? "Equity",
                "N/A"
            )
        );
    }
}
