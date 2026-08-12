using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using PortfolioManager.Api.Models;
using Skender.Stock.Indicators;

namespace PortfolioManager.Api.Services;

public class TacticalFeatureEngineer
{
    // ---- Liquidity & size floors -------------------------------------------------
    private const double MinDailyTurnoverInr = 50_00_000; // ₹50 lakh/day

    // Need ~200+ trading days of history to evaluate the 200DMA long-term trend
    // filter. Callers should request this many days from the historical delegate.
    public const int RecommendedHistoryDays = 220;

    public static async Task<List<StockTacticalInsight>> GenerateUniverseInsightsAsync(
        List<StockFundamental> stockUniverse,
        Func<List<string>, int, Task<ILookup<string, ChartDataPoint>>> fetchHistoricalDataDelegate,
        MarketRegime? regime = null,
        int batchSize = 100
    )
    {
        if (stockUniverse == null || !stockUniverse.Any())
        {
            return new List<StockTacticalInsight>();
        }

        var allInsights = new ConcurrentBag<StockTacticalInsight>();

        var batches = stockUniverse
            .Select((stock, index) => new { stock, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.stock).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var batchSymbols = batch.Select(s => s.Symbol).ToList();

            ILookup<string, ChartDataPoint> bulkHistoricalData = await fetchHistoricalDataDelegate(
                batchSymbols,
                RecommendedHistoryDays
            );

            var batchTasks = batch
                .Select(stock =>
                    Task.Run(() =>
                    {
                        var stockPoints = bulkHistoricalData[stock.Symbol].ToList();
                        var insight = GenerateInsights(stock, stockPoints, regime);
                        allInsights.Add(insight);
                    })
                )
                .ToList();

            await Task.WhenAll(batchTasks);
        }

        return allInsights
            .OrderByDescending(i => i.IsInvestableGrade)
            .ThenByDescending(i => i.ConvictionScore)
            .ToList();
    }

    // ---- NEW: broader market regime, computed once per scan cycle from index history ----
    // Callers should fetch ~220 days of the benchmark index (e.g. NIFTY 50) the same way they
    // fetch stock history, and pass the resulting points here once, then reuse the returned
    // MarketRegime for every call to GenerateInsights in that cycle.
    public static MarketRegime ComputeMarketRegime(
        string indexSymbol,
        List<ChartDataPoint> indexPoints
    )
    {
        if (indexPoints == null || indexPoints.Count < 10)
        {
            return new MarketRegime(indexSymbol, 0, null, null, 0, 0, "Unknown", 1.0);
        }

        indexPoints = indexPoints.OrderBy(p => p.Date).ToList();
        var last = indexPoints.Last();
        decimal price = last.Price;

        bool? above50 = null;
        if (indexPoints.Count >= 50)
        {
            decimal sma50 = indexPoints.TakeLast(50).Average(p => p.Price);
            above50 = price > sma50;
        }

        bool? above200 = null;
        if (indexPoints.Count >= 200)
        {
            decimal sma200 = indexPoints.TakeLast(200).Average(p => p.Price);
            above200 = price > sma200;
        }

        decimal price5Ago =
            indexPoints.Count >= 6 ? indexPoints[^6].Price : indexPoints.First().Price;
        decimal price20Ago =
            indexPoints.Count >= 21 ? indexPoints[^21].Price : indexPoints.First().Price;

        decimal momentum5D = price5Ago > 0 ? ((price - price5Ago) / price5Ago) * 100 : 0;
        decimal momentum20D = price20Ago > 0 ? ((price - price20Ago) / price20Ago) * 100 : 0;

        string label;
        double gate;

        if (above50 == true && above200 == true && momentum20D > 0)
        {
            label = "Risk-On";
            gate = 1.0;
        }
        else if (above200 == false || (above50 == false && momentum20D < -2m))
        {
            label = "Risk-Off";
            gate = 0.7;
        }
        else
        {
            label = "Neutral / Choppy";
            gate = 0.9;
        }

        return new MarketRegime(
            indexSymbol,
            Math.Round(price, 2),
            above50,
            above200,
            Math.Round(momentum5D, 2),
            Math.Round(momentum20D, 2),
            label,
            gate
        );
    }

