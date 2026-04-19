using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PortfolioHealthService _health;
    private readonly StockPriceService _priceService;
    private readonly NewsService _newsService;
    private readonly MarketService _marketService;
    private readonly IMongoDatabase _mongoDb;
    private readonly ILogger<PortfolioController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PortfolioController(
        AppDbContext db,
        PortfolioHealthService health,
        StockPriceService priceService,
        NewsService newsService,
        MarketService marketService,
        IMongoDatabase mongoDb,
        ILogger<PortfolioController> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _db = db;
        _health = health;
        _priceService = priceService;
        _newsService = newsService;
        _marketService = marketService;
        _mongoDb = mongoDb;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("summary/{userId}")]
    public async Task<IActionResult> GetSummary(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required.");
        _logger.LogInformation("[Summary] Fetching for User: {UserId}", userId);

        try
        {
            // ✅ FIX 1: Fire-and-forget heartbeat moved to a safe background thread with its own scope
            _ = Task.Run(() => UpdateUserHeartbeat(userId));

            // ✅ FIX 2: Use AsNoTracking() for read-only operations to improve performance and thread safety
            var holdings = await _db
                .Holdings.AsNoTracking()
                .Where(h => h.UserId == userId)
                .ToListAsync();

            if (!holdings.Any())
            {
                return Ok(new PortfolioSummaryResponse { UserId = userId, Holdings = new() });
            }

            // Batch fetch prices upfront
            var symbols = holdings.Select(h => h.Symbol).Distinct().ToList();
            var priceMap = await _priceService.GetBatchPricesAsync(symbols);

            var stockCollection = _mongoDb.GetCollection<StockFundamental>("StocksDeepData");
            var semaphore = new SemaphoreSlim(2);

            var tasks = holdings.Select(async h =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Price lookup with batch priority and fallback
                    string ticker = Sanitize(h.Symbol);
                    if (!priceMap.TryGetValue(ticker, out decimal livePrice) || livePrice <= 0)
                    {
                        try
                        {
                            livePrice = await _priceService
                                .GetLivePriceAsync(h.Symbol)
                                .WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        catch
                        {
                            livePrice = h.AvgBuyPrice;
                        }
                    }

                    string? marketCapLabel = null;
                    try
                    {
                        // ✅ FIX 3: Project only the field we need to reduce memory pressure
                        var fundamental = await stockCollection
                            .Find(s => s.Symbol == h.Symbol)
                            .Project(s => new { s.MarketCap })
                            .FirstOrDefaultAsync()
                            .WaitAsync(TimeSpan.FromSeconds(4));

                        if (fundamental != null)
                            marketCapLabel = GetMarketCapLabel(
                                ParseMarketCap(fundamental.MarketCap)
                            );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "[Summary] MCAP lookup failed for {Symbol}: {Msg}",
                            h.Symbol,
                            ex.Message
                        );
                    }

                    return new HoldingResponse(
                        h.Id,
                        h.Symbol,
                        h.Quantity,
                        h.AvgBuyPrice,
                        livePrice,
                        CalculatePnl(h.Quantity, h.AvgBuyPrice, livePrice),
                        h.BuyDate,
                        0.85m,
                        h.AvgBuyPrice > 0
                            ? Math.Round(((livePrice - h.AvgBuyPrice) / h.AvgBuyPrice) * 100, 2)
                            : 0,
                        h.Tags ?? "Equity",
                        marketCapLabel
                    );
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var holdingResponses = (await Task.WhenAll(tasks)).Where(x => x != null).ToList();

            var totalInv = Math.Round(holdingResponses.Sum(h => h.Quantity * h.AvgBuyPrice), 2);
            var totalCur = Math.Round(holdingResponses.Sum(h => h.Quantity * h.CurrentPrice), 2);
            var totalPnl = Math.Round(totalCur - totalInv, 2);

            return Ok(
                new PortfolioSummaryResponse
                {
                    UserId = userId,
                    TotalHoldings = holdingResponses.Count,
                    TotalInvested = totalInv,
                    CurrentValue = totalCur,
                    TotalPnl = totalPnl,
                    TotalPnlPercent = totalInv > 0 ? Math.Round((totalPnl / totalInv) * 100, 2) : 0,
                    Holdings = holdingResponses.OrderBy(x => x.Symbol).ToList(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Summary] Fatal error for {UserId}", userId);
            return StatusCode(500, "Internal Server Error");
        }
    }

    [HttpGet("analysis")]
    public async Task<IActionResult> AnalyzeCurrentUser([FromQuery] string userId)
    {
        _logger.LogInformation("[Analysis] Analyzing portfolio for {UserId}", userId);

        // Use the same safe fetching logic as GetSummary
        var summaryActionResult = await GetSummary(userId);
        if (
            summaryActionResult is OkObjectResult okResult
            && okResult.Value is PortfolioSummaryResponse summary
        )
        {
            try
            {
                var healthResult = _health.Analyze(userId, summary.Holdings);
                var symbols = summary.Holdings.Select(h => h.Symbol).ToList();

                var sparklineMap = await _priceService
                    .GetBatchSparklinesAsync(symbols)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                foreach (var pos in healthResult.Positions)
                {
                    if (sparklineMap.TryGetValue(pos.Symbol, out var trend))
                        pos.History = trend;
                }
                return Ok(healthResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Analysis] Sparklines failed for {UserId}", userId);
                return Ok(_health.Analyze(userId, summary.Holdings));
            }
        }
        return BadRequest("Could not analyze portfolio.");
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string userId)
    {
        try
        {
            var user = await _db
                .Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            if (user == null)
                return NotFound("User not found.");

            var sectors = (user.PreferredSectors ?? "").Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            return Ok(_health.SuggestStocks(user.RiskProfile ?? "Moderate", sectors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Suggestions] Failed for {UserId}", userId);
            return Ok(new List<object>());
        }
    }

    [HttpGet("price/{symbol}")]
    public async Task<IActionResult> GetSinglePrice(string symbol)
    {
        try
        {
            decimal price = await _priceService
                .GetLivePriceAsync(symbol)
                .WaitAsync(TimeSpan.FromSeconds(8));
            return price <= 0 ? NotFound() : Ok(new { Symbol = symbol, Price = price });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Price] Failed for {Symbol}", symbol);
            return StatusCode(503, "Price service unavailable");
        }
    }

    [HttpGet("news/{symbol}")]
    public async Task<IActionResult> GetNews(string symbol)
    {
        try
        {
            var news = await _newsService
                .GetStockNewsAsync(symbol)
                .WaitAsync(TimeSpan.FromSeconds(10));
            return (news == null || !news.Any()) ? NotFound() : Ok(news);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[News] Failed for {Symbol}", symbol);
            return Ok(new List<object>());
        }
    }

    [HttpGet("index-movers")]
    public async Task<IActionResult> GetMovers([FromQuery] string index = "NIFTY 500")
    {
        // index can be: "NIFTY 100", "NIFTY 500", "MIDCAP 100", "SMALLCAP 100", "NIFTY TOTAL MARKET"
        var result = await _marketService.GetIndexMoversAsync(index);
        return Ok(result);
    }

    [HttpGet("ticker")]
    public async Task<IActionResult> GetTicker()
    {
        try
        {
            return Ok(
                await _marketService.GetTickerDataAsync().WaitAsync(TimeSpan.FromSeconds(15))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[Ticker] Timed out or failed: {Msg}", ex.Message);
            return Ok(new List<object>());
        }
    }

    [HttpDelete("holding/{id}")]
    public async Task<IActionResult> RemoveHolding(string id)
    {
        var holding = await _db.Holdings.FindAsync(id);
        if (holding == null)
            return NotFound();
        _db.Holdings.Remove(holding);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Asset removed" });
    }

    private async Task UpdateUserHeartbeat(string userId)
    {
        // ✅ FIX 4: Use a BRAND NEW scope for the background task to prevent access to disposed context
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await scopedDb.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastActiveAt = DateTime.UtcNow;
                await scopedDb.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "[Heartbeat] Background update failed for {UserId}: {Msg}",
                userId,
                ex.Message
            );
        }
    }

    private string Sanitize(string s) =>
        s.ToUpper().EndsWith(".NS") ? s.ToUpper() : $"{s.ToUpper()}.NS";

    private double ParseMarketCap(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        double.TryParse(
            s.Replace("Cr", "").Replace(",", "").Trim(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double val
        );
        return val;
    }

    private string? GetMarketCapLabel(double m) =>
        m >= 20000 ? "LARGE-CAP"
        : m >= 5000 ? "MID-CAP"
        : m >= 500 ? "SMALL-CAP"
        : (m > 0 ? "MICRO-CAP" : null);

    private decimal CalculatePnl(decimal q, decimal a, decimal c) => Math.Round(q * (c - a), 2);
}
