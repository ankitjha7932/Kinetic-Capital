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
                return new StockAnalysisResult { Sentiment = "No Data", Reasons = new List<string> { "Insufficient chart history for analysis." } };

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

            // 1. Moving Average Analysis
            if (d50.HasValue && d50.Value > 0)
            {
                decimal diff50 = ((price - d50.Value) / d50.Value) * 100;
                result.PerformanceMatrix.Add("vs 50 DMA", $"{Math.Round(diff50, 1):0.0}%");
                
                if (price > d50.Value) score++;
                else score--;
            }

            if (d200.HasValue && d200.Value > 0)
            {
                decimal diff200 = ((price - d200.Value) / d200.Value) * 100;
                result.PerformanceMatrix.Add("vs 200 DMA", $"{Math.Round(diff200, 1):0.0}%");

                if (price > d200.Value)
                {
                    score += 2; // Price above 200 DMA is strong long-term bullish
                    if (d50.HasValue && price > d50.Value && d50.Value < price)
                        result.Reasons.Add("Golden Setup: Trading above both 50 and 200 DMA.");
                }
                else
                {
                    score -= 2;
                    result.Reasons.Add("Caution: Price is currently below the 200-day long-term trend line.");
                }
            }

            // 2. Volume Analysis
            if (chart.Count >= 20)
            {
                // Compare today's volume to the 20-period average
                double avgVol = chart.Take(chart.Count - 1).TakeLast(20).Average(c => (double)c.Volume);
                double volRatio = latest.Volume / (avgVol > 0 ? avgVol : 1);

                if (volRatio >= 2.0)
                {
                    result.Reasons.Add("Institutional Infusion: Volume is 2x above the 20-day average.");
                    result.PerformanceMatrix.Add("Volume Profile", "Heavy Accumulation");
                    score += 2;
                }
                else if (volRatio >= 1.5)
                {
                    result.Reasons.Add("Increasing Interest: Volume upsurge detected.");
                    score++;
                }
            }

            // 3. Score Mapping
            result.Score = score;
            result.Sentiment = score switch
            {
                >= 4 => "Strongly Bullish",
                >= 1 => "Positive Momentum",
                <= -3 => "Bearish Phase",
                _ => "Neutral / Sideways",
            };

            return result;
        }
    }
}