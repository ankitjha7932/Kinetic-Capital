using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services
{
    public interface IStockAnalysisService
    {
        StockAnalysisResult AnalyzeStock(StockDetails details);
    }

    public class StockAnalysisService : IStockAnalysisService
    {
        private readonly ILogger<StockAnalysisService> _logger;

        public StockAnalysisService(ILogger<StockAnalysisService> logger)
        {
            _logger = logger;
        }

        public StockAnalysisResult AnalyzeStock(StockDetails details)
        {
            if (details?.ChartData == null || !details.ChartData.Any())
                return null;

            var chart = details.ChartData.OrderBy(c => c.Date).ToList();
            var latest = chart.Last();
            decimal price = latest.Price;
            decimal? d50 = latest.DmA50;
            decimal? d200 = latest.DmA200;

            var result = new StockAnalysisResult
            {
                Symbol = details.Symbol,
                Reasons = new List<string>(),
                PerformanceMatrix = new Dictionary<string, string>(),
                GeneratedAt = DateTime.UtcNow,
            };

            int score = 0;

            // 1. Technical Trend (Moving Averages)
            if (d50.HasValue && d50.Value > 0)
            {
                decimal diff50 = ((price - d50.Value) / d50.Value) * 100;
                result.PerformanceMatrix.Add("vs 50 DMA", $"{Math.Round(diff50, 1):0.0}%");

                if (price > d50.Value)
                    score++;
                else
                    score--;
            }

            if (d200.HasValue && d200.Value > 0)
            {
                decimal diff200 = ((price - d200.Value) / d200.Value) * 100;
                result.PerformanceMatrix.Add("vs 200 DMA", $"{Math.Round(diff200, 1):0.0}%");

                if (d50.HasValue && price > d50.Value && price < d200.Value)
                {
                    result.Reasons.Add(
                        "Sweet Spot: Recovery above 50 DMA, approaching 200 DMA breakout."
                    );
                    score += 2;
                }
                else if (price > d200.Value)
                {
                    score++;
                }
            }

            // 2. Volume Analysis (Upsurge / Downsurge)
            if (chart.Count >= 5)
            {
                // Calculate 20-day avg volume
                double avgVol = chart
                    .Take(chart.Count - 1)
                    .Reverse()
                    .Take(20)
                    .Average(c => (double)c.Volume);

                long currentVol = latest.Volume;
                double volRatio = currentVol / avgVol;

                if (volRatio >= 2.0)
                {
                    result.Reasons.Add("Institutional Activity: Massive volume upsurge detected.");
                    result.PerformanceMatrix.Add("Volume", "High Surge");
                    score += 2;
                }
                else if (volRatio >= 1.5)
                {
                    result.Reasons.Add("Strong Volume: Significant upsurge in trading activity.");
                    result.PerformanceMatrix.Add("Volume", "Upsurge");
                    score++;
                }
                else if (volRatio <= 0.5)
                {
                    result.Reasons.Add(
                        "Low Conviction: Volume downsurge suggests lack of interest."
                    );
                    result.PerformanceMatrix.Add("Volume", "Downsurge");
                    score--;
                }
                else
                {
                    result.PerformanceMatrix.Add("Volume", "Normal");
                }
            }

            // 3. Final Sentiment Scoring
            result.Score = score;
            result.Sentiment = score switch
            {
                >= 4 => "Strongly Bullish",
                >= 1 => "Positive Momentum",
                <= -2 => "Bearish Phase",
                _ => "Neutral / Sideways",
            };

            return result;
        }
    }
}
