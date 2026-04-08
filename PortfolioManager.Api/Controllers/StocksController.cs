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

        public StocksController(
            StockDetailsService detailsService,
            IStockAnalysisService analysisService,
            IMongoDatabase database,
            PeerComparisonService peerComparisonService
        )
        {
            _detailsService = detailsService;
            _analysisService = analysisService;
            _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
            _peerService = peerComparisonService;
        }

        /// <summary>
        /// Unified Search: Used for both Global Search (Navbar) and Modal Search (Add to Portfolio).
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Ok(new List<object>());

            // Case-insensitive regex for Symbol and CompanyName
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

            // High-performance projection: Only fetch what the UI needs for the dropdown
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
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("details/{symbol}")]
        public async Task<IActionResult> GetDetails(string symbol, [FromQuery] string range = "1y")
        {
            string ticker = SanitizeTicker(symbol);
            var details = await _detailsService.GetStockDetailsAsync(ticker, range);

            if (details == null)
                return NotFound(new { message = $"Details unavailable for {ticker}" });

            return Ok(details);
        }

        [HttpGet("analyze/{symbol}")]
        public async Task<IActionResult> GetAnalysis(string symbol)
        {
            string ticker = SanitizeTicker(symbol);

            try
            {
                // 1. Fetch both Stock Details and Trades (Bulk/Block deals) in parallel for efficiency
                var detailsTask = _detailsService.GetStockDetailsAsync(ticker, "1y");
                var tradesTask = _detailsService.GetStockTradesAsync(ticker);

                await Task.WhenAll(detailsTask, tradesTask);

                var details = await detailsTask;
                var tradesData = await tradesTask;

                // 2. Validate essential data
                if (details == null)
                    return NotFound(new { message = "Stock data not found" });

                // 3. Run the updated analysis logic passing both datasets
                // This ensures the 'Smart Money' pillar is no longer "Missing"
                var analysis = _analysisService.AnalyzeStock(details, tradesData);

                return analysis != null
                    ? Ok(analysis)
                    : BadRequest(new { message = "Analysis calculation failed" });
            }
            catch (Exception ex)
            {
                // Log the exception here if you have a logger
                return StatusCode(
                    500,
                    new { error = "Internal server error during analysis", details = ex.Message }
                );
            }
        }

        [HttpGet("{symbol}/shareholding")]
        public async Task<IActionResult> GetShareholdingData(string symbol)
        {
            var stock = await _detailsService.GetStockDetailsAsync(symbol);
            if (stock == null || stock.Shareholding == null || !stock.Shareholding.Any())
                return NotFound();

            var quartersList = stock.Shareholding.First().Values.Keys.ToList();
            var latest = quartersList.Last();

            var pieData = stock
                .Shareholding.Where(s =>
                    !s.Category.Contains("Shareholders", StringComparison.OrdinalIgnoreCase)
                )
                .Select(s => new
                {
                    name = s.Category,
                    value = double.TryParse(s.Values.GetValueOrDefault(latest), out var v) ? v : 0,
                })
                .ToList();

            return Ok(
                new
                {
                    quarters = quartersList,
                    history = stock.Shareholding,
                    pieData = pieData,
                    latestQuarterName = latest,
                }
            );
        }

        [HttpGet("peers/{symbol}")]
        public async Task<IActionResult> GetPeers(string symbol)
        {
            string ticker = symbol.ToUpper().EndsWith(".NS")
                ? symbol.ToUpper()
                : $"{symbol.ToUpper()}.NS";

            var peerData = await _peerService.GetPeerIntelligenceAsync(ticker);

            if (peerData == null)
            {
                return NotFound(new { message = $"No peer data found for {symbol}" });
            }

            return Ok(peerData);
        }

        [HttpGet("recent-insider-activity")]
        public async Task<IActionResult> GetRecentInsiderActivity()
        {
            var filter = Builders<StockFundamental>.Filter.And(
                Builders<StockFundamental>.Filter.Exists(s => s.Trades.Insider),
                Builders<StockFundamental>.Filter.Not(
                    Builders<StockFundamental>.Filter.Size(s => s.Trades.Insider, 0)
                )
            );

            var recentStocks = await _fundamentalCollection
                .Find(filter)
                .SortByDescending(s => s.LastTradesUpdate)
                .Limit(10)
                .Project(s => new
                {
                    s.Symbol,
                    s.CompanyName,
                    InsiderCount = s.Trades.Insider.Count,
                })
                .ToListAsync();

            return Ok(recentStocks);
        }

        [HttpGet("{symbol}/trades")]
        public async Task<IActionResult> GetTrades(string symbol)
        {
            string ticker = SanitizeTicker(symbol);
            var stockTrades = await _detailsService.GetStockTradesAsync(ticker);

            if (stockTrades == null)
                return NotFound(new { message = $"No trade data found for {symbol}" });

            return Ok(
                new
                {
                    Symbol = stockTrades.Symbol,
                    CompanyName = stockTrades.CompanyName,
                    Trades = stockTrades.Trades ?? new TradesContainer(),
                    LastUpdate = stockTrades.LastTradesUpdate,
                }
            );
        }

        private string SanitizeTicker(string s) =>
            s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
                ? s.ToUpper()
                : $"{s.ToUpper()}.NS";
    }
}
