using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HoldingsController : ControllerBase
{
    private readonly IMongoCollection<Holding> _holdings;
    private readonly IMongoCollection<User> _users;

    public HoldingsController(IMongoDatabase database)
    {
        _holdings = database.GetCollection<Holding>("Holdings");
        _users = database.GetCollection<User>("Users");
    }

    private string GetUserId()
    {
        var userId =
            User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return userId ?? "";
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyHoldings()
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var holdings = await _holdings.Find(h => h.UserId == userId).ToListAsync();

        var responses = holdings
            .Select(h => new HoldingResponse(
                h.Id!,
                h.Symbol,
                h.Quantity,
                h.AvgBuyPrice,
                2678m,
                h.Quantity * (2678m - h.AvgBuyPrice),
                h.BuyDate,
                h.Tags ?? ""
            ))
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

        // Check if the user already owns this stock -> n stock A already present with some avg price, m stocks of A added, then we will calc it accordingly
        var existingHolding = await _holdings
            .Find(h => h.UserId == userId && h.Symbol == symbol)
            .FirstOrDefaultAsync();

        if (existingHolding != null)
        {
            decimal totalQuantity = existingHolding.Quantity + request.Quantity;

            // Weighted Average Price Calculation
            decimal currentTotalValue = existingHolding.Quantity * existingHolding.AvgBuyPrice;
            decimal newPurchaseValue = request.Quantity * request.AvgBuyPrice;

            decimal newAvgPrice = (currentTotalValue + newPurchaseValue) / totalQuantity;

            // Update existing record
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
        else
        {
            //nhi hai -> create new record
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
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHolding(
        string id,
        [FromBody] HoldingUpdateRequest request
    )
    {
        if (!ObjectId.TryParse(id, out _))
            return BadRequest("Invalid ID format.");

        string userId = GetUserId();
        var holding = await _holdings
            .Find(h => h.Id == id && h.UserId == userId)
            .FirstOrDefaultAsync();

        if (holding == null)
            return NotFound();

        holding.Quantity = request.Quantity;
        holding.AvgBuyPrice = request.AvgBuyPrice;

        await _holdings.ReplaceOneAsync(h => h.Id == id, holding);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHolding(string id)
    {
        Console.WriteLine("--- Incoming Delete Request Claims ---");
        foreach (var c in User.Claims)
        {
            Console.WriteLine($"CLAIM: {c.Type} = {c.Value}");
        }

        string userIdString = GetUserId();
        Console.WriteLine($"Extracted UserId: '{userIdString}'");

        if (!MongoDB.Bson.ObjectId.TryParse(id, out var holdingObjectId))
        {
            return BadRequest(new { message = $"Invalid Holding ID format: {id}" });
        }

        if (!MongoDB.Bson.ObjectId.TryParse(userIdString, out var userObjectId))
        {
            return Unauthorized(
                new { message = $"User session invalid. ID in token is: '{userIdString}'" }
            );
        }

        try
        {
            var filter = Builders<Holding>.Filter.And(
                Builders<Holding>.Filter.Eq("_id", holdingObjectId),
                Builders<Holding>.Filter.Eq("UserId", userObjectId)
            );

            var result = await _holdings.DeleteOneAsync(filter);

            if (result.DeletedCount == 0)
            {
                return NotFound(
                    new { message = "Holding not found or you don't have permission to delete it." }
                );
            }

            return NoContent(); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error: {ex.Message}");
            return BadRequest(new { message = "An error occurred while deleting." });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHolding(string id)
    {
        if (!ObjectId.TryParse(id, out _))
            return BadRequest("Invalid ID format.");

        string userId = GetUserId();
        var holding = await _holdings
            .Find(h => h.Id == id && h.UserId == userId)
            .FirstOrDefaultAsync();

        if (holding == null)
            return NotFound();

        return Ok(
            new HoldingResponse(
                holding.Id!,
                holding.Symbol,
                holding.Quantity,
                holding.AvgBuyPrice,
                2500m,
                holding.Quantity * (2500m - holding.AvgBuyPrice),
                holding.BuyDate,
                holding.Tags ?? ""
            )
        );
    }
}
