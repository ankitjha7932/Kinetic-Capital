using System;
using System.Collections.Generic;
using System.Linq;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services
{
    public interface IStockAnalysisService
    {
        StockAnalysisResult AnalyzeStock(StockDetails details);
    }

    public class StockAnalysisService : IStockAnalysisService
    {
        public StockAnalysisResult AnalyzeStock(StockDetails details)
        {
            if (details?.ChartData == null || !details.ChartData.Any())
                return new StockAnalysisResult { Sentiment = "Insufficient Data", Score = 0 };

            var result = new StockAnalysisResult
            {
                Symbol = details.Symbol,
                Reasons = new List<string>(),
                PerformanceMatrix = new Dictionary<string, string>(),
                GeneratedAt = DateTime.UtcNow,
            };

            // --- 4-PILLAR WEIGHTED MODEL (Total 100%) ---
            double techScore = CalculateTechnicalScore(details, result); // Max 25%
            double finScore = CalculateFinancialScore(details, result); // Max 30%
            double shScore = CalculateShareholdingScore(details, result); // Max 25%
            double newsScore = CalculateNewsScore(details, result); // Max 20%

            result.Score = (int)Math.Min(techScore + finScore + shScore + newsScore, 100);

            // --- POPULATE PERFORMANCE MATRIX ---
            result.PerformanceMatrix["Chart Setup"] = $"{Math.Round(techScore / 25 * 100)}%";
            result.PerformanceMatrix["Financials"] = $"{Math.Round(finScore / 30 * 100)}%";

            // Only show Ownership percentage string if data exists
            if (!result.PerformanceMatrix.ContainsKey("Ownership Score"))
                result.PerformanceMatrix["Ownership Score"] = $"{Math.Round(shScore / 25 * 100)}%";

            result.PerformanceMatrix["Market Buzz"] = $"{Math.Round(newsScore / 20 * 100)}%";

            // Logic-Based Sentiment Mapping
            result.Sentiment = result.Score switch
            {
                >= 80 => "Strong Buy / High Confidence",
                >= 65 => "Positive Momentum / Accumulate",
                >= 45 => "Neutral / Consolidation",
                >= 25 => "Caution / Negative Sentiment",
                _ => "Avoid / High Risk",
            };

            return result;
        }

        private double CalculateTechnicalScore(StockDetails details, StockAnalysisResult res)
        {
            double score = 12.5; // 50% Milestone base
            var latest = details.ChartData.Last();

            if (latest.Price > latest.DmA200)
            {
                score += 6.25;
                res.Reasons.Add("Trend: Holding above 200-day long-term support.");
            }
            if (latest.Price > latest.DmA50)
            {
                score += 6.25;
                res.Reasons.Add("Momentum: Trading above short-term 50-day average.");
            }

            return Math.Clamp(score, 0, 25);
        }

        private double CalculateFinancialScore(StockDetails details, StockAnalysisResult res)
        {
            double score = 15; // 50% Milestone base
            try
            {
                var sales = details.ProfitAndLoss.FirstOrDefault(r => r.Metric.Contains("Sales"));
                var profit = details.ProfitAndLoss.FirstOrDefault(r =>
                    r.Metric.Contains("Net Profit")
                );

                if (sales != null && IsGrowthDetected(sales))
                {
                    score += 7.5;
                    res.Reasons.Add("Growth: Annual revenue is trending upwards.");
                }
                if (profit != null && IsGrowthDetected(profit))
                {
                    score += 7.5;
                    res.Reasons.Add("Earnings: The company is reporting consistent profits.");
                }
            }
            catch { }
            return Math.Clamp(score, 0, 30);
        }

        private double CalculateShareholdingScore(StockDetails details, StockAnalysisResult res)
        {
            if (details.Shareholding == null || !details.Shareholding.Any())
                return 12.5;

            try
            {
                // 1. Get 1-Year Deltas (Comparing Latest vs 4 Quarters Ago)
                var promoter = GetYearlyDeltas(details, "Promoter");
                var fii = GetYearlyDeltas(details, "FII");
                var dii = GetYearlyDeltas(details, "DII");
                var retail = GetYearlyDeltas(details, "Public");

                // 2. Identify the Handover Story (Biggest Seller ➔ Biggest Buyer)
                var deltas = new Dictionary<string, decimal>
                {
                    { "Promoters", promoter.delta },
                    { "FIIs", fii.delta },
                    { "DIIs", dii.delta },
                    { "Retail", retail.delta },
                };

                var majorSeller = deltas.OrderBy(x => x.Value).First();
                var majorBuyer = deltas.OrderByDescending(x => x.Value).First();

                if (majorSeller.Value < -0.5m)
                {
                    decimal amountSold = Math.Abs(majorSeller.Value);
                    decimal absorptionRate = (majorBuyer.Value / amountSold) * 100;

                    res.PerformanceMatrix["Handover"] = $"{majorSeller.Key} ➔ {majorBuyer.Key}";
                    res.PerformanceMatrix["Absorption"] =
                        $"{Math.Min(100, Math.Round(absorptionRate))}%";
                    res.Reasons.Add(
                        $"Yearly Story: {majorSeller.Key} exited {amountSold}%, which was {Math.Round(Math.Min(100, absorptionRate))}% absorbed by {majorBuyer.Key}."
                    );
                }

                // 3. Magnitude Sentiment Math
                double magnitudeSentiment = 0;
                bool isProfManaged = (promoter.latest == 0 || promoter.latest < 0.01m);

                if (isProfManaged)
                {
                    magnitudeSentiment +=
                        (double)fii.delta * 5.0
                        + (double)dii.delta * 3.5
                        + (double)retail.delta * -2.5;
                }
                else
                {
                    res.PerformanceMatrix["Promoter Stake"] = $"{promoter.latest}%";
                    magnitudeSentiment +=
                        (double)promoter.delta * 8.0
                        + (double)fii.delta * 4.0
                        + (double)dii.delta * 2.5
                        + (double)retail.delta * -2.0;
                }

                return Math.Clamp(12.5 + magnitudeSentiment, 0, 25);
            }
            catch
            {
                return 12.5;
            }
        }

        // --- HELPERS ---

        private bool IsGrowthDetected(FinancialRow row)
        {
            var vals = row
                .Values.Values.Select(v => decimal.TryParse(v.Replace(",", ""), out var d) ? d : 0)
                .ToList();
            return vals.Count >= 2 && vals.Last() > vals[vals.Count - 2];
        }

        private (decimal latest, decimal delta) GetYearlyDeltas(
            StockDetails details,
            string categoryName
        )
        {
            var row = details.Shareholding.FirstOrDefault(s =>
                s.Category.Contains(categoryName, StringComparison.OrdinalIgnoreCase)
            );
            if (row == null || !row.Values.Any())
                return (0, 0);

            var vals = row
                .Values.Values.Select(v =>
                    decimal.TryParse(v.Replace("%", "").Replace(",", "").Trim(), out var d) ? d : 0
                )
                .ToList();

            if (vals.Count < 1)
                return (0, 0);
            if (vals.Count < 2)
                return (vals.Last(), 0);

            decimal latest = vals.Last();
            // Look back 4 steps (1 year). If data is shorter, compare with the oldest record.
            int lookbackIndex = Math.Max(0, vals.Count - 5);
            decimal yearAgoValue = vals[lookbackIndex];

            return (latest, latest - yearAgoValue);
        }

        private double CalculateNewsScore(StockDetails details, StockAnalysisResult res)
        {
            if (details.News == null || !details.News.Any())
                return 10;
            int bull = 0,
                bear = 0;
            string[] bullWords =
            {
                "buy",
                "upgrade",
                "target",
                "profit",
                "growth",
                "dividend",
                "order",
                "acquisition",
                "deal",
            };
            string[] bearWords =
            {
                "sell",
                "downgrade",
                "loss",
                "debt",
                "investigation",
                "fine",
                "scam",
                "warning",
                "fall",
            };

            foreach (var n in details.News)
            {
                var t = n.Title.ToLower();
                if (bullWords.Any(w => t.Contains(w)))
                    bull++;
                if (bearWords.Any(w => t.Contains(w)))
                    bear++;
            }
            if (bull > bear)
                return 15; // 75%
            if (bear > bull)
                return 5; // 25%
            return 10; // 50%
        }
    }
}
