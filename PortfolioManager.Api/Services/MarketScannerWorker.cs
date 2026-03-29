using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortfolioManager.Api.Services;

public class MarketScannerWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MarketScannerWorker> _logger;

    public MarketScannerWorker(IServiceProvider services, ILogger<MarketScannerWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _services.CreateScope())
        {
            var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();
            var existingData = await marketService.GetTickerDataAsync();

            if (existingData == null || !existingData.Any())
            {
                _logger.LogInformation("Cache empty. Performing startup refresh...");
                await marketService.RefreshTickerBatchAsync();
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var nowIST = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);

            if (IsMarketOpen(nowIST))
            {
                using (var scope = _services.CreateScope())
                {
                    var marketService = scope.ServiceProvider.GetRequiredService<MarketService>();
                    await marketService.RefreshTickerBatchAsync();
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            else
            {
                var nextOpen = nowIST.Date.AddDays(1).AddHours(9).AddMinutes(15);
                if (nowIST.Hour < 9)
                    nextOpen = nowIST.Date.AddHours(9).AddMinutes(15);

                if (nextOpen.DayOfWeek == DayOfWeek.Saturday)
                    nextOpen = nextOpen.AddDays(2);
                if (nextOpen.DayOfWeek == DayOfWeek.Sunday)
                    nextOpen = nextOpen.AddDays(1);

                var sleepDuration = nextOpen - nowIST;

                _logger.LogInformation(
                    "Market Closed. Sleeping until {NextOpen} IST ({Duration} hours)",
                    nextOpen,
                    Math.Round(sleepDuration.TotalHours, 2)
                );

                await Task.Delay(sleepDuration, stoppingToken);
            }
        }
    }

    private bool IsMarketOpen(DateTime nowIST)
    {
        if (nowIST.DayOfWeek == DayOfWeek.Saturday || nowIST.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var marketOpen = nowIST.Date.AddHours(9).AddMinutes(15);
        var marketClose = nowIST.Date.AddHours(15).AddMinutes(30);

        return nowIST >= marketOpen && nowIST <= marketClose;
    }
}
