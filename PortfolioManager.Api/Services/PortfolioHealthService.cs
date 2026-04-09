using System.Collections.Generic;
using System.Linq;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class PortfolioHealthService
{
    public PortfolioHealthResult Analyze(string userId, List<HoldingResponse> holdings)
    {
        if (holdings.Count == 0)
        {
            return new PortfolioHealthResult(
                userId,
                0,
                0,
                0,
                0,
                0,
                "Weak",
                new(),
                new() { "No holdings found. Add some positions to start analysis." }
            );
        }

        // Minimal change: Ensure rounding is applied to prevent floating point issues in JSON serialization
        var totalInvested = Math.Round(holdings.Sum(h => h.Quantity * h.AvgBuyPrice), 2);
        var currentValue = Math.Round(holdings.Sum(h => h.Quantity * h.CurrentPrice), 2);
        var totalPnl = Math.Round(currentValue - totalInvested, 2);
        var totalPnlPct = totalInvested == 0 ? 0 : Math.Round((totalPnl / totalInvested) * 100m, 2);

        var positions = new List<PositionAdvice>();
        foreach (var h in holdings)
        {
            var invested = h.Quantity * h.AvgBuyPrice;
            var pnl = h.Quantity * (h.CurrentPrice - h.AvgBuyPrice);
            var pnlPct = invested == 0 ? 0 : Math.Round((pnl / invested) * 100m, 2);

            string action =
                pnlPct <= -30 ? "SELL_FAST"
                : pnlPct <= -10 ? "GRADUAL_SELL"
                : pnlPct >= 30 ? "GRADUAL_SELL"
                : pnlPct >= 10 ? "HOLD"
                : "HOLD";

            string reason =
                pnlPct <= -30 ? "Large unrealized loss (>30%). Consider cutting quickly."
                : pnlPct <= -10 ? "Moderate loss (10–30%). Exit slowly or on bounces."
                : pnlPct >= 30 ? "Large gains (>30%). Take profit gradually."
                : pnlPct >= 10 ? "Reasonable gain (10–30%). Consider trailing stop."
                : "Small move; no immediate action.";

            positions.Add(
                new PositionAdvice(
                    h.Id,
                    h.Symbol,
                    h.Quantity,
                    h.AvgBuyPrice,
                    h.CurrentPrice,
                    pnlPct,
                    action,
                    reason,
                    h.MarketCapLabel, // Carried from Controller safely
                    new List<decimal>()
                )
            );
        }

        var weights = holdings
            .Select(h =>
                (
                    h.Symbol,
                    Weight: Math.Round(
                        (h.Quantity * h.CurrentPrice)
                            / (currentValue == 0 ? 1 : currentValue)
                            * 100m,
                        2
                    )
                )
            )
            .ToList();

        var maxWeight = weights.Any() ? weights.Max(w => w.Weight) : 0;
        var highlyConcentrated = maxWeight > 25m;

        int score = 50;
        if (totalPnlPct >= 20)
            score += 20;
        else if (totalPnlPct <= -20)
            score -= 20;
        if (highlyConcentrated)
            score -= 15;
        score = Math.Max(0, Math.Min(100, score));

        string band =
            score >= 70 ? "Good"
            : score >= 50 ? "Moderate"
            : "Weak";

        var warnings = new List<string>();
        if (highlyConcentrated)
        {
            var top = weights.OrderByDescending(w => w.Weight).First();
            warnings.Add(
                $"High concentration: {top.Symbol} is {top.Weight:F1}% of your portfolio."
            );
        }
        if (totalPnlPct < 0)
        {
            warnings.Add($"Overall portfolio is at a loss of {Math.Abs(totalPnlPct):F1}%.");
        }

        return new PortfolioHealthResult(
            userId,
            totalInvested,
            currentValue,
            totalPnl,
            totalPnlPct,
            score,
            band,
            positions,
            warnings
        );
    }

    public List<RecommendedStock> SuggestStocks(string riskProfile, string[] preferredSectors)
    {
        var list = new List<RecommendedStock>();

        if (preferredSectors.Contains("IT", StringComparer.OrdinalIgnoreCase))
        {
            list.Add(
                new RecommendedStock(
                    "TCS",
                    "Large-cap IT with strong profitability.",
                    riskProfile == "High" ? 10 : 5
                )
            );
            list.Add(
                new RecommendedStock(
                    "INFY",
                    "Well-established IT services company with stable earnings.",
                    riskProfile == "Moderate" ? 8 : 4
                )
            );
        }

        if (riskProfile == "High")
        {
            list.Add(
                new RecommendedStock("MIDCAP_IT", "Higher-volatility, high-growth IT midcaps.", 5)
            );
        }

        return list;
    }
}
