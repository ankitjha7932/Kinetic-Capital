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
            var resultObj = await _detailsService.GetStockDetailsAsync(ticker, "1y");

            if (resultObj == null)
                return NotFound(new { message = "Stock data not found" });

            try
            {
                var analysis = _analysisService.AnalyzeStock((StockDetails)resultObj);
                return analysis != null
                    ? Ok(analysis)
                    : BadRequest(new { message = "Analysis failed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
            }
        }

        [HttpGet("{symbol}/shareholding")]
        public async Task<IActionResult> GetShareholdingData(string symbol)
        {
            var stock = await _detailsService.GetStockDetailsAsync(symbol);
            if (stock == null || stock.Shareholding == null || !stock.Shareholding.Any())
                return NotFound();

            var quarters = stock.Shareholding.First().Values.Keys.ToList();
            var latestQuarter = quarters.Last();

            var pieData = stock
                .Shareholding.Where(s => !s.Category.Contains("Shareholders"))
                .Select(s => new
                {
                    name = s.Category,
                    value = double.TryParse(s.Values[latestQuarter], out var v) ? v : 0,
                })
                .ToList();

            return Ok(
                new
                {
                    Quarters = quarters,
                    History = stock.Shareholding,
                    PieData = pieData,
                    LatestQuarterName = latestQuarter,
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

        private string SanitizeTicker(string s) =>
            s.ToUpper().EndsWith(".NS") || s.ToUpper().EndsWith(".BO")
                ? s.ToUpper()
                : $"{s.ToUpper()}.NS";
    }
}
