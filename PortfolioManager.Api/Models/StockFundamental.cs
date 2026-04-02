using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Api.Models;

[BsonIgnoreExtraElements]
public class FinancialRow
{
    public string Metric { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class StockFundamental
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Symbol { get; set; } = null!;
    public string Industry { get; set; } = "N/A";
    public string MarketCap { get; set; } = "N/A";
    public string StockPE { get; set; } = "N/A";
    public string ROCE { get; set; } = "N/A";
    public string ROE { get; set; } = "N/A";
    public string DividendYield { get; set; } = "N/A";
    public string BookValue { get; set; } = "N/A";
    public string? FaceValue { get; set; } = "N/A";
    public string CompanyName { get; set; } = "N/A";

    public List<FinancialRow> QuarterlyResults { get; set; } = new();
    public List<FinancialRow> ProfitAndLoss { get; set; } = new();
    public List<FinancialRow> BalanceSheet { get; set; } = new();
    public List<FinancialRow> CashFlow { get; set; } = new();

    // --- UPDATED PART ---
    // Making these nullable (?) is critical for MongoDB backward compatibility
    [BsonElement("PeersData")]
    public List<PeerData>? PeersData { get; set; } = new();

    [BsonElement("PeerSymbols")]
    public List<string>? PeerSymbols { get; set; } = new();
    // --------------------

    public List<ShareholdingData> Shareholding { get; set; } = new();
    public List<ShareholdingData> ShareholdingYearly { get; set; } = new();
    public string ScreenerId { get; set; } = string.Empty;
    public DateTime? LastUpserted { get; set; }
}

[BsonIgnoreExtraElements]
public class PeerData
{
    public string Name { get; set; } = null!;
    public string? Symbol { get; set; }
    public string PE { get; set; } = string.Empty;
    public string MarketCap { get; set; } = string.Empty;
    public string DivYield { get; set; } = string.Empty;
    public string NetProfitQtr { get; set; } = string.Empty;
    public string ProfitVarQtr { get; set; } = string.Empty;
    public string SalesQtr { get; set; } = string.Empty;
    public string SalesVarQtr { get; set; } = string.Empty;
    public string ROCE { get; set; } = string.Empty;
}

public class ShareholdingData
{
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
}