    public static StockTacticalInsight GenerateInsights(
        StockFundamental stock,
        List<ChartDataPoint> rawPoints,
        MarketRegime? regime = null
    )
    {
        string symbol = stock.Symbol;
        string companyName = stock.CompanyName;

        if (rawPoints == null || rawPoints.Count < 30)
        {
            return new StockTacticalInsight(
                symbol,
                companyName,
                rawPoints?.LastOrDefault()?.Price ?? 0,
                "Insufficient Data",
                0,
                false,
                new() { "Requires at least 30 historical trading days." },
                new(),
                EmptyMetrics(),
                EmptyHealth("Insufficient price history to evaluate."),
                EmptyOutlook()
            );
        }

        rawPoints = rawPoints.OrderBy(p => p.Date).ToList();

        var quotes = rawPoints
            .Select(p => new Quote
            {
                Date = p.Date,
                Open = p.Price,
                High = p.Price,
                Low = p.Price,
                Close = p.Price,
                Volume = p.Volume,
            })
            .ToList();

        var lastPoint = rawPoints.Last();
        decimal currentPrice = lastPoint.Price;

        // Compute Indicators via Skender Library
        var rsiList = quotes.GetRsi(14).ToList();
        var bbList = quotes.GetBollingerBands(20, 2).ToList();
        var atrList = quotes.GetAtr(14).ToList();
        var macdList = quotes.GetMacd(12, 26, 9).ToList();
        var bma3List = quotes.GetEma(3).ToList();
        var bma9List = quotes.GetEma(9).ToList();
        var obvList = quotes.GetObv().ToList();
        var adxList = quotes.GetAdx(14).ToList(); // NEW: trend strength

        var currentRsi = (decimal)(rsiList.LastOrDefault()?.Rsi ?? 50);
        var previousRsi = (decimal)(rsiList.Count >= 2 ? rsiList[^2]?.Rsi ?? 50 : 50);

        var currentBb = bbList.LastOrDefault();
        var currentAtr = (decimal)(atrList.LastOrDefault()?.Atr ?? 0);
        var currentMacd = macdList.LastOrDefault();
        var currentAdx = (decimal)(adxList.LastOrDefault()?.Adx ?? 0);

        string trendStrengthLabel = currentAdx switch
        {
            >= 25 => "Strong Trend",
            >= 15 => "Developing Trend",
            _ => "Choppy / Range-bound",
        };

        decimal bandwidth = 0;
        bool isSqueeze = false;
        double compressionIntensity = 0;

        if (currentBb != null && currentBb.Width.HasValue)
        {
            bandwidth = (decimal)currentBb.Width.Value;
            var historicalBbWidthAvg = bbList
                .Skip(Math.Max(0, bbList.Count - 20))
                .Average(b => b.Width ?? 0.2);

            isSqueeze = (double)bandwidth < (historicalBbWidthAvg * 0.85);
            compressionIntensity =
                historicalBbWidthAvg > 0 ? 1.0 - ((double)bandwidth / historicalBbWidthAvg) : 0;
        }

        decimal atrPercent = currentPrice > 0 ? (currentAtr / currentPrice) * 100 : 0m;

        double avgVolume = rawPoints
            .Skip(Math.Max(0, rawPoints.Count - 21))
            .Take(20)
            .Average(v => v.Volume);
        double volumeMultiplier = avgVolume > 0 ? (double)lastPoint.Volume / avgVolume : 1.0;

        double avgDailyTurnoverInr = rawPoints
            .Skip(Math.Max(0, rawPoints.Count - 21))
            .Take(20)
            .Average(p => (double)p.Price * p.Volume);
        double avgDailyTurnoverCr = avgDailyTurnoverInr / 1_00_00_000.0;
        bool isIlliquid = avgDailyTurnoverInr < MinDailyTurnoverInr;

        decimal price3DaysAgo =
            rawPoints.Count >= 4 ? rawPoints[^4].Price : rawPoints.First().Price;
        decimal price7DaysAgo =
            rawPoints.Count >= 8 ? rawPoints[^8].Price : rawPoints.First().Price;
        decimal price20DaysAgo =
            rawPoints.Count >= 21 ? rawPoints[^21].Price : rawPoints.First().Price;

        decimal velocity3D =
            price3DaysAgo > 0 ? ((currentPrice - price3DaysAgo) / price3DaysAgo) * 100 : 0;
        decimal velocity7D =
            price7DaysAgo > 0 ? ((currentPrice - price7DaysAgo) / price7DaysAgo) * 100 : 0;
        decimal velocity20D =
            price20DaysAgo > 0 ? ((currentPrice - price20DaysAgo) / price20DaysAgo) * 100 : 0;

        // --- NEW: relative strength vs the broader market ---
        decimal relativeStrength20D =
            regime != null ? velocity20D - regime.Momentum20D : velocity20D;

        // --- OBV trend confirmation ---
        // Checks whether volume has genuinely been accumulating in the direction of the
        // recent price move, vs. a price move happening on thin/unconvinced volume.
        string obvTrend = "Insufficient Data";
        bool obvDiverging = false;
        bool obvConfirming = false;
        if (obvList.Count >= 11)
        {
            double obvNow = obvList.Last().Obv;
            double obvThen = obvList[^11].Obv;
            double obvDelta = obvNow - obvThen;

            if (velocity7D > 1.0m && obvDelta < 0)
            {
                obvTrend = "Diverging";
                obvDiverging = true;
            }
            else if (velocity7D > 1.0m && obvDelta > 0)
            {
                obvTrend = "Confirming";
                obvConfirming = true;
            }
            else if (velocity7D < -1.0m && obvDelta > 0)
            {
                obvTrend = "Diverging"; // price falling but volume net-accumulating — early basing signal, flagged either way for visibility
                obvDiverging = true;
            }
            else
            {
                obvTrend = "Neutral";
            }
        }

        // --- long-term trend context (200DMA), only if enough history was fetched ---
        bool? above200Dma = null;
        if (rawPoints.Count >= 200)
        {
            decimal sma200 = rawPoints.TakeLast(200).Average(p => p.Price);
            above200Dma = currentPrice > sma200;
        }

        // === Technical scoring matrix ===
        var triggers = new List<string>();
        double score = 40.0;
        string category = "Stable / Consolidating";

        // Pillar A: 52-Week Low Reversal / Oversold Snapback
        if (previousRsi < 32 && currentRsi >= 32)
        {
            double rsiVelocity = (double)(currentRsi - previousRsi);
            double rsiBonus = Math.Clamp(15.0 + (rsiVelocity * 2.5), 15.0, 25.0);
            score += rsiBonus;
            triggers.Add(
                $"OVERSOLD_REVERSAL: RSI crossed above oversold baseline with a velocity of +{rsiVelocity:0.2}/day. (+{rsiBonus:0.1} Pts)"
            );
            category = "52W Low Reversal Candidate";
        }

        // Pillar B: Momentum Acceleration
        if (velocity3D > 2.0m && currentRsi > 55)
        {
            double velocityFactor = (double)velocity3D * 2.0;
            double rsiFactor = (double)(currentRsi - 55) * 0.5;
            double momentumBonus = Math.Clamp(velocityFactor + rsiFactor, 5.0, 25.0);
            score += momentumBonus;
            triggers.Add(
                $"MOMENTUM_ACCELERATION: Outward price movement backed by strong multi-day momentum indicators. (+{momentumBonus:0.1} Pts)"
            );
            if (category == "Stable / Consolidating")
                category = "Momentum Breakout";
        }

        // Pillar C: Volume Shocker — capped input, diminishing returns past 3x
        if (volumeMultiplier > 1.2)
        {
            double effectiveMultiplier = Math.Min(volumeMultiplier, 5.0);
            double volumeBonus = Math.Clamp((effectiveMultiplier - 1.0) * 4.0, 2.0, 16.0);
            score += volumeBonus;
            triggers.Add(
                $"VOLUME_SHOCKER: Trading volume footprint is running {volumeMultiplier:0.1}x above its 20-day baseline. (+{volumeBonus:0.1} Pts)"
            );

            if (volumeMultiplier > 8.0)
            {
                triggers.Add(
                    $"VOLUME_ANOMALY: {volumeMultiplier:0.1}x is an extreme outlier — in a low-liquidity name this is a manipulation red flag, not a quality signal."
                );
            }
        }

        // Pillar D: Coiled Volatility Squeeze
        if (isSqueeze && lastPoint.Price > (decimal)(currentBb?.Sma ?? 0))
        {
            double squeezeBonus = Math.Clamp(compressionIntensity * 30.0, 5.0, 15.0);
            score += squeezeBonus;
            triggers.Add(
                $"VOLATILITY_COIL: Deep Bollinger contraction detected (Compression: {compressionIntensity * 100:0.1}%). Price breaking upward. (+{squeezeBonus:0.1} Pts)"
            );
            if (category == "Stable / Consolidating")
                category = "Volatility Squeeze";
        }

        // Pillar E: Ultra Short Term Trend Cross
        var lastEma3 = bma3List.LastOrDefault()?.Ema;
        var lastEma9 = bma9List.LastOrDefault()?.Ema;
        var prevEma3 = bma3List.Count >= 2 ? bma3List[^2]?.Ema : null;
        var prevEma9 = bma9List.Count >= 2 ? bma9List[^2]?.Ema : null;

        if (lastEma3 > lastEma9 && prevEma3 <= prevEma9)
        {
            score += 15.0;
            triggers.Add(
                "SHORT_TERM_CROSSOVER: Immediate 3-Day EMA crossed above 9-Day trend line indicating rapid reversal. (+15.0 Pts)"
            );
            if (category == "Stable / Consolidating")
                category = "Micro-Trend Cross";
        }

        // Pillar F: Rocket Velocity Setup
        bool isConsecutiveGreen =
            rawPoints.Count >= 3
            && rawPoints[^1].Price > rawPoints[^2].Price
            && rawPoints[^2].Price > rawPoints[^3].Price;
        if (isConsecutiveGreen && velocity3D > 6.0m)
        {
            score += 20.0;
            triggers.Add(
                $"ROCKET_BREAKOUT: Extreme velocity detected with consecutive compounding days (+{velocity3D:0.1}% over 3d). (+20.0 Pts)"
            );
            category = "Rocket Breakout";
        }

        // Pillar G: OBV confirmation / divergence
        if (obvConfirming)
        {
            score += 6.0;
            triggers.Add(
                "OBV_CONFIRMED: Volume has been net-accumulating in the direction of the recent move — the move looks backed by real participation, not a single-day spike. (+6.0 Pts)"
            );
        }
        else if (obvDiverging)
        {
            score -= 10.0;
            triggers.Add(
                "OBV_DIVERGENCE: Price move is not backed by accumulating volume — historically a warning that a move lacks conviction. (-10.0 Pts)"
            );
        }

        // Pillar H (NEW): Relative strength leadership vs the broader market
        bool marketIsWeak = regime != null && regime.RegimeLabel == "Risk-Off";
        if (relativeStrength20D > 5m && !marketIsWeak)
        {
            double rsBonus = Math.Clamp((double)(relativeStrength20D - 5m) * 0.8, 3.0, 12.0);
            score += rsBonus;
            triggers.Add(
                $"RELATIVE_STRENGTH_LEADER: Outperforming {(regime?.IndexSymbol ?? "the broader market")} by {relativeStrength20D:0.1} pts over the last 20 sessions — possible rotation into this name. (+{rsBonus:0.1} Pts)"
            );
        }

        // Saturated Overbought Penalty
        if (currentRsi > 76 && category != "Rocket Breakout")
        {
            double overboughtPenalty = (double)(currentRsi - 76) * 1.5;
            score -= overboughtPenalty;
            triggers.Add(
                $"RISK_OVEREXTENDED: Momentum values highly saturated. High near-term mean-reversion risk. (-{overboughtPenalty:0.1} Pts)"
            );
        }

        double technicalRawScore = Math.Clamp(score, 0, 130);

        // === Fundamental health gate ===
        var health = EvaluateFundamentalHealth(stock);

        var riskFlags = new List<string>(health.RedFlags);
        bool illiquidOverride = false;

        if (isIlliquid)
        {
            illiquidOverride = true;
            riskFlags.Add(
                $"ILLIQUID: Avg daily turnover ~₹{avgDailyTurnoverCr:0.00} Cr is below the ₹{MinDailyTurnoverInr / 1_00_00_000.0:0.0} Cr/day floor — price can be moved by a small number of orders."
            );
        }

        if (above200Dma == false)
        {
            riskFlags.Add(
                "Trading below the 200-day average — the long-term trend is still down; a short-term bounce here may not be a real reversal."
            );
        }

        if (relativeStrength20D < -8m)
        {
            riskFlags.Add(
                $"Lagging {(regime?.IndexSymbol ?? "the broader market")} by {Math.Abs(relativeStrength20D):0.1} pts over 20 sessions — any bounce here may just be beta catching up, not genuine stock-specific strength."
            );
        }

        if (marketIsWeak)
        {
            riskFlags.Add(
                $"Broader market ({regime!.IndexSymbol}) is in a Risk-Off regime (below key moving averages, negative momentum) — bottom-up breakout signals are statistically less reliable when the index itself is trending down."
            );
        }

        double liquidityGate = isIlliquid ? 0.35 : 1.0;
        double longTermTrendGate = above200Dma == false ? 0.85 : 1.0;
        double regimeGate = regime?.RegimeGateMultiplier ?? 1.0;
        double finalGate = Math.Round(
            health.GateMultiplier * liquidityGate * longTermTrendGate * regimeGate,
            2
        );

        double gatedScore = technicalRawScore * finalGate;

        string finalCategory = category;
        bool investableGrade = true;

        if (illiquidOverride && !health.RedFlags.Any())
        {
            finalCategory = "High Risk / Illiquid Spike";
            investableGrade = false;
        }
        else if (health.RedFlags.Count >= 2 || health.NegativeNetWorth)
        {
            finalCategory = "High Risk / Red-Flagged";
            investableGrade = false;
        }
        else if (isIlliquid)
        {
            investableGrade = false;
        }

        var metrics = new TacticalMetrics(
            Math.Round(currentRsi, 2),
            Math.Round(bandwidth, 4),
            isSqueeze,
            Math.Round(atrPercent, 2),
            Math.Round((decimal)(currentMacd?.Histogram ?? 0), 2),
            Math.Round(volumeMultiplier, 1),
            Math.Round(velocity3D, 2),
            Math.Round(velocity7D, 2),
            Math.Round(avgDailyTurnoverCr, 2),
            obvTrend,
            above200Dma,
            Math.Round(velocity20D, 2),
            Math.Round(relativeStrength20D, 2),
            Math.Round(currentAdx, 2),
            trendStrengthLabel
        );

        var forecast = BuildForwardOutlook(
            currentPrice,
            currentAtr,
            velocity3D,
            currentAdx,
            obvConfirming,
            obvDiverging,
            finalCategory,
            regime
        );

        return new StockTacticalInsight(
            symbol,
            companyName,
            Math.Round(currentPrice, 2),
            finalCategory,
            Math.Clamp(Math.Round(gatedScore, 1), 0, 100),
            investableGrade,
            triggers,
            riskFlags,
            metrics,
            health,
            forecast
        );
    }

