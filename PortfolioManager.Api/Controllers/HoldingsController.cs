using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class HoldingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public HoldingsController(AppDbContext db) => _db = db;

    // Helper to get ID from Token
    private string GetUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    // 1. GET /api/holdings/me (Replaces the by-user/{userId} route)
    [HttpGet("me")]
    public async Task<IActionResult> GetMyHoldings()
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var holdings = await _db.Holdings
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var responses = holdings.Select(h => new HoldingResponse(
            h.Id, h.Symbol, h.Quantity, h.AvgBuyPrice,
            2678m, // Simulated live price
            h.Quantity * (2678m - h.AvgBuyPrice),
            h.BuyDate, h.Tags
        )).ToList();

        return Ok(responses);
    }

    // 2. POST /api/holdings
    [HttpPost]
    public async Task<IActionResult> CreateHolding([FromBody] HoldingRequest request)
    {
        string userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        //check that if in case db got refresh and we don't have that user in table, FE should not allow that user to add stock
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return BadRequest("User session expired or database reset. Please re-login.");
        }

        var holding = new Holding
        {
            UserId = userId,
            Symbol = request.Symbol.ToUpper(),
            Quantity = request.Quantity,
            AvgBuyPrice = request.AvgBuyPrice,
            BuyDate = request.PurchaseDate ?? DateTime.UtcNow,
            Tags = request.Tags ?? ""
        };

        _db.Holdings.Add(holding);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHolding), new { id = holding.Id }, holding);
    }

    // 3. PUT /api/holdings/1 (Added Security: Must own the holding)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHolding(int id, [FromBody] HoldingUpdateRequest request)
    {
        var holding = await _db.Holdings.FindAsync(id);

        // Security Check: Does this holding exist AND belong to the logged-in user?
        if (holding == null || holding.UserId != GetUserId()) return NotFound();

        holding.Quantity = request.Quantity;
        holding.AvgBuyPrice = request.AvgBuyPrice;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // 4. DELETE /api/holdings/1 (Added Security: Must own the holding)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHolding(int id)
    {
        var holding = await _db.Holdings.FindAsync(id);

        // Security Check
        if (holding == null || holding.UserId != GetUserId()) return NotFound();

        _db.Holdings.Remove(holding);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/holdings/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHolding(int id)
    {
        var holding = await _db.Holdings.FindAsync(id);
        if (holding == null || holding.UserId != GetUserId()) return NotFound();

        return Ok(new HoldingResponse(
            holding.Id, holding.Symbol, holding.Quantity, holding.AvgBuyPrice,
            2500m, holding.Quantity * (2500m - holding.AvgBuyPrice),
            holding.BuyDate, holding.Tags
        ));
    }
}