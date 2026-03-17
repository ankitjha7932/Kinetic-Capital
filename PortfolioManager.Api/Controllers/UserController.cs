using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    public UserController(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("Users");
    }

    // GET: api/user/profile/{id}
    [HttpGet("profile/{id}")]
    public async Task<IActionResult> GetProfile(string id)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            return BadRequest(new { message = "Invalid ID format" });

        var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    // PUT: api/user/profile/{id}
    [HttpPut("profile/{id}")]
    public async Task<IActionResult> UpdateProfile(
        string id,
        [FromBody] ProfileUpdateRequest request
    )
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            return BadRequest(new { message = "Invalid ID format" });

        var filter = Builders<User>.Filter.Eq("_id", objectId);

        var update = Builders<User>
            .Update.Set(u => u.RiskProfile, request.RiskProfile)
            .Set(u => u.FullName, request.FullName)
            .Set(u => u.InvestmentHorizon, request.InvestmentHorizon)
            .Set(u => u.PreferredSectors, request.PreferredSectors);

        var result = await _users.UpdateOneAsync(filter, update);

        if (result.MatchedCount == 0)
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "Profile Synced", modified = result.ModifiedCount > 0 });
    }
}

public record ProfileUpdateRequest(
    string RiskProfile,
    int InvestmentHorizon,
    string PreferredSectors,
    string FullName
);