    // ---- NEW: rules-based multi-day continuation / expected-move projection ----
    // Not a trained or backtested model — a transparent composite of trend strength (ADX),
    // volume confirmation (OBV), setup type persistence, and market regime. Use it to rank
    // relative confidence across today's scan, not as a probability or price target.
    private static ForwardOutlook BuildForwardOutlook(
        decimal currentPrice,
        decimal atrAbs,
        decimal velocity3D,
        decimal adx,
        bool obvConfirming,
        bool obvDiverging,
        string category,
        MarketRegime? regime
    )
    {
        double continuation = 50.0;

        if (adx >= 25)
            continuation += 15;
        else if (adx < 15)
            continuation -= 12;

        if (obvConfirming)
            continuation += 15;
        else if (obvDiverging)
            continuation -= 20;

        if (regime != null)
            continuation *= regime.RegimeGateMultiplier;

        // Signal-specific persistence adjustment — some setups tend to mean-revert fast,
        // others tend to build over several sessions.
        switch (category)
        {
            case "Rocket Breakout":
                continuation -= 8; // already-extended moves fade quicker
                break;
            case "Volatility Squeeze":
                continuation += 8; // breakouts from compression tend to run longer once triggered
                break;
            case "52W Low Reversal Candidate":
                continuation -= 4; // reversals off lows typically need 1-2 confirmation days
                break;
        }

        continuation = Math.Clamp(continuation, 0, 100);

        string horizon = continuation switch
        {
            >= 65 when adx >= 20 => "3-5 Days",
            >= 50 => "1-2 Days",
            _ => "Fading / Low Persistence — Monitor Only",
        };

        double decay = continuation / 100.0;
        decimal dailyDrift = (velocity3D / 3m) * (decimal)decay;

        decimal Move(int days, bool isHigh)
        {
            decimal band = atrAbs * (decimal)Math.Sqrt(days);
            decimal drift = dailyDrift * days;
            return Math.Round(currentPrice + drift + (isHigh ? band : -band), 2);
        }

        var notes = new List<string>();
        if (continuation < 50)
            notes.Add(
                "Low continuation confidence — weak trend strength and/or unconfirmed volume. Treat as a watch-list name, not a high-conviction multi-day hold."
            );
        if (regime?.RegimeLabel == "Risk-Off")
            notes.Add("Forecast discounted for a Risk-Off broader market regime.");
        if (adx < 15)
            notes.Add(
                "ADX below 15 indicates a choppy/range-bound tape — breakout follow-through is historically less reliable here."
            );

        return new ForwardOutlook(
            horizon,
            Math.Round(continuation, 1),
            Move(1, false),
            Move(1, true),
            Move(3, false),
            Move(3, true),
            Move(5, false),
            Move(5, true),
            notes
        );
    }

