using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private static readonly List<StockMaster> _allStocks = new();
        private static readonly object _lock = new();
        private readonly StockDetailsService _detailsService;

        // FIXED: The service must be injected via the constructor
        public StocksController(StockDetailsService detailsService)
        {
            _detailsService = detailsService;

            // Ensure CSV is only parsed once per application lifecycle
            if (_allStocks.Count > 0) return;

            lock (_lock)
            {
                if (_allStocks.Count > 0) return;
                LoadStocksFromCsv();
            }
        }

        private void LoadStocksFromCsv()
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "EQUITY_L.csv");

                if (System.IO.File.Exists(path))
                {
                    var lines = System.IO.File.ReadAllLines(path);
                    foreach (var line in lines.Skip(1))
                    {
                        var columns = line.Split(',');
                        if (columns.Length > 1)
                        {
                            string rawSymbol = columns[0].Trim();
                            string nseSymbol = rawSymbol.EndsWith(".NS") ? rawSymbol : $"{rawSymbol}.NS";
                            string faceVal = columns[7].Trim();

                            _allStocks.Add(new StockMaster(
                                Symbol: nseSymbol,
                                Name: columns[1].Trim(),
                                Sector: "Equity",
                                FaceValue: faceVal
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to read CSV: {ex.Message}");
            }
        }

        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<StockMaster>), 200)]
        public IActionResult Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Ok(Enumerable.Empty<StockMaster>());

            var results = _allStocks
                .Where(s => s.Symbol.Contains(query.ToUpper()) ||
                            s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(15)
                .ToList();

            return Ok(results);
        }

        // NEW: Screener-style deep dive endpoint
        [HttpGet("details/{symbol}")]
        public async Task<IActionResult> GetDetails(string symbol, [FromQuery] string range = "1y")
        {
            // Ensure we use the proper .NS ticker
            string ticker = symbol.ToUpper().EndsWith(".NS") ? symbol.ToUpper() : $"{symbol.ToUpper()}.NS";

            var localStock = _allStocks.FirstOrDefault(s => s.Symbol.Equals(ticker, StringComparison.OrdinalIgnoreCase));
            string faceValue = localStock?.FaceValue ?? "N/A";

            // Pass the range (e.g., "1m", "6m", "5y") to the service
            var details = await _detailsService.GetStockDetailsAsync(ticker, range, faceValue);

            if (details == null)
                return NotFound(new { message = $"Details unavailable for {ticker}" });

            return Ok(details);
        }

        [HttpGet("analyze/{symbol}")]
        public IActionResult GetAnalysis(string symbol)
        {
            var stock = _allStocks.FirstOrDefault(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (stock == null) return NotFound();

            var random = new Random();
            var sentiments = new[] { "Bullish", "Neutral", "Accumulating", "Bearish" };
            var sentiment = sentiments[random.Next(sentiments.Length)];

            return Ok(new
            {
                Symbol = stock.Symbol,
                Name = stock.Name,
                Sentiment = sentiment,
                Summary = $"{stock.Symbol} is currently showing {sentiment.ToLower()} patterns.",
                RiskScore = random.Next(1, 100),
                LastUpdated = DateTime.UtcNow
            });
        }
    }

    public record StockMaster(string Symbol, string Name, string Sector, string FaceValue);
}