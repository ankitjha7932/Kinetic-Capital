using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IPromptService _promptService;
        private readonly StockPriceService _priceService;
        private readonly NewsService _newsService;
        private readonly ILogger<ChatController> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();

        public ChatController(
            IPromptService promptService,
            StockPriceService priceService,
            NewsService newsService,
            ILogger<ChatController> logger
        )
        {
            _promptService = promptService;
            _priceService = priceService;
            _newsService = newsService;
            _logger = logger;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            // 1. API KEY CHECK
            // Ensure you have set this in your system environment or launchSettings.json
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("GEMINI_API_KEY is missing from environment variables.");
                return StatusCode(500, new { message = "Strategist Offline: API Key Missing" });
            }

            string liveData = "No live ticker data.";
            string newsContext = "No major news.";

            try
            {
                if (!string.IsNullOrEmpty(req.Symbol))
                {
                    // 📊 PRICE DATA (Uses your updated 2-parameter method)
                    var history = await _priceService.GetHistoricalDataAsync(req.Symbol, "1d");
                    var last = history.Prices?.LastOrDefault();

                    if (last != null)
                    {
                        liveData =
                            $"Price: ₹{last.Close}, Volume: {last.Volume}, Time: {last.Date:HH:mm} IST";
                    }

                    // 📰 NEWS DATA
                    var news = await _newsService.GetStockNewsAsync(req.Symbol);
                    if (news != null && news.Any())
                    {
                        var cleanSymbol = req.Symbol.Replace(".NS", "").ToLower();
                        var filteredNews = news.Where(n => n.Title.ToLower().Contains(cleanSymbol))
                            .Take(5)
                            .ToList();

                        if (!filteredNews.Any())
                            filteredNews = news.Take(5).ToList();

                        newsContext = string.Join(
                            "\n",
                            filteredNews.Select(n => $"- {n.Title} ({n.Source})")
                        );
                    }
                }

                // 🧠 PROMPT CONSTRUCTION
                string combinedContext =
                    $"\n📊 Live Market:\n{liveData}\n\n📰 Latest News:\n{newsContext}";
                string fullPrompt = _promptService.GetKineticStrategistPrompt(
                    req.Symbol ?? "Market",
                    req.Message,
                    combinedContext
                );

                var payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = fullPrompt } } } },
                };

                // 🎯 GEMINI API CALL
                // Note: gemini-1.5-flash is currently the most stable production model
                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

                var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"
                    )
                );

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Gemini API Error: {Status} - {Response}",
                        response.StatusCode,
                        responseString
                    );
                    return StatusCode(
                        500,
                        new { message = "Strategist Logic Loop", details = responseString }
                    );
                }

                using var doc = JsonDocument.Parse(responseString);

                // Safe parsing of the Gemini response
                if (
                    doc.RootElement.TryGetProperty("candidates", out var candidates)
                    && candidates.GetArrayLength() > 0
                )
                {
                    var aiText = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return Ok(new { message = aiText });
                }

                return StatusCode(500, new { message = "Empty response from intelligence core." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical failure in ChatController for {Symbol}", req.Symbol);
                return StatusCode(
                    500,
                    new { message = "Strategist Core Offline", details = ex.Message }
                );
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? Symbol { get; set; }
    }
}
