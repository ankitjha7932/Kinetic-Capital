namespace PortfolioManager.Api.Services
{
    /// <summary>
    /// Structured market context passed into prompt generation.
    /// Keeps the controller clean and lets the prompt layer make smart decisions.
    /// </summary>
    public record MarketContext
    {
        public string? Symbol { get; init; }
        public decimal? LastPrice { get; init; }
        public long? Volume { get; init; }
        public decimal? Change { get; init; }
        public decimal? ChangePercent { get; init; }
        public DateTime? DataTimestamp { get; init; }
        public IReadOnlyList<string> RecentHeadlines { get; init; } = Array.Empty<string>();

        /// <summary>True when at least price or volume is populated with real data.</summary>
        public bool HasLiveData => LastPrice.HasValue || Volume.HasValue;
    }

    /// <summary>
    /// A single turn in a multi-turn conversation.
    /// Pass the full history so the model maintains coherence across follow-up questions.
    /// </summary>
    public record ConversationMessage(string Role, string Content);

    public interface IPromptService
    {
        /// <summary>
        /// Builds the full system + user prompt for the strategist model.
        /// </summary>
        /// <param name="userMessage">The raw user question.</param>
        /// <param name="context">Structured live market data. Pass an empty record if unavailable.</param>
        /// <param name="conversationHistory">Prior turns. Empty list for first message.</param>
        /// <returns>A list of messages ready to be sent to the LLM API.</returns>
        IReadOnlyList<object> BuildMessages(
            string userMessage,
            MarketContext context,
            IReadOnlyList<ConversationMessage> conversationHistory
        );

        /// <summary>
        /// Generates contextually relevant follow-up suggestions based on intent and symbol.
        /// Intentionally non-deterministic to avoid stale, repetitive chips.
        /// </summary>
        IReadOnlyList<string> GetContextualFollowUps(
            string userMessage,
            MarketContext context,
            string? aiAnswer = null
        );
    }
}