    private static ForwardOutlook EmptyOutlook() =>
        new(
            "Fading / Low Persistence — Monitor Only",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            new List<string> { "Insufficient data to project a forward range." }
        );

    private static TacticalMetrics EmptyMetrics() =>
        new(0, 0, false, 0, 0, 0, 0, 0, 0, "Insufficient Data", null, 0, 0, 0, "Insufficient Data");

    // ---- Fundamental health: balance sheet / ownership / valuation / cash flow gating ----
    private static FundamentalHealthCheck EvaluateFundamentalHealth(StockFundamental stock)
    {
        var redFlags = new List<string>();

        decimal? debtToEquity = ComputeDebtToEquity(stock, out bool negativeNetWorth);
        if (negativeNetWorth)
            redFlags.Add(
                "Negative net worth: accumulated losses (negative reserves) exceed paid-up equity — the balance sheet is structurally impaired, not just cyclically weak."
            );
        else if (debtToEquity.HasValue && debtToEquity > 2.0m)
            redFlags.Add($"High leverage: Debt/Equity ~{debtToEquity:0.00}x.");

        var (promoterChange, sellingTrend) = ComputePromoterTrend(stock);
        if (sellingTrend)
            redFlags.Add(
                $"Promoter stake has declined for 2+ consecutive reporting periods (latest change {promoterChange:0.00} pts) — insiders reducing exposure is a caution signal."
            );

        bool profitConsistent = ComputeProfitConsistency(stock);
        if (!profitConsistent)
            redFlags.Add(
                "Net profit has been negative or erratic across recent reporting periods — earnings quality does not support the price move."
            );

        decimal? interestCoverage = ComputeInterestCoverage(stock);
        if (interestCoverage.HasValue && interestCoverage < 1.0m)
            redFlags.Add(
                $"Interest coverage ~{interestCoverage:0.0}x (below 1.0x) — operating profit alone doesn't cover interest expense."
            );

        decimal? peerPeRatio = ComputePeerRelativePe(stock);
        if (peerPeRatio.HasValue && peerPeRatio > 1.3m)
            redFlags.Add(
                $"Trading at a premium to sector peers (PE ~{peerPeRatio:0.00}x the peer average) — the market may already be pricing in the good news."
            );

        bool cashFlowQualityOk = ComputeCashFlowQuality(stock);
        if (!cashFlowQualityOk)
            redFlags.Add(
                "Operating cash flow is running well below reported net profit — earnings may not be converting to cash, a common early sign of aggressive accounting or working-capital stress."
            );

        bool earningsAccelerating = ComputeEarningsAcceleration(stock);

        double gate = 1.0;
        if (negativeNetWorth)
            gate = Math.Min(gate, 0.25);
        if (debtToEquity.HasValue && debtToEquity > 2.0m)
            gate *= 0.85;
        if (sellingTrend)
            gate *= 0.75;
        if (!profitConsistent)
            gate *= 0.7;
        if (interestCoverage.HasValue && interestCoverage < 1.0m)
            gate *= 0.6;
        if (peerPeRatio.HasValue && peerPeRatio > 1.3m)
            gate *= 0.85;
        if (!cashFlowQualityOk)
            gate *= 0.7;

        gate = Math.Clamp(gate, 0.25, 1.0);

        return new FundamentalHealthCheck(
            debtToEquity,
            negativeNetWorth,
            promoterChange,
            sellingTrend,
            profitConsistent,
            interestCoverage,
            peerPeRatio,
            cashFlowQualityOk,
            earningsAccelerating,
            gate,
            redFlags
        );
    }

