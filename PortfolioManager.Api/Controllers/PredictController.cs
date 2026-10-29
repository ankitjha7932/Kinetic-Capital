using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictController : ControllerBase
{
    private readonly StockPriceService _priceService;
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PredictController> _logger;

    // ---- Caching ----
    // The full scan runs once per cache window and every endpoint reads the shared snapshot.
    private const string CacheKey = "TacticalUniverseSnapshot:v3";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    // If the benchmark failed to load, cache the (degraded) snapshot only briefly.
    private static readonly TimeSpan DegradedCacheDuration = TimeSpan.FromMinutes(1);

    // Prevents a cache stampede: dashboard fires several endpoints at once on a cold cache.
    private static readonly SemaphoreSlim ScanLock = new(1, 1);

    // IMPORTANT: this must be the symbol your StockPriceService actually resolves for the Nifty 50
    // index. "NIFTY 50" returned no data in your output (indexPrice = 0). Try "^NSEI" (Yahoo style).
    // If regime.indexPrice is still 0 in the response, this symbol is wrong.
    private const string BenchmarkIndexSymbol = "^NSEI";

    private record UniverseSnapshot(List<StockTacticalInsight> Insights, MarketRegime Regime);

    public PredictController(
        StockPriceService priceService,
        IMongoDatabase database,
        IMemoryCache cache,
        ILogger<PredictController> logger
    )
    {
        _priceService = priceService;
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
        _cache = cache;
        _logger = logger;
    }

    /// <param name="minConviction">Minimum gated conviction score (default 55).</param>
    /// <param name="investableOnly">
    /// If true (default), excludes "High Risk / Illiquid Spike" and "High Risk / Red-Flagged" names.
    /// </param>
    [HttpGet("short-term-movers")]
    public async Task<IActionResult> GetTacticalOpportunities(
        [FromQuery] double minConviction = 55,
        [FromQuery] bool investableOnly = true
    )
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var filteredOpportunities = snapshot
            .Insights.Where(insight =>
                insight.SignalTriggers.Any() && insight.ConvictionScore > minConviction
            )
            .Where(insight => !investableOnly || insight.IsInvestableGrade)
            .OrderByDescending(x => x.ConvictionScore)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                count = filteredOpportunities.Count,
                note = investableOnly
                    ? "Illiquid and fundamentally red-flagged names are excluded. Pass investableOnly=false to see them (not recommended for sizing real positions)."
                    : "Includes high-risk / illiquid / red-flagged names — check RiskFlags on each result before acting.",
                opportunities = filteredOpportunities,
            }
        );
    }

    [HttpGet("scanner/52w-low-reversals")]
    public async Task<IActionResult> GetDeepOversoldReversals(
        [FromQuery] bool investableOnly = true
    )
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var conversionMatches = snapshot
            .Insights.Where(result => result.SetupCategory == "52W Low Reversal Candidate")
            .Where(result => !investableOnly || result.IsInvestableGrade)
            .OrderByDescending(x => x.ConvictionScore)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                totalMatches = conversionMatches.Count,
                screenResults = conversionMatches,
            }
        );
    }

    [HttpGet("scanner/high-momentum-rockets")]
    public async Task<IActionResult> GetHighMomentumRockets([FromQuery] bool investableOnly = true)
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var rocketMatches = snapshot
            .Insights.Where(result =>
                result.SetupCategory == "Momentum Breakout"
                || result.SetupCategory == "Rocket Breakout"
            )
            .Where(result => !investableOnly || result.IsInvestableGrade)
            .OrderByDescending(x => x.ConvictionScore)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                totalMatches = rocketMatches.Count,
                screenResults = rocketMatches,
            }
        );
    }

    // ---- "coming days" outlook ----
    [HttpGet("scanner/next-days-outlook")]
    public async Task<IActionResult> GetNextDaysOutlook(
        [FromQuery] double minContinuationScore = 60,
        [FromQuery] bool investableOnly = true
    )
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var matches = snapshot
            .Insights.Where(i => i.Forecast.ContinuationScore >= minContinuationScore)
            .Where(i => i.Forecast.PrimaryHorizon != "Fading / Low Persistence — Monitor Only")
            .Where(i => !investableOnly || i.IsInvestableGrade)
            .OrderByDescending(i => i.Forecast.ContinuationScore)
            .ThenByDescending(i => i.ConvictionScore)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                count = matches.Count,
                disclaimer = "ContinuationScore and the expected move bands are a rules-based composite of volatility (ATR), "
                    + "trend strength (ADX), volume confirmation (OBV), and market regime — not a trained or backtested "
                    + "statistical model, and not a price target. Treat as a relative ranking within today's universe, "
                    + "and validate against your own backtests before sizing real positions.",
                opportunities = matches,
            }
        );
    }

    // ---- Manual cache bust, e.g. to force a refresh right after market close ----
    [HttpPost("refresh-cache")]
    public IActionResult RefreshCache()
    {
        _cache.Remove(CacheKey);
        return Ok(
            new
            {
                success = true,
                message = "Cache cleared. Next request will recompute the full scan.",
            }
        );
    }

    private async Task<UniverseSnapshot> GetOrComputeSnapshotAsync()
    {
        if (_cache.TryGetValue(CacheKey, out UniverseSnapshot? cached) && cached != null)
        {
            return cached;
        }

        await ScanLock.WaitAsync();
        try
        {
            // Double-check: another request may have filled the cache while we waited.
            if (_cache.TryGetValue(CacheKey, out cached) && cached != null)
            {
                return cached;
            }

            var trackingUniverse = await LoadUniverseAsync();

            MarketRegime regime;
            try
            {
                var indexHistory = await _priceService.GetHistoricalDataAsync(
                    BenchmarkIndexSymbol,
                    "1y"
                );
                var indexPoints =
                    indexHistory
                        ?.Prices?.Select(p => new ChartDataPoint
                        {
                            Date = p.Date,
                            Price = p.Close,
                            Volume = p.Volume,
                        })
                        .ToList()
                    ?? new List<ChartDataPoint>();

                regime = TacticalFeatureEngineer.ComputeMarketRegime(
                    BenchmarkIndexSymbol,
                    indexPoints
                );

                if (regime.IndexPrice <= 0)
                {
                    _logger.LogWarning(
                        "Benchmark '{Symbol}' returned no usable history ({Count} points). Regime = Unknown, relative strength disabled. Check BenchmarkIndexSymbol.",
                        BenchmarkIndexSymbol,
                        indexPoints.Count
                    );
                }
            }
            catch (Exception ex)
            {
                // If the index feed hiccups, don't take the whole scan down — fall back to neutral.
                _logger.LogError(
                    ex,
                    "Failed to load benchmark index '{Symbol}'.",
                    BenchmarkIndexSymbol
                );
                regime = new MarketRegime(BenchmarkIndexSymbol, 0, null, null, 0, 0, "Unknown", 1.0);
            }

            var totalInsights = await TacticalFeatureEngineer.GenerateUniverseInsightsAsync(
                trackingUniverse,
                FetchAndMapHistoricalDataAsync,
                regime,
                batchSize: 100
            );

            var snapshot = new UniverseSnapshot(totalInsights, regime);

            // Don't pin a degraded (no-benchmark) snapshot in cache for the full window.
            var ttl = regime.IndexPrice > 0 ? CacheDuration : DegradedCacheDuration;
            _cache.Set(CacheKey, snapshot, ttl);
            return snapshot;
        }
        finally
        {
            ScanLock.Release();
        }
    }

    private async Task<List<StockFundamental>> LoadUniverseAsync()
    {
        var universeDocs = await _fundamentalCollection.Find(s => s.Symbol != null).ToListAsync();

        return universeDocs
            .Select(s => new StockFundamental
            {
                Symbol = s.Symbol,
                CompanyName = s.CompanyName,
                MarketCap = s.MarketCap,
                StockPE = s.StockPE,
                BalanceSheet = s.BalanceSheet,
                ProfitAndLoss = s.ProfitAndLoss,
                CashFlow = s.CashFlow,
                QuarterlyResults = s.QuarterlyResults,
                Shareholding = s.Shareholding,
                PeersData = s.PeersData,
            })
            .ToList();
    }

    // daysRequired drives how much history to pull — the engine requests ~220 trading days so
    // it can evaluate the 200DMA long-term trend filter and 20D relative-strength calc.
    private async Task<ILookup<string, ChartDataPoint>> FetchAndMapHistoricalDataAsync(
        List<string> symbols,
        int daysRequired
    )
    {
        string range = daysRequired > 90 ? "1y" : "3mo";

        var bulkLookup = new ConcurrentBag<(string Symbol, ChartDataPoint Point)>();
        using var connectionThrottler = new SemaphoreSlim(8);

        var downloadTasks = symbols.Select(async symbol =>
        {
            await connectionThrottler.WaitAsync();
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(symbol, range);
                if (history?.Prices == null)
                    return;

                foreach (var p in history.Prices)
                {
                    bulkLookup.Add(
                        (
                            symbol,
                            new ChartDataPoint
                            {
                                Date = p.Date,
                                Price = p.Close,
                                Volume = p.Volume,
                            }
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "History fetch failed for {Symbol}", symbol);
            }
            finally
            {
                connectionThrottler.Release();
            }
        });

        await Task.WhenAll(downloadTasks);
        return bulkLookup.ToLookup(x => x.Symbol, x => x.Point);
    }

    [HttpGet("scanner/potential-multibaggers")]
    public async Task<IActionResult> GetPotentialMultibaggers([FromQuery] bool investableOnly = true)
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var matches = snapshot
            .Insights.Where(i => i.DivergenceCategory == "Potential Multibagger")
            .Where(i => !investableOnly || i.IsInvestableGrade)
            .OrderByDescending(i => i.EarningsTrend.QualityScore)
            .ThenByDescending(i => i.ConvictionScore) // quality scores tie often; break ties by setup
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                count = matches.Count,
                disclaimer = "Flags stocks with genuine, broad-based earnings growth (revenue, operating profit, net profit and EPS all up, "
                    + "not driven by other income or tax) whose price hasn't re-rated yet (6-month change between -20% and +15%). "
                    + "This is a fundamentals-vs-price heuristic, not a prediction that the price will move — validate the earnings quality yourself before acting.",
                opportunities = matches,
            }
        );
    }

    // ---- NEW: strong fundamentals + price not yet run + first signs of accumulation ----
    // Narrows the multibagger list to names that are not falling knives, not overbought,
    // not lagging the index, liquid enough to trade, and showing early volume support.
    [HttpGet("scanner/early-movers")]
    public async Task<IActionResult> GetEarlyMovers([FromQuery] int top = 25)
    {
        var snapshot = await GetOrComputeSnapshotAsync();
        bool benchmarkKnown = snapshot.Regime.IndexPrice > 0;

        var matches = snapshot
            .Insights.Where(i => i.DivergenceCategory == "Potential Multibagger" && i.IsInvestableGrade)
            .Where(i =>
                i.Health.CashFlowQualityOk
                && i.Health.ProfitConsistent
                && i.Health.GateMultiplier >= 0.85
            )
            .Where(i => i.Metrics.AvgDailyTurnoverCr >= 2)
            .Where(i => i.Metrics.Rsi >= 40 && i.Metrics.Rsi <= 68) // not a falling knife, not overbought
            .Where(i => !benchmarkKnown || i.Metrics.RelativeStrength20D > -3) // not lagging the index
            .Where(i => i.Metrics.PriceVelocity7D > -3)
            .Where(i =>
                i.Metrics.Above200Dma != false
                || i.Metrics.ObvTrend == "Confirming"
                || i.Metrics.ObvTrend == "Accumulating"
            )
            .Select(i => new
            {
                Insight = i,
                EarlyScore = Math.Round(
                    (i.EarningsTrend.QualityScore * 0.5) + (i.ConvictionScore * 0.5),
                    1
                ),
            })
            .OrderByDescending(x => x.EarlyScore)
            .Take(top)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                benchmarkKnown,
                count = matches.Count,
                disclaimer = "Ranking = 50% earnings quality + 50% technical conviction. Not a prediction or price target. "
                    + "Log the daily top list and measure 5/10/20-day forward returns before sizing real positions.",
                results = matches,
            }
        );
    }

    [HttpGet("scanner/re-rated-compounders")]
    public async Task<IActionResult> GetReRatedCompounders([FromQuery] bool investableOnly = true)
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var matches = snapshot
            .Insights.Where(i => i.DivergenceCategory == "Re-Rated Compounder")
            .Where(i => !investableOnly || i.IsInvestableGrade)
            .OrderByDescending(i => i.Metrics.PriceChange6M)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                count = matches.Count,
                note = "Strong earnings growth where price has already moved up meaningfully (6-month change over 40%) — "
                    + "the market has already recognized this one. Useful for confirming a thesis, less useful for finding cheap entries.",
                opportunities = matches,
            }
        );
    }

    [HttpGet("scanner/structural-decliners")]
    public async Task<IActionResult> GetStructuralDecliners()
    {
        var snapshot = await GetOrComputeSnapshotAsync();

        var matches = snapshot
            .Insights.Where(i => i.DivergenceCategory == "Structural Decliner")
            .OrderBy(i => i.EarningsTrend.QualityScore)
            .ThenBy(i => i.Metrics.PriceChange6M)
            .ToList();

        return Ok(
            new
            {
                success = true,
                regime = snapshot.Regime,
                count = matches.Count,
                warning = "Deteriorating earnings AND a price decline over 25% in 6 months — this list is for avoidance/exit "
                    + "review, not for buying the dip. A cheap price with worsening fundamentals is usually a value trap, not a bargain.",
                opportunities = matches,
            }
        );
    }
}