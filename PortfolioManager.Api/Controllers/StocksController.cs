using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
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
        private readonly IStockAnalysisService _analysisService;

        // FIXED: The service must be injected via the constructor
        public StocksController(
            StockDetailsService detailsService,
            IStockAnalysisService analysisService
        )
        {
            _detailsService = detailsService;
            _analysisService = analysisService;

            // Ensure CSV is only parsed once per application lifecycle
            if (_allStocks.Count > 0)
                return;

            lock (_lock)
            {
                if (_allStocks.Count > 0)
                    return;
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
                            string nseSymbol = rawSymbol.EndsWith(".NS")
                                ? rawSymbol
                                : $"{rawSymbol}.NS";
                            string faceVal = columns[7].Trim();

                            _allStocks.Add(
                                new StockMaster(
                                    Symbol: nseSymbol,
                                    Name: columns[1].Trim(),
                                    Sector: "Equity",
                                    FaceValue: faceVal
                                )
                            );
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
                .Where(s =>
                    s.Symbol.Contains(query.ToUpper())
                    || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
                .Take(15)
                .ToList();

            return Ok(results);
        }

        // NEW: Screener-style deep dive endpoint
        [HttpGet("details/{symbol}")]
        public async Task<IActionResult> GetDetails(string symbol, [FromQuery] string range = "1y")
        {
            string ticker = symbol.ToUpper().EndsWith(".NS")
                ? symbol.ToUpper()
                : $"{symbol.ToUpper()}.NS";

            // 1. Fetch FaceValue from the CSV list (as you were doing before)
            var localStock = _allStocks.FirstOrDefault(s =>
                s.Symbol.Equals(ticker, StringComparison.OrdinalIgnoreCase)
            );

            string faceValueFromCsv = localStock?.FaceValue ?? "N/A";

            // 2. Pass the CSV-sourced faceValue to the service
            var details = await _detailsService.GetStockDetailsAsync(
                ticker,
                range,
                faceValueFromCsv
            );

            if (details == null)
                return NotFound(new { message = $"Details unavailable for {ticker}" });

            return Ok(details);
        }

        [HttpGet("analyze/{symbol}")]
        public async Task<IActionResult> GetAnalysis(string symbol)
        {
            string ticker = symbol.ToUpper().EndsWith(".NS")
                ? symbol.ToUpper()
                : $"{symbol.ToUpper()}.NS";

            // Explicitly cast the result to StockDetails
            var resultObj = await _detailsService.GetStockDetailsAsync(ticker, "1y", "N/A");

            if (resultObj == null)
                return NotFound(new { message = "Stock data not found for analysis" });

            // This explicit cast solves CS0266
            StockDetails details = (StockDetails)resultObj;

            try
            {
                var analysis = _analysisService.AnalyzeStock(details);

                return analysis != null
                    ? Ok(analysis)
                    : BadRequest(new { message = "Analysis engine could not process the data" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Analysis Exception]: {ex.Message}");
                return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
            }
        }
    }

    public record StockMaster(string Symbol, string Name, string Sector, string FaceValue);
}