    private static FundamentalHealthCheck EmptyHealth(string reason) =>
        new(null, false, 0, false, true, null, null, true, false, 1.0, new List<string> { reason });

    private static decimal? ComputeDebtToEquity(StockFundamental stock, out bool negativeNetWorth)
    {
        negativeNetWorth = false;
        decimal? borrowings = LatestValue(FindRow(stock.BalanceSheet, "Borrowings"));
        decimal? reserves = LatestValue(FindRow(stock.BalanceSheet, "Reserves"));
        decimal? equityCapital = LatestValue(FindRow(stock.BalanceSheet, "Equity Capital"));

        if (!borrowings.HasValue || !equityCapital.HasValue)
            return null;

        decimal netWorth = equityCapital.Value + (reserves ?? 0);

        if (netWorth <= 0)
        {
            negativeNetWorth = true;
            return null;
        }

        return Math.Round(borrowings.Value / netWorth, 2);
    }

    private static (decimal change, bool sellingTrend) ComputePromoterTrend(StockFundamental stock)
    {
        var row = stock.Shareholding?.FirstOrDefault(s =>
            s.Category.Contains("Promoter", StringComparison.OrdinalIgnoreCase)
        );
        if (row?.Values == null || row.Values.Count < 3)
            return (0, false);

        var vals = row.Values.Values.Select(v => SafeParseDecimal(v.Replace("%", ""))).ToList();

        decimal latestDelta = vals[^1] - vals[^2];
        decimal priorDelta = vals[^2] - vals[^3];

        bool sellingTrend = latestDelta < -0.25m && priorDelta < 0;
        return (Math.Round(latestDelta, 2), sellingTrend);
    }

