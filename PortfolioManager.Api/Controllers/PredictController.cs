using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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

    // ---- Caching ----
    // All three scanners used to independently reload the fundamentals universe, refetch
    // ~220 days of history per stock, and recompute every indicator from scratch. Hitting
    // the dashboard (which calls all three) meant 3x the Mongo load, 3x the price-service
    // calls, and 3x the Skender indicator computation for identical results. Now the full
    // scan runs once per cache window and every endpoint reads from that shared snapshot.
    private const string CacheKey = "TacticalUniverseSnapshot:v2";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    // Adjust to whatever symbol your StockPriceService actually resolves for the
    // benchmark index (e.g. "^NSEI", "NIFTY 50", "NSEI") — used for the market regime filter.
    private const string BenchmarkIndexSymbol = "NIFTY 50";

    private record UniverseSnapshot(List<StockTacticalInsight> Insights, MarketRegime Regime);

    public PredictController(
        StockPriceService priceService,
        IMongoDatabase database,
        IMemoryCache cache
    )
    {
        _priceService = priceService;
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
        _cache = cache;
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

    // ---- NEW: "coming days" outlook ----
    // Ranks by ForwardOutlook.ContinuationScore — the composite of trend strength (ADX), volume
    // confirmation (OBV), setup persistence, and market regime. See the disclaimer below: this is
    // a transparent heuristic ranking, not a trained/backtested probability model.
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

            regime = TacticalFeatureEngineer.ComputeMarketRegime(BenchmarkIndexSymbol, indexPoints);
        }
        catch
        {
            // If the index feed hiccups, don't take the whole scan down — fall back to neutral.
            regime = new MarketRegime(BenchmarkIndexSymbol, 0, null, null, 0, 0, "Unknown", 1.0);
        }

        var totalInsights = await TacticalFeatureEngineer.GenerateUniverseInsightsAsync(
            trackingUniverse,
            FetchAndMapHistoricalDataAsync,
            regime,
            batchSize: 100
        );

        var snapshot = new UniverseSnapshot(totalInsights, regime);
        _cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    // Pulls every field the fundamental health gate now needs: balance sheet (D/E, net worth),
    // P&L (profit consistency, interest coverage, cash flow quality vs profit), shareholding
    // (promoter trend), quarterly results (earnings acceleration), and peer data (relative PE).
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
            catch
            {
                // Gracefully ignore trace dropouts
            }
            finally
            {
                connectionThrottler.Release();
            }
        });

        await Task.WhenAll(downloadTasks);
        return bulkLookup.ToLookup(x => x.Symbol, x => x.Point);
    }
}
