using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Api.Models;

public class IndexMapping
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string IndexName { get; set; } = string.Empty;
    public List<string> Symbols { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