    private static bool ComputeProfitConsistency(StockFundamental stock)
    {
        var row = FindRow(stock.ProfitAndLoss, "Net Profit");
        if (row?.Values == null || row.Values.Count == 0)
            return true;

        var recent = row.Values.Values.Select(v => SafeParseDecimal(v)).TakeLast(4).ToList();
        if (!recent.Any())
            return true;

        int positiveCount = recent.Count(v => v > 0);
        return positiveCount >= (recent.Count / 2.0);
    }

    private static decimal? ComputeInterestCoverage(StockFundamental stock)
    {
        decimal? opProfit = LatestValue(
            FindRow(stock.ProfitAndLoss, "Operating Profit"),
            preferTtm: true
        );
        decimal? interest = LatestValue(FindRow(stock.ProfitAndLoss, "Interest"), preferTtm: true);

        if (!opProfit.HasValue || !interest.HasValue || interest.Value == 0)
            return null;

        return Math.Round(opProfit.Value / interest.Value, 2);
    }

    private static decimal? ComputePeerRelativePe(StockFundamental stock)
    {
        if (stock.PeersData == null || !stock.PeersData.Any())
            return null;
        if (
            string.IsNullOrWhiteSpace(stock.StockPE)
            || !decimal.TryParse(
                stock.StockPE,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var stockPe
            )
            || stockPe <= 0
        )
            return null;

        var peerPes = stock
            .PeersData.Where(p =>
                !p.Symbol?.Equals(stock.Symbol, StringComparison.OrdinalIgnoreCase) ?? true
            )
            .Select(p => SafeParseDecimal(p.PE))
            .Where(pe => pe > 0)
            .ToList();

        if (!peerPes.Any())
            return null;

        decimal peerAvg = peerPes.Average();
        if (peerAvg <= 0)
            return null;

        return Math.Round(stockPe / peerAvg, 2);
    }

