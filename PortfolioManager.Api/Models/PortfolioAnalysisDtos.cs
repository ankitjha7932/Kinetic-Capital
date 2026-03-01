using System.Collections.Generic;

namespace PortfolioManager.Api.Models;

public record PortfolioHealthResult(
    int UserId,
    decimal TotalInvested,
    decimal CurrentValue,
    decimal TotalPnl,
    decimal TotalPnlPercent,
    int Score,                          // 0–100
    string RatingBand,                  // "Weak", "Moderate", "Good"
    List<PositionAdvice> Positions,     // per holding advice
    List<string> Warnings               // human-readable issues
);

public record PositionAdvice(
    int HoldingId,
    string Symbol,
    decimal Quantity,
    decimal AvgBuyPrice,
    decimal CurrentPrice,
    decimal PnlPercent,
    string Action,                      // "SELL_FAST", "GRADUAL_SELL", "HOLD", "ACCUMULATE"
    string Reason
);

public record RecommendedStock(
    string Symbol,
    string Rationale,
    decimal SuggestedAllocationPercent
);
