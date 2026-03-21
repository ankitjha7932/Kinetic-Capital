using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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

        [AllowAnonymous]
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogCritical("GROQ_API_KEY not found.");
                return StatusCode(500, new { message = "Strategist Offline: API Key missing." });
            }

            string liveData = "No live ticker data available.";
            string newsContext = "No major news found.";

            try
            {
                // =========================
                // 📊 MARKET DATA
                // =========================
                if (!string.IsNullOrEmpty(req.Symbol))
                {
                    var history = await _priceService.GetHistoricalDataAsync(req.Symbol, "1d");
                    var last = history.Prices?.LastOrDefault();

                    if (last != null)
                    {
                        liveData =
                            $"Price: ₹{last.Close}, Volume: {last.Volume}, Time: {last.Date:HH:mm} IST";
                    }

                    var news = await _newsService.GetStockNewsAsync(req.Symbol);
                    if (news != null && news.Any())
                    {
                        newsContext = string.Join(
                            "\n",
                            news.Take(5).Select(n => $"- {n.Title} ({n.Source})")
                        );
                    }
                }

                // =========================
                // 🧠 PROMPT
                // =========================
                string combinedContext =
                    $"\n📊 Live Market:\n{liveData}\n\n📰 Latest News:\n{newsContext}";

                string fullPrompt = _promptService.GetKineticStrategistPrompt(
                    req.Symbol ?? "Market",
                    req.Message,
                    combinedContext
                );

                var payload = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[] { new { role = "user", content = fullPrompt } },
                    temperature = 0.7,
                };

                // =========================
                // 🚀 GROQ CALL
                // =========================
                var url = "https://api.groq.com/openai/v1/chat/completions";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Groq API Error: {Status} - {Response}",
                        response.StatusCode,
                        responseString
                    );

                    return StatusCode(
                        (int)response.StatusCode,
                        new { message = "Strategist Brain-Freeze", details = responseString }
                    );
                }

                // =========================
                // 🧠 PARSE AI RESPONSE
                // =========================
                using var doc = JsonDocument.Parse(responseString);

                var aiText = doc
                    .RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                // =========================
                // ✨ SPLIT ANSWER + FOLLOWUPS
                // =========================
                string answer = aiText ?? "";
                List<string> followUps = new List<string>();

                if (!string.IsNullOrEmpty(aiText) && aiText.Contains("Follow-ups:"))
                {
                    var parts = aiText.Split("Follow-ups:");

                    // Extract Answer
                    answer = parts[0].Replace("Answer:", "").Trim();

                    // Extract Follow-ups
                    if (parts.Length > 1)
                    {
                        followUps = parts[1]
                            .Split('\n')
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x =>
                                x.Replace("1.", "").Replace("2.", "").Replace("3.", "").Trim()
                            )
                            .Where(x => x.Length > 5)
                            .Take(3)
                            .ToList();
                    }
                }
                else
                {
                    // 🔥 FALLBACK FOLLOWUPS (IMPORTANT)
                    followUps = new List<string>
                    {
                        "Is this a good entry point?",
                        "What are the downside risks?",
                        "Should I wait for breakout?",
                    };
                }

                // =========================
                // 📦 FINAL RESPONSE
                // =========================
                return Ok(new { message = answer, followUps = followUps });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical failure for {Symbol}", req.Symbol);

                return StatusCode(
                    500,
                    new { message = "Strategist Core Failure", details = ex.Message }
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
