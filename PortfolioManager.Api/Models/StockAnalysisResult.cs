using System;
using System.Collections.Generic;

namespace PortfolioManager.Api.Models
{
    public class StockDetails
    {
        public string Symbol { get; set; } = string.Empty;
        public string Industry { get; set; } = "N/A";
        public string LastUpdate { get; set; } = string.Empty;
        public List<ChartDataPoint> ChartData { get; set; } = new();
        public FundamentalRatios Ratios { get; set; } = new();
        public List<FinancialRow> QuarterlyResults { get; set; } = new();
        public List<FinancialRow> ProfitAndLoss { get; set; } = new();
        public List<FinancialRow> BalanceSheet { get; set; } = new();
        public List<FinancialRow> CashFlow { get; set; } = new();
        public List<PeerData> Peers { get; set; } = new();
        public StockAnalysisResult? Analysis { get; set; }
        public decimal PeriodHigh { get; set; }
        public decimal PeriodLow { get; set; }
        public decimal PeriodReturn { get; set; }
    }

    public class ChartDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public long Volume { get; set; }
        public decimal? DmA50 { get; set; }
        public decimal? DmA200 { get; set; }
        public bool IsVolumeSpike { get; set; }
    }

    public class FundamentalRatios
    {
        public decimal CurrentPrice { get; set; }
        public decimal PriceChange { get; set; }
        public decimal PriceChangePercent { get; set; }
        public string MarketCap { get; set; } = "N/A";
        public string StockPE { get; set; } = "N/A";
        public string ROCE { get; set; } = "N/A";
        public string ROE { get; set; } = "N/A";
        public string BookValue { get; set; } = "N/A";
        public string DividendYield { get; set; } = "N/A";
        public string FaceValue { get; set; } = "N/A";
        public decimal High52W { get; set; }
        public decimal Low52W { get; set; }
        public decimal HistoricalHigh { get; set; }
        public decimal HistoricalLow { get; set; }
    }

    public class StockAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
        public int Score { get; set; }
        public List<string> Reasons { get; set; } = new();
        public Dictionary<string, string> PerformanceMatrix { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
