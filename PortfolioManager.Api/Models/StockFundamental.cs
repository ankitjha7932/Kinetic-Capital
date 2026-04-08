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

    [BsonElement("PeersData")]
    public List<PeerData>? PeersData { get; set; } = new();

    [BsonElement("PeerSymbols")]
    public List<string>? PeerSymbols { get; set; } = new();

    public List<ShareholdingData> Shareholding { get; set; } = new();
    public List<ShareholdingData> ShareholdingYearly { get; set; } = new();

    public string ScreenerId { get; set; } = string.Empty;

    [BsonElement("TradeId")]
    public string? TradeId { get; set; }

    [BsonElement("Trades")]
    public TradesContainer? Trades { get; set; }

    public DateTime? LastUpserted { get; set; }

    [BsonElement("LastTradesUpdate")]
    public DateTime? LastTradesUpdate { get; set; }
}

[BsonIgnoreExtraElements]
public class TradesContainer
{
    [BsonElement("Insider")]
    public List<InsiderTrade> Insider { get; set; } = new();

    [BsonElement("Bulk")]
    public List<BulkBlockTrade> Bulk { get; set; } = new();

    [BsonElement("Block")]
    public List<BulkBlockTrade> Block { get; set; } = new();

    [BsonElement("Sast")]
    public List<SignificantOwnershipTrade> Sast { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class InsiderTrade
{
    public string Date { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string AvgPrice { get; set; } = string.Empty;
    public string ValueLacs { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class BulkBlockTrade
{
    public string Date { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class SignificantOwnershipTrade
{
    public string Date { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public string Transaction { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Percent { get; set; } = string.Empty;
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

[BsonIgnoreExtraElements]
public class ShareholdingData
{
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
}
