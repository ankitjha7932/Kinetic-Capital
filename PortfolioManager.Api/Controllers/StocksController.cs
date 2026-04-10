using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly StockDetailsService _detailsService;
        private readonly IStockAnalysisService _analysisService;
        private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
        private readonly PeerComparisonService _peerService;
        private readonly ILogger<StocksController> _logger;

        private static readonly SemaphoreSlim _analysisSemaphore = new SemaphoreSlim(1);

        public StocksController(
            StockDetailsService detailsService,
            IStockAnalysisService analysisService,
            IMongoDatabase database,
            PeerComparisonService peerComparisonService,
            ILogger<StocksController> logger
        )
        {
            _detailsService = detailsService;
            _analysisService = analysisService;
            _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
            _peerService = peerComparisonService;
            _logger = logger;
        }

        private IActionResult Success(object data) => Ok(new { success = true, data });

        private IActionResult Fail(string message) => Ok(new { success = false, message });

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Success(new List<object>());

            try
            {
                var filter = Builders<StockFundamental>.Filter.Or(
                    Builders<StockFundamental>.Filter.Regex(
                        s => s.Symbol,
                        new BsonRegularExpression(query, "i")
                    ),
                    Builders<StockFundamental>.Filter.Regex(
                        s => s.CompanyName,
                        new BsonRegularExpression(query, "i")
                    )
                );

                var results = await _fundamentalCollection
                    .Find(filter)
                    .Project(s => new
                    {
                        symbol = s.Symbol,
                        name = s.CompanyName,
                        industry = s.Industry,
                        marketCap = s.MarketCap,
                    })
                    .Limit(10)
                    .ToListAsync()
                    .WaitAsync(TimeSpan.FromSeconds(5));

                return Success(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Search] Failed");
                return Fail("Search failed");
            }
        }

       [HttpGet("details/{symbol}")]
        public async Task<IActionResult> GetDetails(string symbol, [FromQuery] string range = "1y")
        {
            string ticker = SanitizeTicker(symbol);

            try
            {
                var details = await _detailsService
                    .GetStockDetailsAsync(ticker, range)
                    .WaitAsync(TimeSpan.FromSeconds(12));

                if (details == null)
                    return Fail("No details available");

                return Success(details);
            }
            catch (TimeoutException)
            {
                return Fail("Details request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Details] Failed");
                return Fail("Error fetching details");
            }
        }

        [HttpGet("analyze/{symbol}")]
        public async Task<IActionResult> GetAnalysis(string symbol)
        {
            string ticker = SanitizeTicker(symbol);

            if (!await _analysisSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
                return Fail("Analysis busy. Try again in a few seconds.");

            try
            {
                var detailsTask = _detailsService.GetStockDetailsAsync(ticker, "1y");
                var tradesTask = _detailsService.GetStockTradesAsync(ticker);

                await Task.WhenAll(detailsTask, tradesTask).WaitAsync(TimeSpan.FromSeconds(10));

                var details = await detailsTask;
                var tradesData = await tradesTask;

                if (details == null)
                    return Fail("Stock data not found");

                var analysis = _analysisService.AnalyzeStock(details, tradesData);

                return analysis != null ? Success(analysis) : Fail("Analysis failed");
            }
            catch (TimeoutException)
            {
                return Fail("Analysis timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Analyze] Failed");
                return Fail("Analysis error");
            }
            finally
            {
                _analysisSemaphore.Release();
            }
        }

        [HttpGet("{symbol}/shareholding")]
        public async Task<IActionResult> GetShareholdingData(string symbol)
        {
            try
            {
                var stock = await _detailsService
                    .GetStockDetailsAsync(symbol)
                    .WaitAsync(TimeSpan.FromSeconds(8));

                if (stock == null || stock.Shareholding == null || !stock.Shareholding.Any())
                    return Fail("No shareholding data");

                var quarters = stock.Shareholding.First().Values.Keys.ToList();
                var latest = quarters.Last();

                var pieData = stock
                    .Shareholding.Where(s =>
                        !s.Category.Contains("Shareholders", StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(s => new
                    {
                        name = s.Category,
                        value = double.TryParse(s.Values.GetValueOrDefault(latest), out var v)
                            ? v
                            : 0,
                    })
                    .ToList();

                return Success(
                    new
                    {
                        quarters,
                        history = stock.Shareholding,
                        pieData,
                        latestQuarterName = latest,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shareholding] Failed");
                return Fail("Error fetching shareholding data");
            }
        }

        [HttpGet("peers/{symbol}")]
        public async Task<IActionResult> GetPeers(string symbol)
        {
            string ticker = SanitizeTicker(symbol);

            try
            {
                var peerData = await _peerService
                    .GetPeerIntelligenceAsync(ticker)
                    .WaitAsync(TimeSpan.FromSeconds(6));

                return peerData != null ? Success(peerData) : Fail("No peer data found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Peers] Failed");
                return Fail("Peer lookup failed");
            }
        }

        [HttpGet("recent-insider-activity")]
        public async Task<IActionResult> GetRecentInsiderActivity()
        {
            try
            {
                var filter = Builders<StockFundamental>.Filter.And(
                    Builders<StockFundamental>.Filter.Exists(s => s.Trades.Insider),
                    Builders<StockFundamental>.Filter.Not(
                        Builders<StockFundamental>.Filter.Size(s => s.Trades.Insider, 0)
                    )
                );

                var data = await _fundamentalCollection
                    .Find(filter)
                    .SortByDescending(s => s.LastTradesUpdate)
                    .Limit(10)
                    .Project(s => new
                    {
                        s.Symbol,
                        s.CompanyName,
                        InsiderCount = s.Trades.Insider.Count,
                    })
                    .ToListAsync()
                    .WaitAsync(TimeSpan.FromSeconds(10));

                return Success(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Insider] Failed");
                return Fail("Failed to fetch insider activity");
            }
        }

        [HttpGet("{symbol}/trades")]
        public async Task<IActionResult> GetTrades(string symbol)
        {
            string ticker = SanitizeTicker(symbol);

            try
            {
                var stockTrades = await _detailsService
                    .GetStockTradesAsync(ticker)
                    .WaitAsync(TimeSpan.FromSeconds(10));

                if (stockTrades == null)
                    return Fail("No trade data found");

                return Success(
                    new
                    {
                        stockTrades.Symbol,
                        stockTrades.CompanyName,
                        Trades = stockTrades.Trades ?? new TradesContainer(),
                        stockTrades.LastTradesUpdate,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Trades] Failed");
                return Fail("Error fetching trades");
            }
        }

        private string SanitizeTicker(string s) =>
            s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
                ? s.ToUpper()
                : $"{s.ToUpper()}.NS";
    }
}
