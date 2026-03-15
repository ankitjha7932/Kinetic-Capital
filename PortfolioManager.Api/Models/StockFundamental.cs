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
    public string FaceValue { get; set; } = "N/A";

    public List<FinancialRow> QuarterlyResults { get; set; } = new();
    public List<FinancialRow> ProfitAndLoss { get; set; } = new();
    public List<FinancialRow> BalanceSheet { get; set; } = new();
    public List<FinancialRow> CashFlow { get; set; } = new();
    public List<PeerData> Peers { get; set; } = new();
}

public class PeerData
{
    public string Name { get; set; } = null!;
    public string Price { get; set; } = null!;
    public string PE { get; set; } = null!;
    public string MarketCap { get; set; } = null!;
    public string ROCE { get; set; } = null!;
}