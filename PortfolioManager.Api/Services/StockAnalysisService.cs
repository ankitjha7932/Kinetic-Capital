using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services
{
    public interface IStockAnalysisService
    {
        StockAnalysisResult AnalyzeStock(StockDetails details, StockFundamental tradesData = null);
    }

    public class StockAnalysisService : IStockAnalysisService
    {
        public StockAnalysisResult AnalyzeStock(StockDetails d, StockFundamental tradesData = null)
        {
            if (d?.ChartData == null || !d.ChartData.Any())
                return new StockAnalysisResult { Sentiment = "Insufficient Data", Score = 0 };

            var res = new StockAnalysisResult
            {
                Symbol = d.Symbol,
                Reasons = new List<string>(),
                Breakdown = new List<ScoreBreakdown>(),
                PerformanceMatrix = new Dictionary<string, string>(),
                GeneratedAt = DateTime.UtcNow,
            };

            double techRaw = CalculateTechnicalScore(d, res);
            double finRaw = CalculateFinancialScore(d, res);
            double shRaw = CalculateShareholdingScore(d, res);
            double smartRaw = CalculateSmartMoney(tradesData, res);
            double newsRaw = CalculateNewsScore(d, res);

            double tech = (techRaw / 50.0) * 30.0;
            double fin = Math.Clamp(finRaw, 0, 30);
            double sh = Math.Clamp(shRaw, 0, 15);
            double smart = Math.Clamp(smartRaw, 0, 10);
            double news = Math.Clamp(newsRaw, 0, 15);

            res.Score = (int)Math.Clamp(Math.Round(tech + fin + sh + smart + news), 0, 100);

            res.PerformanceMatrix["Technicals"] = $"{Math.Round(techRaw / 50 * 100)}%";
            res.PerformanceMatrix["Financials"] = $"{Math.Round(fin / 30 * 100)}%";
            res.PerformanceMatrix["Ownership"] = $"{Math.Round(sh / 15 * 100)}%";
            res.PerformanceMatrix["Smart Money"] = $"{Math.Round(smart / 10 * 100)}%";
            res.PerformanceMatrix["Sentiment"] = $"{Math.Round(news / 15 * 100)}%";

            res.Sentiment = res.Score switch
            {
                >= 85 => "Exceptionally Good / Elite",
                >= 70 => "Strong Bullish / High Conviction",
                >= 50 => "Bullish / Accumulate",
                >= 35 => "Neutral / Consolidation",
                >= 20 => "Caution / Weak Structure",
                _ => "Negative / High Risk",
            };

            return res;
        }

        // ---------------- TECHNICAL ----------------
        private double CalculateTechnicalScore(StockDetails d, StockAnalysisResult res)
        {
            double score = 0;
            var last = d.ChartData.Last();
            double price = (double)last.Price;
            double dma50 = (double)(last.DmA50 ?? 0);
            double dma200 = (double)(last.DmA200 ?? 0);

            if (dma50 > 0)
            {
                double pct50 = (price - dma50) / dma50 * 100;
                double s50 = pct50 >= 0 ? 15 : Math.Max(1, 15 + (pct50 * 1.2));
                score += s50;
                Add(
                    res,
                    "Technical",
                    "50 DMA",
                    $"{pct50:0.0}%",
                    s50,
                    pct50 >= 0 ? $"Price above 50 DMA" : $"Below 50 DMA"
                );
            }

            if (dma50 > 0 && dma200 > dma50 && price > dma50)
            {
                double progress = (price - dma50) / (dma200 - dma50);
                double sRange = Math.Clamp(progress * 15, 0, 15);
                score += sRange;
                Add(
                    res,
                    "Technical",
                    "Trend Progress",
                    $"{progress * 100:0.0}%",
                    sRange,
                    "Moving towards 200 DMA"
                );
            }

            if (dma200 > 0 && price > dma200)
            {
                double pct200 = (price - dma200) / dma200 * 100;
                double bonus = Math.Min(10, pct200 / 2);
                score += bonus;
                Add(res, "Technical", "200 DMA", $"{pct200:0.0}%", bonus, "Strong long-term trend");
            }

            return Math.Clamp(score, 0, 50);
        }

        // ---------------- FINANCIAL ----------------
        private double CalculateFinancialScore(StockDetails d, StockAnalysisResult res)
        {
            double score = 0;
            var sales = GetPnlValues(d, "Sales");
            var profit = GetPnlValues(d, "Net Profit");

            if (IsGrowing(sales))
            {
                score += 10;
                Add(res, "Financial", "Revenue", "Growing", 10, "Revenue increasing");
            }

            if (IsGrowing(profit))
            {
                score += 10;
                Add(res, "Financial", "Profit", "Growing", 10, "Profit increasing");
            }

            return Math.Clamp(score, 0, 30);
        }

        // ---------------- SHAREHOLDING (UPDATED) ----------------
        private double CalculateShareholdingScore(StockDetails d, StockAnalysisResult res)
        {
            double score = 7;

            try
            {
                var promoter = GetShDelta(d, "Promoters");
                var fii = GetShDelta(d, "FII");
                var dii = GetShDelta(d, "DII");
                var retail = GetShDelta(d, "Public");

                // 🔥 HANDOVER LOGIC
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
                    decimal sold = Math.Abs(majorSeller.Value);
                    decimal absorption = majorBuyer.Value > 0 ? (majorBuyer.Value / sold) * 100 : 0;

                    res.PerformanceMatrix["Handover"] = $"{majorSeller.Key} ➔ {majorBuyer.Key}";
                    res.PerformanceMatrix["Absorption"] =
                        $"{Math.Min(100, Math.Round(absorption))}%";

                    Add(
                        res,
                        "Ownership",
                        "Handover",
                        $"{majorSeller.Key} ➔ {majorBuyer.Key}",
                        2,
                        $"{majorSeller.Key} exited {sold:0.00}%, absorbed by {majorBuyer.Key}"
                    );
                }

                // Promoter logic
                if (promoter.delta > 0)
                {
                    score += 4;
                    Add(
                        res,
                        "Ownership",
                        "Promoters",
                        "Buying",
                        4,
                        $"Stake increased {promoter.delta:0.00}%"
                    );
                }
                else if (promoter.delta < -1)
                {
                    score -= 5;
                    Add(
                        res,
                        "Ownership",
                        "Promoters",
                        "Selling",
                        -5,
                        $"Stake reduced {Math.Abs(promoter.delta):0.00}%"
                    );
                }
                else
                {
                    Add(res, "Ownership", "Promoters", "Stable", 0, "Holding stable");
                }

                res.PerformanceMatrix["Promoter Stake"] = $"{promoter.latest:0.00}%";
            }
            catch { }

            return Math.Clamp(score, 0, 15);
        }

        // ---------------- SMART MONEY ----------------
        private double CalculateSmartMoney(StockFundamental data, StockAnalysisResult res)
        {
            if (data?.Trades == null)
            {
                Add(
                    res,
                    "Smart Money",
                    "Activity",
                    "No Data",
                    0,
                    "No institutional activity detected"
                );
                return 5; // Neutral baseline
            }

            double score = 5; // Neutral base

            var trades = data.Trades.Sast ?? new List<SignificantOwnershipTrade>();

            if (!trades.Any())
            {
                Add(
                    res,
                    "Smart Money",
                    "SAST",
                    "Neutral",
                    0,
                    "No major block deals or disclosures"
                );
                return score;
            }

            int buys = 0,
                sells = 0;

            foreach (var t in trades)
            {
                var trans = t?.Transaction?.ToUpper() ?? "";

                if (trans.Contains("ACQ") || trans.Contains("BUY"))
                {
                    buys++;
                    score += 1.5;
                }
                else if (trans.Contains("SALE") || trans.Contains("SELL"))
                {
                    sells++;
                    score -= 1.5;
                }
            }

            // Clamp final score
            score = Math.Clamp(score, 0, 10);

            // Add one clean summary reason (avoids duplicates)
            if (buys > sells)
            {
                Add(
                    res,
                    "Smart Money",
                    "Flow",
                    $"Buy:{buys} Sell:{sells}",
                    score - 5,
                    "Institutional accumulation detected"
                );
            }
            else if (sells > buys)
            {
                Add(
                    res,
                    "Smart Money",
                    "Flow",
                    $"Buy:{buys} Sell:{sells}",
                    score - 5,
                    "Institutional distribution / exit observed"
                );
            }
            else
            {
                Add(
                    res,
                    "Smart Money",
                    "Flow",
                    $"Buy:{buys} Sell:{sells}",
                    0,
                    "Balanced institutional activity"
                );
            }

            res.PerformanceMatrix["Smart Flow"] =
                buys > sells ? "Accumulation"
                : sells > buys ? "Distribution"
                : "Neutral";

            return score;
        }

        // ---------------- NEWS ----------------
        private double CalculateNewsScore(StockDetails d, StockAnalysisResult res)
        {
            if (d?.News == null || !d.News.Any())
                return 7.5;
            return 7.5;
        }

        // ---------------- HELPERS ----------------
        private void Add(StockAnalysisResult res, string p, string m, string v, double i, string e)
        {
            double impact = Math.Round(i, 1);

            res.Breakdown.Add(
                new ScoreBreakdown
                {
                    Pillar = p,
                    Metric = m,
                    Value = v,
                    Impact = impact,
                    Explanation = e,
                }
            );

            // ✅ NO DUPLICATE REASONS FIX
            var reasonText = $"{m}: {e}";
            if (!res.Reasons.Contains(reasonText))
            {
                res.Reasons.Add($"{reasonText} (Impact: {impact})");
            }
        }

        private List<decimal> GetPnlValues(StockDetails d, string metric)
        {
            var row = d?.ProfitAndLoss?.FirstOrDefault(r =>
                r.Metric.Contains(metric, StringComparison.OrdinalIgnoreCase)
            );
            return row?.Values?.Values.Select(v => SafeParseDecimal(v)).ToList()
                ?? new List<decimal>();
        }

        private decimal SafeParseDecimal(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
                return 0;
            decimal.TryParse(
                val.Replace(",", ""),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var r
            );
            return r;
        }

        private bool IsGrowing(List<decimal> vals) =>
            vals.Count >= 2 && vals.Last() > vals[vals.Count - 2];

        private (decimal latest, decimal delta) GetShDelta(StockDetails d, string cat)
        {
            var row = d?.Shareholding?.FirstOrDefault(s =>
                s.Category.Contains(cat, StringComparison.OrdinalIgnoreCase)
            );

            if (row?.Values == null)
                return (0, 0);

            var vals = row.Values.Values.Select(v => SafeParseDecimal(v.Replace("%", ""))).ToList();

            if (vals.Count < 2)
                return (vals.LastOrDefault(), 0);

            return (vals.Last(), vals.Last() - vals[vals.Count - 2]);
        }
    }
}
