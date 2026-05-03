using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    // ── Resolves userId from JWT claim OR query param (/me?userId=xxx)
    private string GetUserId(string? bodyUserId = null)
    {
        // 1. Try JWT claims (works when [Authorize] is applied)
        var claimId =
            User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(claimId))
            return claimId;

        // 2. Passed in from request body
        if (!string.IsNullOrEmpty(bodyUserId))
            return bodyUserId;

        // 3. Query string
        var queryId = Request.Query["userId"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryId))
            return queryId;

        // 4. Manually parse Authorization header (for [AllowAnonymous] endpoints)
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            try
            {
                var token = authHeader.Substring(7);
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                if (!string.IsNullOrEmpty(sub))
                    return sub;
            }
            catch { }
        }

        return "";
    }

    [HttpGet("me")]
    [AllowAnonymous]
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
                    0.5m,
                    Math.Round(pnlPercent, 2),
                    h.Tags ?? "Equity",
                    "N/A"
                );
            })
            .ToList();

        return Ok(responses);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateHolding([FromBody] HoldingRequest request)
    {
        // Accept userId from body (frontend sends it there) or JWT
        string userId = GetUserId(request.UserId);
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
        // Bust sparkline cache so the new symbol gets fetched on next analysis call
        _priceService.InvalidateSparklineCache();
        return CreatedAtAction(nameof(GetHolding), new { id = holding.Id }, holding);
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteHolding(string id)
    {
        string userId = GetUserId();
        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { message = "Invalid ID format" });

        // Accept userId from query string for delete: DELETE /api/holdings/{id}?userId=xxx
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _holdings.DeleteOneAsync(h => h.Id == id && h.UserId == userId);
        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHolding(string id)
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

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
