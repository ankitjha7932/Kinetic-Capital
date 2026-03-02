using PortfolioManager.Api.Models;
using System.Collections.Generic;
namespace PortfolioManager.Api.Services;

public class PortfolioHealthService
{
    public PortfolioHealthResult Analyze(string userId, List<HoldingResponse> holdings)
    {
        if (holdings.Count == 0)
        {
            return new PortfolioHealthResult(
                userId,
                0, 0, 0, 0,
                Score: 0,
                RatingBand: "Weak",
                Positions: new(),
                Warnings: new() { "No holdings found. Add some positions to start analysis." }
            );
        }

        var totalInvested = holdings.Sum(h => h.Quantity * h.AvgBuyPrice);
        var currentValue = holdings.Sum(h => h.Quantity * h.CurrentPrice);
        var totalPnl = currentValue - totalInvested;
        var totalPnlPct = totalInvested == 0 ? 0 : (totalPnl / totalInvested) * 100m;

        // Per-position advice
        var positions = new List<PositionAdvice>();
        foreach (var h in holdings)
        {
            var invested = h.Quantity * h.AvgBuyPrice;
            var pnl = h.Quantity * (h.CurrentPrice - h.AvgBuyPrice);
            var pnlPct = invested == 0 ? 0 : (pnl / invested) * 100m;

            string action;
            string reason;

            if (pnlPct <= -30)
            {
                action = "SELL_FAST";
                reason = "Large unrealized loss (>30%). Consider cutting quickly.";
            }
            else if (pnlPct <= -10)
            {
                action = "GRADUAL_SELL";
                reason = "Moderate loss (10–30%). Exit slowly or on bounces.";
            }
            else if (pnlPct >= 30)
            {
                action = "GRADUAL_SELL";
                reason = "Large gains (>30%). Take profit gradually.";
            }
            else if (pnlPct >= 10)
            {
                action = "HOLD";
                reason = "Reasonable gain (10–30%). Consider trailing stop.";
            }
            else
            {
                action = "HOLD";
                reason = "Small move; no immediate action.";
            }

            positions.Add(new PositionAdvice(
                h.Id,
                h.Symbol,
                h.Quantity,
                h.AvgBuyPrice,
                h.CurrentPrice,
                pnlPct,
                action,
                reason
            ));
        }

        // Concentration risk: max single-position weight
        var weights = holdings
            .Select(h => (h.Symbol, Weight: (h.Quantity * h.CurrentPrice) / (currentValue == 0 ? 1 : currentValue) * 100m))
            .ToList();

        var maxWeight = weights.Max(w => w.Weight);
        var highlyConcentrated = maxWeight > 25m; // arbitrary

        // Simple scoring: based on totalPnlPct and concentration
        int score = 50; // start neutral

        if (totalPnlPct >= 20) score += 20;
        else if (totalPnlPct >= 5) score += 10;
        else if (totalPnlPct <= -20) score -= 20;
        else if (totalPnlPct <= -5) score -= 10;

        if (highlyConcentrated) score -= 15;

        // Clamp score
        score = Math.Max(0, Math.Min(100, score));

        string band =
            score >= 70 ? "Good" :
            score >= 50 ? "Moderate" :
            "Weak";

        var warnings = new List<string>();
        if (highlyConcentrated)
        {
            var top = weights.OrderByDescending(w => w.Weight).First();
            warnings.Add($"High concentration: {top.Symbol} is {top.Weight:F1}% of your portfolio.");
        }
        if (totalPnlPct < 0)
        {
            warnings.Add($"Overall portfolio is at a loss of {totalPnlPct:F1}%.");
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

        // for now just hardcode it – replace later with API-based screener
        if (preferredSectors.Contains("IT", StringComparer.OrdinalIgnoreCase))
        {
            list.Add(new RecommendedStock(
                "TCS",
                "Large-cap IT with strong profitability; suitable for moderate to low risk profiles.",
                riskProfile == "High" ? 10 : 5
            ));
            list.Add(new RecommendedStock(
                "INFY",
                "Well-established IT services company with stable earnings.",
                riskProfile == "Moderate" ? 8 : 4
            ));
        }

        if (riskProfile == "High")
        {
            list.Add(new RecommendedStock(
                "MIDCAP_IT",
                "Higher-volatility, high-growth IT midcaps for aggressive allocation.",
                5
            ));
        }

        return list;
    }
}
