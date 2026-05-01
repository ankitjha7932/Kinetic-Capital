using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndexController : ControllerBase
{
    private readonly StockPriceService _priceService;
    private readonly IMongoCollection<IndexMapping> _indexCollection;
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
    private readonly ILogger<IndexController> _logger;

    private static readonly Dictionary<string, string> IndexYahooMap = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["NIFTY 50"] = "^NSEI",
        ["NIFTY BANK"] = "^NSEBANK",
        ["NIFTY FINANCIAL SER"] = "NIFTY_FIN_SERVICE.NS",
        ["BSE SENSEX"] = "^BSESN",
        ["NIFTY MIDCAP SELECT"] = "NIFTY_MID_SELECT.NS",
        ["BSE BANKEX"] = "^SPBSEBKEX",
        ["INDIA VIX"] = "^INDIAVIX",
        ["NIFTY TOTAL MARKET"] = "^NIFTYTR",
        ["NIFTY NEXT 50"] = "^NSMIDCP",
        ["NIFTY 100"] = "^CNX100",
        ["NIFTY MIDCAP 100"] = "NIFTY_MIDCAP_100.NS",
        ["BSE 100"] = "^BSE100",
        ["NIFTY 500"] = "^CNX500",
        ["NIFTY AUTO"] = "^CNXAUTO",
        ["NIFTY SMALLCAP 100"] = "^CNXSC",
        ["NIFTY FMCG"] = "^CNXFMCG",
        ["NIFTY METAL"] = "^CNXMETAL",
        ["NIFTY PHARMA"] = "^CNXPHARMA",
        ["NIFTY PSU BANK"] = "^CNXPSUBANK",
        ["NIFTY IT"] = "^CNXIT",
        ["BSE SMALLCAP"] = "^SPBSESCP",
        ["NIFTY SMALLCAP 250"] = "NIFTY_SMLCAP_250.NS",
        ["NIFTY MIDCAP 150"] = "NIFTY_MIDCAP_150.NS",
        ["NIFTY COMMODITIES"] = "^CNXCMDT",
        ["BSE IPO"] = "^SPBSEIPO",
    };

    public IndexController(
        StockPriceService priceService,
        IMongoDatabase database,
        ILogger<IndexController> logger
    )
    {
        _priceService = priceService;
        _indexCollection = database.GetCollection<IndexMapping>("IndexConstituents");
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
        _logger = logger;
    }

    [HttpGet("chart")]
    public async Task<IActionResult> GetIndexChart(
        [FromQuery] string name,
        [FromQuery] string range = "1d"
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Index name is required.");

        if (!IndexYahooMap.TryGetValue(name.Trim(), out var yahooSymbol))
            return NotFound($"No Yahoo symbol mapped for index: {name}");

        // ── Market status (calendar-aware) ────────────────────────────────────
        var mktStatus = MarketCalendar.GetCurrentStatus();

        try
        {
            var (fetchRange, cutoffMode) = range.ToLower() switch
            {
                "1d" => ("5d", "today"),
                "1w" => ("1mo", "week"),
                "1m" => ("1y", "month"),
                "3m" => ("2y", "3month"),
                "6m" => ("2y", "6month"),
                "1y" => ("3y", "year"),
                "3y" => ("5y", "3year"),
                "5y" => ("10y", "5year"),
                "max" => ("max", "max"),
                _ => ("max", "max"),
            };

            var historyTask = FetchHistoricalDirectAsync(yahooSymbol, fetchRange);
            var quoteTask = FetchLiveQuoteAsync(yahooSymbol);
            await Task.WhenAll(historyTask, quoteTask);

            var history = await historyTask;
            var quote = await quoteTask;

            if (history == null || !history.Prices.Any())
                return Ok(
                    new
                    {
                        success = false,
                        message = "No chart data available",
                        marketStatus = BuildMarketStatusPayload(mktStatus),
                    }
                );

            var istNow = mktStatus.CurrentIST;

            DateTime cutoff = cutoffMode switch
            {
                "today" => new DateTime(istNow.Year, istNow.Month, istNow.Day, 9, 15, 0),
                "week" => istNow.AddDays(-7),
                "month" => istNow.AddMonths(-1),
                "3month" => istNow.AddMonths(-3),
                "6month" => istNow.AddMonths(-6),
                "year" => istNow.AddMonths(-12),
                "3year" => istNow.AddMonths(-36),
                "5year" => istNow.AddMonths(-60),
                _ => DateTime.MinValue,
            };

            var allPrices = history.Prices.OrderBy(p => p.Date).ToList();

            var chartPoints = allPrices
                .Where(p => p.Date >= cutoff)
                .Select(p => new
                {
                    date = p.Date,
                    price = Math.Round(p.Close, 2),
                    volume = p.Volume,
                })
                .ToList();

            // If market was closed today (holiday/weekend) and range=1d, no intraday data
            // exists — fall back gracefully to the previous trading day's candles.
            if (cutoffMode == "today" && chartPoints.Count < 3)
            {
                // Use the previous actual trading day rather than "just yesterday"
                var prevDate = mktStatus.PreviousSessionDate.ToDateTime(TimeOnly.MinValue).Date;
                chartPoints = allPrices
                    .Where(p => p.Date.Date == prevDate)
                    .Select(p => new
                    {
                        date = p.Date,
                        price = Math.Round(p.Close, 2),
                        volume = p.Volume,
                    })
                    .ToList();

                // If still empty (data gap), use whatever the latest available day is
                if (chartPoints.Count == 0)
                {
                    var lastDate = allPrices.Last().Date.Date;
                    chartPoints = allPrices
                        .Where(p => p.Date.Date == lastDate)
                        .Select(p => new
                        {
                            date = p.Date,
                            price = Math.Round(p.Close, 2),
                            volume = p.Volume,
                        })
                        .ToList();
                }
            }

            // ── Price stats ───────────────────────────────────────────────────
            decimal currentPrice,
                prevClose,
                dayChange,
                dayChangePct;
            decimal todayHigh,
                todayLow,
                openPrice,
                week52High,
                week52Low;

            if (quote != null && quote.RegularMarketOpen > 0 && mktStatus.IsOpen)
            {
                // ONLY trust Yahoo when market is LIVE
                currentPrice = quote.RegularMarketPrice;
                prevClose = quote.RegularMarketPreviousClose;
                dayChange = quote.RegularMarketChange;
                dayChangePct = quote.RegularMarketChangePercent;
                todayHigh = quote.RegularMarketDayHigh;
                todayLow = quote.RegularMarketDayLow;
                openPrice = quote.RegularMarketOpen;
                week52High = quote.FiftyTwoWeekHigh;
                week52Low = quote.FiftyTwoWeekLow;
            }
            else
            {
                var lastPoint = allPrices.Last();
                currentPrice = lastPoint.Close;

                var prevTradingDate = mktStatus
                    .PreviousSessionDate.ToDateTime(TimeOnly.MinValue)
                    .Date;

                var prevDayPoints = allPrices
                    .Where(p => p.Date.Date == prevTradingDate)
                    .OrderByDescending(p => p.Date)
                    .ToList();

                var secondLastDay = allPrices
                    .Where(p => p.Date.Date < prevTradingDate)
                    .OrderByDescending(p => p.Date)
                    .FirstOrDefault();

                prevClose = secondLastDay?.Close ?? currentPrice;

                dayChange = currentPrice - prevClose;
                dayChangePct = prevClose != 0 ? (dayChange / prevClose) * 100 : 0;

                todayHigh = prevDayPoints.Any() ? prevDayPoints.Max(p => p.Close) : currentPrice;
                todayLow = prevDayPoints.Any() ? prevDayPoints.Min(p => p.Close) : currentPrice;

                openPrice = prevDayPoints.FirstOrDefault()?.Close ?? currentPrice;

                var trailing252 = allPrices.TakeLast(Math.Min(allPrices.Count, 252)).ToList();
                week52High = trailing252.Any() ? trailing252.Max(p => p.Close) : currentPrice;
                week52Low = trailing252.Any() ? trailing252.Min(p => p.Close) : currentPrice;
            }
            return Ok(
                new
                {
                    success = true,
                    indexName = name.Trim(),
                    chartData = chartPoints,
                    marketStatus = BuildMarketStatusPayload(mktStatus),
                    stats = new
                    {
                        currentPrice = Math.Round(currentPrice, 2),
                        prevClose = Math.Round(prevClose, 2),
                        dayChange = Math.Round(dayChange, 2),
                        dayChangePct = Math.Round(dayChangePct, 2),
                        todayHigh = Math.Round(todayHigh, 2),
                        todayLow = Math.Round(todayLow, 2),
                        open = Math.Round(openPrice, 2),
                        week52High = Math.Round(week52High, 2),
                        week52Low = Math.Round(week52Low, 2),
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexChart] Failed for {Name}", name);
            return StatusCode(500, "Error fetching index chart");
        }
    }

    [HttpGet("constituents")]
    public async Task<IActionResult> GetConstituents(
        [FromQuery] string name,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 8
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Index name is required.");

        try
        {
            var mapping = await _indexCollection
                .Find(x => x.IndexName == name.Trim().ToUpperInvariant())
                .FirstOrDefaultAsync()
                .WaitAsync(TimeSpan.FromSeconds(5));

            if (mapping == null || !mapping.Symbols.Any())
                return Ok(
                    new
                    {
                        success = true,
                        data = new List<object>(),
                        totalCount = 0,
                        page,
                        pageSize,
                        totalPages = 0,
                        marketStatus = BuildMarketStatusPayload(MarketCalendar.GetCurrentStatus()),
                    }
                );

            var fundamentals = await _fundamentalCollection
                .Find(Builders<StockFundamental>.Filter.In(f => f.Symbol, mapping.Symbols))
                .Project(f => new
                {
                    f.Symbol,
                    f.CompanyName,
                    f.MarketCap,
                    f.Industry,
                })
                .ToListAsync()
                .WaitAsync(TimeSpan.FromSeconds(8));

            var sorted = fundamentals.OrderByDescending(f => ParseMarketCap(f.MarketCap)).ToList();
            var totalCount = sorted.Count;
            var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var result = paged
                .Select(f =>
                {
                    var rawSym = f.Symbol.Replace(".NS", "").Replace(".BO", "");
                    return new
                    {
                        symbol = rawSym,
                        fullSymbol = f.Symbol,
                        name = f.CompanyName ?? rawSym,
                        marketCap = f.MarketCap ?? "N/A",
                        industry = f.Industry ?? "N/A",
                    };
                })
                .ToList();

            return Ok(
                new
                {
                    success = true,
                    data = result,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    marketStatus = BuildMarketStatusPayload(MarketCalendar.GetCurrentStatus()),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Constituents] Failed for {Name}", name);
            return StatusCode(500, "Error fetching constituents");
        }
    }

    // ── GET /api/index/market-status ─────────────────────────────────────────
    /// <summary>
    /// Lightweight endpoint — returns only the current market status.
    /// Useful for the frontend to decide how to label prices without
    /// triggering a full chart/constituents fetch.
    /// </summary>
    [HttpGet("market-status")]
    public IActionResult GetMarketStatus()
    {
        var status = MarketCalendar.GetCurrentStatus();
        return Ok(new { success = true, marketStatus = BuildMarketStatusPayload(status) });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the internal <see cref="MarketStatus"/> record into a plain object
    /// that serialises cleanly to JSON for every API response.
    /// </summary>
    private static object BuildMarketStatusPayload(MarketStatus s) =>
        new
        {
            isOpen = s.IsOpen,
            sessionType = s.SessionType, // OPEN | PRE_MARKET | POST_MARKET | HOLIDAY | WEEKEND
            isLiveData = s.IsLiveData,
            previousSessionDate = s.PreviousSessionDate.ToString("yyyy-MM-dd"),
            closedReason = s.ClosedReason, // e.g. "Republic Day" / "Saturday" / null when open
            currentIST = s.CurrentIST.ToString("yyyy-MM-dd HH:mm:ss"),
        };

    // ── Private fetch helpers (unchanged from original) ───────────────────────

    private record QuoteData(
        decimal RegularMarketPrice,
        decimal RegularMarketPreviousClose,
        decimal RegularMarketChange,
        decimal RegularMarketChangePercent,
        decimal RegularMarketDayHigh,
        decimal RegularMarketDayLow,
        decimal RegularMarketOpen,
        decimal FiftyTwoWeekHigh,
        decimal FiftyTwoWeekLow
    );

    private async Task<QuoteData?> FetchLiveQuoteAsync(string rawSymbol)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );
            http.Timeout = TimeSpan.FromSeconds(10);

            var encoded = Uri.EscapeDataString(rawSymbol);
            var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={encoded}";
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var result = doc.RootElement.GetProperty("quoteResponse").GetProperty("result");
            if (result.GetArrayLength() == 0)
                return null;

            var q = result[0];
            decimal Get(string key)
            {
                if (q.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number)
                    return p.GetDecimal();
                return 0m;
            }

            return new QuoteData(
                RegularMarketPrice: Get("regularMarketPrice"),
                RegularMarketPreviousClose: Get("regularMarketPreviousClose"),
                RegularMarketChange: Get("regularMarketChange"),
                RegularMarketChangePercent: Get("regularMarketChangePercent"),
                RegularMarketDayHigh: Get("regularMarketDayHigh"),
                RegularMarketDayLow: Get("regularMarketDayLow"),
                RegularMarketOpen: Get("regularMarketOpen"),
                FiftyTwoWeekHigh: Get("fiftyTwoWeekHigh"),
                FiftyTwoWeekLow: Get("fiftyTwoWeekLow")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[LiveQuote] Failed for {Symbol}: {Msg}", rawSymbol, ex.Message);
            return null;
        }
    }

    private async Task<HistoricalData?> FetchHistoricalDirectAsync(string rawSymbol, string range)
    {
        string interval = range.ToLower() switch
        {
            "5d" => "5m",
            "1mo" => "1h",
            "1y" => "1d",
            "2y" => "1d",
            "3y" => "1d",
            "5y" => "1d",
            "10y" => "1wk",
            "max" => "1wk",
            _ => "1d",
        };

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );
            http.Timeout = TimeSpan.FromSeconds(14);

            var encoded = Uri.EscapeDataString(rawSymbol);
            var url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{encoded}?range={range}&interval={interval}";
            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[DirectFetch] {Symbol} → HTTP {Code}",
                    rawSymbol,
                    response.StatusCode
                );
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("chart", out var chart))
                return null;

            var resArr = chart.GetProperty("result");
            if (resArr.GetArrayLength() == 0)
                return null;

            var res = resArr[0];
            if (!res.TryGetProperty("timestamp", out var tProp))
                return null;

            var ts = tProp.EnumerateArray().ToList();
            var quote = res.GetProperty("indicators").GetProperty("quote")[0];
            var cls = quote.GetProperty("close").EnumerateArray().ToList();
            var vol = quote.GetProperty("volume").EnumerateArray().ToList();

            var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
            var istZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var prices = new List<PricePoint>();

            for (int i = 0; i < ts.Count; i++)
            {
                if (i < cls.Count && cls[i].ValueKind == JsonValueKind.Number)
                {
                    var istTime = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime,
                        istZone
                    );
                    long volVal =
                        (i < vol.Count && vol[i].ValueKind == JsonValueKind.Number)
                            ? vol[i].GetInt64()
                            : 0;
                    prices.Add(new PricePoint(istTime, cls[i].GetDecimal(), volVal));
                }
            }
            return new HistoricalData(prices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DirectFetch] Failed for {Symbol}", rawSymbol);
            return null;
        }
    }

    private static double ParseMarketCap(string? s)
    {
        if (string.IsNullOrEmpty(s) || s == "N/A")
            return 0;
        var clean = s.Replace("Cr", "").Replace(",", "").Trim();
        return double.TryParse(
            clean,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v
        )
            ? v
            : 0;
    }
}
