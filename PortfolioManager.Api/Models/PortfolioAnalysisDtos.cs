using System.Collections.Generic;

namespace PortfolioManager.Api.Models;

public record PortfolioHealthResult(
    string UserId,
    decimal TotalInvested,
    decimal CurrentValue,
    decimal TotalPnl,
    decimal TotalPnlPercent,
    int Score,
    string RatingBand,
    List<PositionAdvice> Positions,
    List<string> Warnings
);

public class PositionAdvice
{
    public string HoldingId { get; set; }
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgBuyPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal PnlPercent { get; set; }
    public string Action { get; set; }
    public string Reason { get; set; }
    public string? MarketCapLabel { get; set; }
    public List<decimal> History { get; set; } = new();

    public PositionAdvice(
        string holdingId,
        string symbol,
        decimal quantity,
        decimal avgBuyPrice,
        decimal currentPrice,
        decimal pnlPercent,
        string action,
        string reason,
        string? marketCapLabel,
        List<decimal> history
    )
    {
        HoldingId = holdingId;
        Symbol = symbol;
        Quantity = quantity;
        AvgBuyPrice = avgBuyPrice;
        CurrentPrice = currentPrice;
        PnlPercent = pnlPercent;
        Action = action;
        Reason = reason;
        MarketCapLabel = marketCapLabel;
        History = history ?? new List<decimal>();
    }
}

public record RecommendedStock(string Symbol, string Rationale, decimal SuggestedAllocationPercent);
