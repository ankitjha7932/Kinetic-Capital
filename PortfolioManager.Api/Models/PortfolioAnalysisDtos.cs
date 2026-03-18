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

public record PositionAdvice(
    string HoldingId,
    string Symbol,
    decimal Quantity,
    decimal AvgBuyPrice,
    decimal CurrentPrice,
    decimal PnlPercent,
    string Action,
    string Reason
);

public record RecommendedStock(string Symbol, string Rationale, decimal SuggestedAllocationPercent);