    private static bool ComputeCashFlowQuality(StockFundamental stock)
    {
        var ocfRow = FindRow(stock.CashFlow, "Cash from Operating Activity");
        var profitRow = FindRow(stock.ProfitAndLoss, "Net Profit");

        if (ocfRow?.Values == null || profitRow?.Values == null)
            return true; // no data — don't penalize on absence

        var ocfRecent = ocfRow.Values.Values.Select(v => SafeParseDecimal(v)).TakeLast(3).ToList();
        var profitRecent = profitRow
            .Values.Values.Where(v => v != null)
            .Select(v => SafeParseDecimal(v))
            .TakeLast(3)
            .ToList();

        if (!ocfRecent.Any() || !profitRecent.Any())
            return true;

        decimal totalOcf = ocfRecent.Sum();
        decimal totalProfit = profitRecent.Sum();

        if (totalProfit <= 0)
            return true;

        return (totalOcf / totalProfit) >= 0.5m;
    }

    private static bool ComputeEarningsAcceleration(StockFundamental stock)
    {
        var row = FindRow(stock.QuarterlyResults, "Sales");
        if (row?.Values == null || row.Values.Count < 6)
            return false;

        var vals = row.Values.Values.Select(v => SafeParseDecimal(v)).ToList();

        decimal last = vals[^1];
        decimal last4Ago = vals[^5];
        decimal prev = vals[^2];
        decimal prev4Ago = vals[^6];

        if (last4Ago == 0 || prev4Ago == 0)
            return false;

        decimal yoyLatest = (last - last4Ago) / Math.Abs(last4Ago);
        decimal yoyPrior = (prev - prev4Ago) / Math.Abs(prev4Ago);

        return yoyLatest > yoyPrior && yoyLatest > 0;
    }

    private static FinancialRow? FindRow(List<FinancialRow>? rows, string metric) =>
        rows?.FirstOrDefault(r => r.Metric.Equals(metric, StringComparison.OrdinalIgnoreCase));

    private static decimal? LatestValue(FinancialRow? row, bool preferTtm = false)
    {
        if (row?.Values == null || row.Values.Count == 0)
            return null;

        if (
            preferTtm
            && row.Values.TryGetValue("TTM", out var ttmRaw)
            && !string.IsNullOrWhiteSpace(ttmRaw)
        )
            return SafeParseDecimal(ttmRaw);

        var lastEntry = row.Values.Values.LastOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return lastEntry == null ? null : SafeParseDecimal(lastEntry);
    }

    private static decimal SafeParseDecimal(string val)
    {
        if (string.IsNullOrWhiteSpace(val))
            return 0;
        decimal.TryParse(
            val.Replace(",", ""),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result
        );
        return result;
    }
}
