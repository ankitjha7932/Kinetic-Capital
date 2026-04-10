using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class MarketScannerWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MarketScannerWorker> _logger;

    private DateTime _lastHeavyRefresh = DateTime.MinValue;

    // ✅ Indian Market Holidays (Example - update yearly)
    private static readonly HashSet<DateTime> MarketHolidays = new()
    {
        new DateTime(2026, 1, 26), // Republic Day
        new DateTime(2026, 3, 6), // Holi (approx)
        new DateTime(2026, 4, 14), // Ambedkar Jayanti
        new DateTime(2026, 8, 15), // Independence Day
        new DateTime(2026, 10, 31), // Diwali (approx)
        new DateTime(2026, 12, 25), // Christmas
    };

    public MarketScannerWorker(IServiceProvider services, ILogger<MarketScannerWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Market Scanner Worker starting...");

        // ✅ Startup Warm Cache
        using (var scope = _services.CreateScope())
        {
            var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();
            var existingData = await marketService.GetTickerDataAsync();

            if (existingData == null || !existingData.Any())
            {
                _logger.LogInformation("[Startup] Cache empty → Performing initial refresh...");
                await marketService.RefreshTickerBatchAsync();
                _lastHeavyRefresh = DateTime.UtcNow;
            }
        }

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

                    // ✅ Immediate Refresh Logic
                    if (hasActiveUsers && (DateTime.UtcNow - _lastHeavyRefresh).TotalMinutes >= 15)
                    {
                        _logger.LogInformation(
                            "[Worker] Active users detected → Refreshing market data..."
                        );
                        await marketService.RefreshTickerBatchAsync();
                        _lastHeavyRefresh = DateTime.UtcNow;
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                else
                {
                    var nextOpen = GetNextMarketOpen(nowIST);
                    var sleepDuration = nextOpen - nowIST;

                    _logger.LogInformation(
                        "[Worker] Market CLOSED → Sleeping until {NextOpen}",
                        nextOpen
                    );

                    var maxChunk = TimeSpan.FromMinutes(30);

                    while (sleepDuration > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
                    {
                        var delay = sleepDuration > maxChunk ? maxChunk : sleepDuration;
                        await Task.Delay(delay, stoppingToken);
                        sleepDuration -= delay;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker] Loop error. Retrying in 30s...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private bool IsMarketOpen(DateTime nowIST)
    {
        // ❌ Weekend
        if (nowIST.DayOfWeek == DayOfWeek.Saturday || nowIST.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // ❌ Holiday
        if (MarketHolidays.Contains(nowIST.Date))
            return false;

        var open = new TimeSpan(9, 15, 0);
        var close = new TimeSpan(15, 30, 0);

        return nowIST.TimeOfDay >= open && nowIST.TimeOfDay <= close;
    }

    private DateTime GetNextMarketOpen(DateTime nowIST)
    {
        DateTime nextOpen =
            nowIST.TimeOfDay < new TimeSpan(9, 15, 0)
                ? nowIST.Date.AddHours(9).AddMinutes(15)
                : nowIST.Date.AddDays(1).AddHours(9).AddMinutes(15);

        // ✅ Skip weekends + holidays
        while (
            nextOpen.DayOfWeek == DayOfWeek.Saturday
            || nextOpen.DayOfWeek == DayOfWeek.Sunday
            || MarketHolidays.Contains(nextOpen.Date)
        )
        {
            nextOpen = nextOpen.AddDays(1);
        }

        return nextOpen;
    }
}
