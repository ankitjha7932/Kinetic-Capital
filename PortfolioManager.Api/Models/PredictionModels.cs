namespace PortfolioManager.Api.Models;

// === UPDATED: StockTacticalInsight now carries investability + risk transparency + forward outlook ===
public record StockTacticalInsight(
    string Symbol,
    string CompanyName,
    decimal CurrentPrice,
    string SetupCategory, // "Momentum Breakout", "52W Low Reversal", "Volatility Squeeze",
                           // "Stable", "High Risk / Illiquid Spike", "High Risk / Red-Flagged"
    double ConvictionScore, // 0 to 100 — gated by fundamentals + liquidity + market regime, not just technicals
    bool IsInvestableGrade, // false if liquidity or fundamental red flags cap this from "high conviction"
    List<string> SignalTriggers,
    List<string> RiskFlags, // explicit reasons the score was discounted
    TacticalMetrics Metrics,
    FundamentalHealthCheck Health,
    ForwardOutlook Forecast // NEW: rules-based "coming days" projection, see disclaimer on the endpoint response
);

public record TacticalMetrics(
    decimal Rsi,
    decimal BollingerBandwidth,
    bool IsBollingerSqueeze,
    decimal AtrPercent, // Expected daily movement capacity
    decimal MacdHistogram,
    double VolumeMultiplier, // Today's volume vs 20-day average
    decimal PriceVelocity3D, // 3-day return %
    decimal PriceVelocity7D, // 7-day return %
    double AvgDailyTurnoverCr, // avg (price * volume) over 20d, in Cr — the liquidity check
    string ObvTrend, // "Confirming", "Diverging", "Neutral", "Insufficient Data"
    bool? Above200Dma, // null if <200 trading days of history available
    // --- NEW ---
    decimal Velocity20D, // 20-day return %, used for relative-strength calc
    decimal RelativeStrength20D, // stock 20D return minus benchmark 20D return (points). >0 = outperforming.
    decimal Adx, // Average Directional Index (14) — trend strength, not direction
    string TrendStrengthLabel // "Strong Trend", "Developing Trend", "Choppy / Range-bound"
);

// === Fundamental health gate — auditable, drives the score multiplier ===
public record FundamentalHealthCheck(
    decimal? DebtToEquity,
    bool NegativeNetWorth,
    decimal PromoterHoldingChange, // percentage point change, most recent vs prior period
    bool PromoterSellingTrend, // true if promoter stake declining for 2+ consecutive periods
    bool ProfitConsistent, // true if majority of recent P&L periods show positive net profit
    decimal? InterestCoverage, // Operating Profit / Interest — null if not computable
    decimal? PeerPeRatio, // stock PE ÷ average peer PE. >1 = premium to sector, <1 = discount
    bool CashFlowQualityOk, // false if operating cash flow is well below reported net profit
    bool EarningsAccelerating, // true if YoY quarterly sales growth is speeding up, not just positive
    double GateMultiplier, // 0.0 to 1.0 — actually discounts the raw technical score
    List<string> RedFlags
);

// === NEW: broader market context, computed once per scan cycle and applied to every stock ===
public record MarketRegime(
    string IndexSymbol,
    decimal IndexPrice,
    bool? AboveSma50,
    bool? AboveSma200,
    decimal Momentum5D, // index 5-session return %
    decimal Momentum20D, // index 20-session return %
    string RegimeLabel, // "Risk-On", "Neutral / Choppy", "Risk-Off", "Unknown"
    double RegimeGateMultiplier // 0.7 (Risk-Off) to 1.0 (Risk-On) — discounts every stock's score when the tape is weak
);

// === NEW: rules-based "coming days" projection.
// This is a heuristic composite of volatility (ATR), trend strength (ADX), volume confirmation (OBV)
// and market regime — it is NOT a trained or backtested statistical model. Treat ContinuationScore as
// a relative ranking signal across today's universe, not a probability, and treat the move bands as a
// volatility-implied range, not a target. ===
public record ForwardOutlook(
    string PrimaryHorizon, // "3-5 Days", "1-2 Days", "Fading / Low Persistence — Monitor Only"
    double ContinuationScore, // 0-100, relative confidence that the current setup persists vs. fades/reverses
    decimal Expected1DLow,
    decimal Expected1DHigh,
    decimal Expected3DLow,
    decimal Expected3DHigh,
    decimal Expected5DLow,
    decimal Expected5DHigh,
    List<string> Notes
);