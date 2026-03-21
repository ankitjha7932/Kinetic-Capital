namespace PortfolioManager.Api.Services
{
    public class PromptService : IPromptService
    {
        public string GetKineticStrategistPrompt(string? symbol, string message, string context)
        {
            var intent = DetectIntent(message);

            return intent switch
            {
                PromptIntent.Volume => VolumePrompt(symbol, message, context),
                PromptIntent.News => NewsPrompt(symbol, message, context),
                PromptIntent.Decision => DecisionPrompt(symbol, message, context),
                PromptIntent.Fundamental => FundamentalPrompt(symbol, message, context),
                PromptIntent.Comparison => ComparisonPrompt(message, context),
                PromptIntent.Risk => RiskPrompt(symbol, message, context),
                PromptIntent.Detailed => DetailedPrompt(symbol, message, context),
                _ => PricePrompt(symbol, message, context),
            };
        }

        // =========================
        // 🧠 INTENT DETECTION
        // =========================
        private PromptIntent DetectIntent(string message)
        {
            message = message.ToLower();

            if (message.Contains("volume") || message.Contains("participation"))
                return PromptIntent.Volume;

            if (message.Contains("why") || message.Contains("reason"))
                return PromptIntent.News;

            if (message.Contains("buy") || message.Contains("sell") || message.Contains("should i"))
                return PromptIntent.Decision;

            if (
                message.Contains("pe")
                || message.Contains("valuation")
                || message.Contains("growth")
                || message.Contains("results")
            )
                return PromptIntent.Fundamental;

            if (message.Contains("vs") || message.Contains("compare"))
                return PromptIntent.Comparison;

            if (
                message.Contains("risk")
                || message.Contains("overvalued")
                || message.Contains("safe")
            )
                return PromptIntent.Risk;

            if (
                message.Contains("complete")
                || message.Contains("detailed")
                || message.Contains("analysis")
                || message.Contains("holding")
                || message.Contains("loss")
                || message.Contains("average")
            )
                return PromptIntent.Detailed;

            return PromptIntent.Price;
        }

        // =========================
        // 🔥 BASE CORE (UPDATED)
        // =========================
        private string Base(string context) =>
            $@"
You are 'Kinetic Intelligence', an elite Indian stock strategist.

CONTEXT:
{context}

MACRO BACKDROP:
- Ongoing geopolitical tension (US–Israel–Iran)
- Expect volatility, liquidity shifts, and sentiment swings

RESPONSE LENGTH:
- Normal → 120–180 words
- Detailed → 250–400 words

INTELLIGENCE RULES:
- No templates
- Think like a hedge fund analyst
- Use: Money Flow, Institutional Activity, Participation
- Bold key numbers like ₹**XXX**, **10%**

VOLUME INTELLIGENCE:
- Focus on **3 PM volume vs today's average**
- High → Institutional Activity
- Low → Weak participation
- **0** → Ignore (data gap/post-market)

STYLE:
- Natural, sharp, non-repetitive
- Insight > explanation

IMPORTANT:
- Always include actionable insight
- Always include risk awareness when relevant

OUTPUT FORMAT:

Answer:
<your analysis>

Follow-ups:
1. <short smart question>
2. <short smart question>
3. <short smart question>

End with:
This is Market Logic, not formal financial advice.
";

        // =========================
        // 📊 PRICE
        // =========================
        private string PricePrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Analyze price trend and momentum.

- Trend direction
- Strength of move
- Participation quality
- Add actionable insight with **10% Stop Loss**

User Question:
{msg}
";

        // =========================
        // 📦 VOLUME
        // =========================
        private string VolumePrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Analyze volume using smart money logic.

- 3 PM vs average volume
- Institutional vs retail activity
- Accumulation vs weak participation

User Question:
{msg}
";

        // =========================
        // 📰 NEWS
        // =========================
        private string NewsPrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Explain WHY the stock is moving.

- Key trigger
- News vs sentiment move
- Link with Money Flow

User Question:
{msg}
";

        // =========================
        // ⚖️ DECISION
        // =========================
        private string DecisionPrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Give a clear decision.

- Buy / Hold / Wait
- Justify with trend + participation
- Suggest entry (dip/breakout)
- Include **10% Stop Loss**

User Question:
{msg}
";

        // =========================
        // 📉 FUNDAMENTAL
        // =========================
        private string FundamentalPrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Evaluate fundamentals.

- Profit consistency
- Margins
- Growth sustainability
- Valuation view

User Question:
{msg}
";

        // =========================
        // 🆚 COMPARISON
        // =========================
        private string ComparisonPrompt(string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Compare stocks.

- Growth vs stability
- Money Flow difference
- Risk vs reward

User Question:
{msg}
";

        // =========================
        // ⚠️ RISK
        // =========================
        private string RiskPrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Analyze risk.

- Downside potential
- Participation weakness
- Macro impact

User Question:
{msg}
";

        // =========================
        // 🔥 DETAILED
        // =========================
        private string DetailedPrompt(string? symbol, string msg, string ctx) =>
            $@"
{Base(ctx)}

TASK:
Provide complete analysis.

Cover:

1. Price Action (trend + momentum)
2. Money Flow & Participation
3. Volume (3 PM vs avg)
4. News & Sentiment
5. Fundamentals
6. Strategy (Buy/Hold/Wait + **10% SL**)
7. Risk

If user is in loss → include averaging logic.

User Question:
{msg}
";
    }

    public enum PromptIntent
    {
        Price,
        Volume,
        News,
        Fundamental,
        Decision,
        Comparison,
        Risk,
        Detailed,
    }
}
