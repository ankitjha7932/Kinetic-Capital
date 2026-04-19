using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Workers;

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
        _logger.LogInformation("Market Scanner Worker starting...");

        // ✅ IMPROVED Startup: Sequential Warmup to avoid 401/IP Bans
        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();

                    _logger.LogInformation("[Startup] Beginning Sequential Warmup...");

                    // 1. Ticker first (Smallest request)
                    await marketService.RefreshTickerBatchAsync();

                    // 2. Wait 5 seconds before hitting the next index
                    await Task.Delay(5000, stoppingToken);
                    _logger.LogInformation("[Startup] Warming up NIFTY 100...");
                    await marketService.GetIndexMoversAsync("NIFTY 100");

                    // 3. Wait another 5 seconds
                    await Task.Delay(5000, stoppingToken);
                    _logger.LogInformation("[Startup] Warming up NIFTY 500...");
                    await marketService.GetIndexMoversAsync("NIFTY 500");

                    _logger.LogInformation("[Startup] Warmup Complete.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Startup] Warmup failed: {ex.Message}");
                }
            },
            stoppingToken
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "India Standard Time"
                    : "Asia/Kolkata";
                var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                var nowIST = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);

                if (IsMarketOpen(nowIST))
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();

                    var activeThreshold = DateTime.UtcNow.AddMinutes(-5);
                    var hasActiveUsers = await db.Users.AnyAsync(
                        u => u.LastActiveAt >= activeThreshold,
                        stoppingToken
                    );

                    if (hasActiveUsers)
                    {
                        // 1. Refresh Ticker (Every 15 mins)
                        if ((DateTime.UtcNow - _lastTickerRefresh).TotalMinutes >= 15)
                        {
                            await marketService.RefreshTickerBatchAsync();
                            _lastTickerRefresh = DateTime.UtcNow;
                        }

                        // 2. Refresh Index Movers (Every 30 mins)
                        if ((DateTime.UtcNow - _lastGlobalScan).TotalMinutes >= 30)
                        {
                            _logger.LogInformation(
                                "[Worker] Firing Scheduled Index Batch Scans..."
                            );

                            // We space these out slightly even in the loop
                            await marketService.GetIndexMoversAsync("NIFTY 100");
                            await Task.Delay(3000, stoppingToken);
                            await marketService.GetIndexMoversAsync("NIFTY 500");

                            _lastGlobalScan = DateTime.UtcNow;
                        }
                    }

                    // Check user activity and market status every minute
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                else
                {
                    _logger.LogInformation("[Worker] Market CLOSED → Sleeping.");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[Worker] Operation was cancelled (Shutting down).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker] Loop error.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private bool IsMarketOpen(DateTime dt)
    {
        if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var start = new TimeSpan(9, 15, 0);
        var end = new TimeSpan(15, 30, 0);
        return dt.TimeOfDay >= start && dt.TimeOfDay <= end;
    }
}
