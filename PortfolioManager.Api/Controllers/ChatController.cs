using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services; // ConversationMessage, MarketContext, ChatRequest, ChatResponse all live here
 
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
 
        // Single shared HttpClient — never instantiate per-request
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
 
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
 
        /// <summary>
        /// Main chat endpoint.
        ///
        /// Accepts an optional conversation history so the model maintains context across
        /// follow-up questions. The client is responsible for echoing history back each turn
        /// (stateless by design — no server-side session state needed).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            using var scope = _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["Symbol"] = req.Symbol ?? "NONE",
                }
            );
 
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogCritical("GEMINI_API_KEY environment variable not set.");
                return StatusCode(500, Error("Strategist offline — API key missing."));
            }
 
            // ── 1. Fetch market context (non-blocking on failure) ─────────────────
            var context = await BuildMarketContextAsync(req.Symbol);
 
            // ── 2. Build messages (system + history + current user turn) ──────────
            var history = req.History ?? Array.Empty<ConversationMessage>();
            var messages = _promptService.BuildMessages(req.Message, context, history);
 
            // ── 3. Call Groq ──────────────────────────────────────────────────────
            var payload = new
            {
                model = "llama-3.3-70b-versatile",
                messages,
                temperature = 0.75, // slightly higher = more varied phrasing each time
                max_tokens = 700,
                top_p = 0.9,
            };
 
            string responseString;
            try
            {
                using var httpReq = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.groq.com/openai/v1/chat/completions"
                );
                httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                httpReq.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );
 
                var httpResp = await _httpClient.SendAsync(
                    httpReq,
                    HttpCompletionOption.ResponseContentRead
                );
                responseString = await httpResp.Content.ReadAsStringAsync();
 
                if (!httpResp.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Groq API error {Status}: {Body}",
                        httpResp.StatusCode,
                        responseString
                    );
                    return StatusCode(
                        (int)httpResp.StatusCode,
                        Error("Strategist temporary failure — please retry.", responseString)
                    );
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Groq API timed out for correlation {Id}", correlationId);
                return StatusCode(504, Error("Request timed out — Groq is slow right now."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP failure calling Groq");
                return StatusCode(500, Error("Strategist network failure.", ex.Message));
            }
 
            // ── 4. Parse AI response ──────────────────────────────────────────────
            string answer;
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                answer =
                    doc.RootElement.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Groq response: {Raw}", responseString);
                return StatusCode(500, Error("Could not parse strategist response."));
            }
 
            // ── 5. Generate follow-ups independently (no model round-trip needed) ─
            var followUps = _promptService.GetContextualFollowUps(req.Message, context, answer);
 
            // ── 6. Echo updated history back so client can pass it next turn ───────
            var updatedHistory = history
                .Append(new ConversationMessage("user", req.Message))
                .Append(new ConversationMessage("assistant", answer))
                .ToList();
 
            Response.Headers.Append("X-Correlation-Id", correlationId);
 
            return Ok(
                new ChatResponse
                {
                    Message = answer,
                    FollowUps = followUps,
                    History = updatedHistory,
                    HasLiveData = context.HasLiveData,
                    Symbol = context.Symbol,
                }
            );
        }
 
        // ─── Helpers ──────────────────────────────────────────────────────────────
 
        /// <summary>
        /// Fetches price + news in parallel. Silently degrades if either fails —
        /// the prompt layer is designed to handle missing data gracefully.
        /// </summary>
        private async Task<MarketContext> BuildMarketContextAsync(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return new MarketContext();
 
            var priceTask = FetchPriceAsync(symbol);
            var newsTask = FetchNewsAsync(symbol);
            await Task.WhenAll(priceTask, newsTask);
 
            var (price, change, changePercent, volume, timestamp) = priceTask.Result;
            var headlines = newsTask.Result;
 
            return new MarketContext
            {
                Symbol = symbol.ToUpperInvariant(),
                LastPrice = price,
                Change = change,
                ChangePercent = changePercent,
                Volume = volume,
                DataTimestamp = timestamp,
                RecentHeadlines = headlines,
            };
        }
 
        private async Task<(
            decimal? Price,
            decimal? Change,
            decimal? ChangePercent,
            long? Volume,
            DateTime? Timestamp
        )> FetchPriceAsync(string symbol)
        {
            try
            {
                var history = await _priceService.GetHistoricalDataAsync(symbol, "5d");
                var last = history.Prices?.LastOrDefault();
                if (last is null)
                    return default;
 
                return (last.Close, null, null, last.Volume, last.Date);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Price fetch failed for {Symbol}", symbol);
                return default;
            }
        }
 
        private async Task<IReadOnlyList<string>> FetchNewsAsync(string symbol)
        {
            try
            {
                var news = await _newsService.GetStockNewsAsync(symbol);
                return news?.Take(5).Select(n => $"{n.Title} — {n.Source}").ToList()
                    ?? (IReadOnlyList<string>)Array.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "News fetch failed for {Symbol}", symbol);
                return Array.Empty<string>();
            }
        }
 
        private static object Error(string message, string? details = null) =>
            details is null ? new { message } : new { message, details };
    }
 
}
 
// ─── Request / Response contracts (in Services namespace so ConversationMessage is in scope) ───
namespace PortfolioManager.Api.Services
{
    public class ChatRequest
    {
        /// <summary>The user's current message.</summary>
        public string Message { get; set; } = string.Empty;
 
        /// <summary>Optional NSE/BSE ticker. Leave null for general market questions.</summary>
        public string? Symbol { get; set; }
 
        /// <summary>
        /// Full conversation history from the previous response's History field.
        /// Null or empty for the first message.
        /// </summary>
        public IReadOnlyList<ConversationMessage>? History { get; set; }
    }
 
    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<string> FollowUps { get; set; } = Array.Empty<string>();
 
        /// <summary>
        /// Updated conversation history. Client echoes this back as History in the next request.
        /// </summary>
        public IReadOnlyList<ConversationMessage> History { get; set; } =
            Array.Empty<ConversationMessage>();
 
        public bool HasLiveData { get; set; }
        public string? Symbol { get; set; }
    }
}
