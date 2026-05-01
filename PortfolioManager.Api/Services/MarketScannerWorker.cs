using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Workers;

/// <summary>
/// Background worker that refreshes market data during NSE trading hours.
/// Uses <see cref="MarketCalendar"/> as the single source of truth for
/// open/closed logic — weekends AND all NSE holidays are handled correctly.
/// </summary>
public class MarketScannerWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MarketScannerWorker> _logger;

    private DateTime _lastTickerRefresh = DateTime.MinValue;
    private DateTime _lastGlobalScan = DateTime.MinValue;

    public MarketScannerWorker(IServiceProvider services, ILogger<MarketScannerWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Worker] Market Scanner starting.");

        // ── Warm-up: run on a background thread so startup isn't delayed ──────
        _ = Task.Run(() => RunStartupWarmupAsync(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var status = MarketCalendar.GetCurrentStatus();

                if (status.IsOpen)
                {
                    await RunMarketOpenCycleAsync(status, stoppingToken);
                    // Poll every minute while open
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                else
                {
                    LogClosedReason(status);

                    // Sleep duration depends on how far we are from the next open.
                    // If it's a holiday/weekend we can sleep long; pre-market sleep is short.
                    var sleepDuration = status.SessionType switch
                    {
                        "PRE_MARKET" => TimeSpan.FromMinutes(5), // almost open — check soon
                        "POST_MARKET" => TimeSpan.FromMinutes(30), // closed for the day
                        "HOLIDAY" => TimeSpan.FromHours(1), // check again in an hour
                        "WEEKEND" => TimeSpan.FromHours(2), // long sleep on weekends
                        _ => TimeSpan.FromMinutes(30),
                    };

                    await Task.Delay(sleepDuration, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Worker] Shutdown requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker] Unexpected error in main loop.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("[Worker] Market Scanner stopped.");
    }

    // ── Market-open cycle ─────────────────────────────────────────────────────

    private async Task RunMarketOpenCycleAsync(MarketStatus status, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();

        // Only refresh if at least one user has been active in the last 5 minutes.
        var activeThreshold = DateTime.UtcNow.AddMinutes(-5);
        var hasActiveUsers = await db.Users.AnyAsync(u => u.LastActiveAt >= activeThreshold, ct);

        if (!hasActiveUsers)
        {
            _logger.LogDebug("[Worker] No active users — skipping refresh.");
            return;
        }

        // 1. Ticker refresh — every 15 minutes
        if ((DateTime.UtcNow - _lastTickerRefresh).TotalMinutes >= 15)
        {
            _logger.LogInformation("[Worker] Refreshing ticker batch.");
            await marketService.RefreshTickerBatchAsync();
            _lastTickerRefresh = DateTime.UtcNow;
        }

        // 2. Index movers — every 30 minutes
        if ((DateTime.UtcNow - _lastGlobalScan).TotalMinutes >= 30)
        {
            _logger.LogInformation("[Worker] Refreshing index movers.");
            await marketService.GetIndexMoversAsync("NIFTY 100");
            await Task.Delay(3_000, ct); // rate-limit guard
            await marketService.GetIndexMoversAsync("NIFTY 500");
            _lastGlobalScan = DateTime.UtcNow;
        }
    }

    // ── Startup warm-up ───────────────────────────────────────────────────────

    private async Task RunStartupWarmupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();
            var status = MarketCalendar.GetCurrentStatus();

            _logger.LogInformation(
                "[Startup] Warmup — market is {Status}. {Reason}",
                status.SessionType,
                status.ClosedReason ?? "Normal session"
            );

            // Always warm up the ticker first (lightest request)
            await marketService.RefreshTickerBatchAsync();

            // Only do the heavier index warmup if the market is actually open
            // (or just closed — post-market). Skip on holidays/weekends.
            if (status.IsOpen || status.SessionType == "POST_MARKET")
            {
                await Task.Delay(5_000, ct);
                _logger.LogInformation("[Startup] Warming up NIFTY 100...");
                await marketService.GetIndexMoversAsync("NIFTY 100");

                await Task.Delay(5_000, ct);
                _logger.LogInformation("[Startup] Warming up NIFTY 500...");
                await marketService.GetIndexMoversAsync("NIFTY 500");
            }
            else
            {
                _logger.LogInformation(
                    "[Startup] Skipping index warmup — market closed ({Reason}).",
                    status.ClosedReason ?? status.SessionType
                );
            }

            _logger.LogInformation("[Startup] Warmup complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Startup] Warmup failed.");
        }
    }

    // ── Logging helper ────────────────────────────────────────────────────────

    private void LogClosedReason(MarketStatus status)
    {
        var msg = status.SessionType switch
        {
            "HOLIDAY" => $"[Worker] Market closed — Holiday: {status.ClosedReason}. Sleeping.",
            "WEEKEND" => $"[Worker] Market closed — {status.ClosedReason}. Sleeping.",
            "PRE_MARKET" => "[Worker] Pre-market. Waiting for 09:15 open.",
            "POST_MARKET" => "[Worker] Post-market (after 15:30). Sleeping.",
            _ => $"[Worker] Market closed ({status.SessionType}). Sleeping.",
        };
        _logger.LogInformation(msg);
    }
}
