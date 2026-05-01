using System.Text;

namespace PortfolioManager.Api.Services
{
    /// <summary>
    /// Builds non-repetitive, intent-aware prompts for the Kinetic Intelligence strategist.
    ///
    /// Design principles:
    ///  1. Every prompt is composed from a randomised "analytical lens" so the same question
    ///     never produces a copy-paste answer.
    ///  2. Graceful degradation — no live data is NOT a blocker; the model falls back to
    ///     sector/macro reasoning WITHOUT inventing specific price levels.
    ///  3. System prompt and user turns are separated — lets us maintain conversation history
    ///     without bloating the system prompt on every turn.
    ///  4. Follow-ups are intent-specific and rotated from a pool, not hardcoded strings.
    ///  5. HALLUCINATION GUARDRAILS: model is explicitly forbidden from inventing price levels,
    ///     corporate events, or using non-INR currency symbols.
    /// </summary>
    public class PromptService : IPromptService
    {
        private static readonly Random _rng = new();

        // ─── Analytical lenses rotated per call ──────────────────────────────────
        private static readonly string[] PriceLenses =
        [
            "Focus on momentum divergence — is price leading or lagging volume?",
            "Examine the supply/demand zone: where are sellers positioned relative to current price?",
            "Use a trend-strength lens: is this move impulsive or corrective?",
            "Think in terms of higher-highs / lower-lows structure. Where does this print sit?",
            "Analyse through the lens of mean-reversion vs breakout continuation.",
        ];

        private static readonly string[] VolumeLenses =
        [
            "Identify smart money footprint: block trades vs retail churn.",
            "Compare today's participation to the 20-session average. Is conviction rising?",
            "Spot divergence: price up, volume down = warning. Price up, volume surge = confirm.",
            "Is this an accumulation day or a distribution dump disguised as a rally?",
            "Look at the intraday volume profile — which price levels attracted the most activity?",
        ];

        private static readonly string[] NewsLenses =
        [
            "Separate the catalyst from the narrative: what actually moved money?",
            "Is the market pricing in the headline or the implication 2 quarters out?",
            "Flag whether this is sector-wide sentiment or company-specific alpha.",
            "Is this news-driven buying likely to sustain, or is it a fade setup?",
            "Map the news to institutional positioning — do they already own this story?",
        ];

        private static readonly string[] DecisionLenses =
        [
            "Use a risk-first framework: define the max loss before the upside target.",
            "Think like a position trader: what does the weekly chart say vs the daily noise?",
            "Apply the three-question test: Trend? Catalyst? Risk/Reward > 2:1?",
            "Consider the opportunity cost — what else could this capital do right now?",
            "Construct a scenario tree: base case, bull case, bear case probabilities.",
            "Give a clear verdict first: Buy / Sell / Wait — then justify with price structure and participation.",
        ];

        private static readonly string[] FundamentalLenses =
        [
            "Focus on earnings quality — is profit growth cash-backed or accounting-inflated?",
            "Assess capital allocation: is management buying back stock or diluting?",
            "Evaluate the moat: pricing power, switching costs, or just a commodity play?",
            "Normalise margins across the cycle. Are current numbers peak or trough?",
            "Check the debt maturity profile — refinancing risk in a rising-rate environment.",
        ];

        private static readonly string[] RiskLenses =
        [
            "Identify the single biggest tail risk that the market is ignoring.",
            "Map correlation risk: how does this name move when the Nifty drops 3%?",
            "Stress-test: what happens to this thesis if crude spikes or FIIs turn net sellers?",
            "Locate the consensus — being wrong means being crowded in the wrong direction.",
            "Discuss downside structure: support zones and narrative breakdown point — in qualitative terms if no live data.",
        ];

        // ─── Follow-up pools (rotated per intent) ────────────────────────────────
        private static readonly Dictionary<PromptIntent, string[]> FollowUpPools = new()
        {
            [PromptIntent.Price] =
            [
                "Where is the next strong resistance?",
                "Is the current trend supported by volume?",
                "What's a good stop-loss level right now?",
                "Is this a breakout or a bull trap?",
                "What does the weekly chart look like?",
                "Is momentum slowing on the daily?",
            ],
            [PromptIntent.Volume] =
            [
                "Is this accumulation or distribution?",
                "Did FIIs participate in today's move?",
                "How does today's volume compare to last week's average?",
                "What's the delivery percentage telling us?",
                "Is the volume spike a one-day event or a trend?",
            ],
            [PromptIntent.News] =
            [
                "Will this catalyst sustain for more than a week?",
                "Is the broader sector also reacting?",
                "Has the news already been priced in?",
                "What's the counter-narrative bears would argue?",
                "Any upcoming events that could reverse this?",
            ],
            [PromptIntent.Decision] =
            [
                "What's the ideal entry point on a dip?",
                "Should I wait for a confirmed breakout?",
                "What's a realistic 3-month target?",
                "What invalidates this trade thesis?",
                "Is this a swing trade or a positional hold?",
            ],
            [PromptIntent.Fundamental] =
            [
                "How does the valuation compare to sector peers?",
                "Is the growth rate sustainable for 3 more years?",
                "Are promoters buying or selling?",
                "What's the dividend yield vs fixed deposits right now?",
                "Any red flags in the latest quarterly results?",
            ],
            [PromptIntent.Comparison] =
            [
                "Which one has stronger institutional holding?",
                "Which is better for a 1-year horizon?",
                "Which carries less macro risk?",
                "Which has better earnings visibility?",
            ],
            [PromptIntent.Risk] =
            [
                "What's the probability of hitting the stop-loss?",
                "How correlated is this to broader market risk?",
                "What macro trigger would hurt this most?",
                "Is the risk asymmetric in my favour?",
                "Should I hedge this position?",
            ],
            [PromptIntent.Detailed] =
            [
                "What would make you turn bearish on this?",
                "Is this a good stock for SIP-style averaging?",
                "How does it behave in a market correction?",
                "What's the institutional consensus on this name?",
                "Should I trim on strength or add on dips?",
            ],
        };

        // ─── Public API ───────────────────────────────────────────────────────────

        public IReadOnlyList<object> BuildMessages(
            string userMessage,
            MarketContext context,
            IReadOnlyList<ConversationMessage> conversationHistory
        )
        {
            var intent = DetectIntent(userMessage);
            var systemPrompt = BuildSystemPrompt(context, intent);
            var taskInstruction = BuildTaskInstruction(userMessage, context, intent);

            var messages = new List<object> { new { role = "system", content = systemPrompt } };

            // Inject prior turns (up to last 6 to keep context window sane)
            foreach (var turn in conversationHistory.TakeLast(6))
            {
                messages.Add(new { role = turn.Role, content = turn.Content });
            }

            // Current user turn — task instruction wraps the raw message with analytical framing
            messages.Add(new { role = "user", content = taskInstruction });

            return messages;
        }

        public IReadOnlyList<string> GetContextualFollowUps(
            string userMessage,
            MarketContext context,
            string? aiAnswer = null
        )
        {
            var intent = DetectIntent(userMessage);
            var pool = FollowUpPools.GetValueOrDefault(intent, FollowUpPools[PromptIntent.Price]);

            // Shuffle and pick 3 distinct questions
            return pool.OrderBy(_ => _rng.Next()).Take(3).ToList();
        }

        // ─── System Prompt ────────────────────────────────────────────────────────

        private static string BuildSystemPrompt(MarketContext context, PromptIntent intent)
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                "You are Kinetic Intelligence — a senior Indian equity strategist with the instincts of a hedge fund PM and the directness of a trading desk head."
            );
            sb.AppendLine();

            // ABSOLUTE RULES — placed first so they are highest priority in the context window
            sb.AppendLine("## ABSOLUTE RULES — NEVER VIOLATE THESE");
            sb.AppendLine(
                "RULE 1 — CURRENCY: This is the Indian stock market (NSE/BSE). Use ₹ (Indian Rupee) for ALL monetary values. NEVER write $, USD, or any other currency — not even once. Violation = incorrect response."
            );
            sb.AppendLine(
                "RULE 2 — PRICE LEVELS: When a live price is provided in the snapshot, you MUST derive and state actionable levels from it — stop-loss (typically 8-10% below CMP), immediate support/resistance zones, and a realistic target. Do NOT invent prices when no live data exists — use 'check your live chart' instead."
            );
            sb.AppendLine(
                "RULE 3 — NO FABRICATED NEWS OR EVENTS: You MUST NOT reference any acquisition, deal, partnership, quarterly result, or corporate event unless it appears word-for-word in the 'Recent Headlines' section below. If that section says NONE, say 'No specific catalyst in my feed this session' — fabricating news is a critical failure."
            );
            sb.AppendLine(
                "RULE 4 — NO HALLUCINATED FIGURES: Do not invent PE ratios, revenue numbers, analyst targets, or deal sizes. If you are not certain, speak in structural terms ('historically this sector trades at a premium', 'pharma exports typically...') without specific numbers."
            );
            sb.AppendLine();

            sb.AppendLine("## Core Behaviour");
            sb.AppendLine(
                "- Never produce the same analytical angle twice. Rotate your framing, vocabulary, and structure each response."
            );
            sb.AppendLine(
                "- When live data is missing, discuss sector dynamics, macro positioning, historical patterns, and structural tendencies — WITHOUT inventing specific numbers."
            );
            sb.AppendLine(
                "- Data gap transparency: if no live price is available, open with 'No live feed this session — speaking to the structural picture' then proceed with qualitative analysis."
            );
            sb.AppendLine(
                "- Actionable > Academic. Every response must end with something the user can actually DO."
            );
            sb.AppendLine(
                "- When live price is available, always state a directional bias (Bullish / Bearish / Neutral) with one-line reasoning. Also, use easier word for Bullish / Bearish thing that user can understand easily. Then give the levels. Also explain users why you are giving bearish, bullish or neutral call on basis of news, shareholding pattern, financials, recent news and other things"
            );
            sb.AppendLine();

            // ── Live Market Snapshot ──
            sb.AppendLine("## Live Market Snapshot");
            if (context.HasLiveData)
            {
                if (context.LastPrice.HasValue)
                    sb.AppendLine(
                        $"- Price: ₹{context.LastPrice:N2}"
                            + (
                                context.ChangePercent.HasValue
                                    ? $"  ({(context.ChangePercent > 0 ? "+" : "")}{context.ChangePercent:F2}%)"
                                    : ""
                            )
                    );
                if (context.Volume.HasValue)
                    sb.AppendLine($"- Volume: {context.Volume:N0}");
                if (context.DataTimestamp.HasValue)
                    sb.AppendLine(
                        $"- As of: {context.DataTimestamp.Value:HH:mm} IST, {context.DataTimestamp.Value:dd MMM yyyy}"
                    );
            }
            else
            {
                sb.AppendLine(
                    "- STATUS: NO LIVE DATA. You have ZERO knowledge of this stock's current or recent price. Do not guess. Do not reference any company-specific news, deals, or events. Speak ONLY about sector-level macro dynamics."
                );
            }

            // ── News Headlines ──
            sb.AppendLine();
            if (context.RecentHeadlines.Count > 0)
            {
                sb.AppendLine(
                    "## Recent Headlines (Per Rule 3: ONLY reference these headlines — nothing else)"
                );
                foreach (var h in context.RecentHeadlines.Take(5))
                    sb.AppendLine($"- {h}");
            }
            else
            {
                sb.AppendLine(
                    "## Recent Headlines: NONE. Per Rule 3, do NOT reference any news, deal, acquisition, or corporate event."
                );
            }

            // ── Macro Backdrop ──
            sb.AppendLine();
            sb.AppendLine("## Macro Backdrop");
            sb.AppendLine(
                "- Elevated geopolitical risk: US-Middle East tensions, INR volatility pressure."
            );
            sb.AppendLine(
                "- FII flows are the swing factor — watch net daily DII vs FII positioning."
            );
            sb.AppendLine(
                "- RBI stance: monitor liquidity and rate trajectory for rate-sensitive sectors."
            );

            // ── Output Contract ──
            sb.AppendLine();
            sb.AppendLine("## Output Contract");
            sb.AppendLine(
                "- Length: 120–180 words for targeted questions; 280–400 words for detailed analysis."
            );
            sb.AppendLine(
                "- Format: Plain prose with **bold** for key signals. No bullet-point dumps."
            );
            sb.AppendLine(
                "- Close with exactly one instance of: 'Market Logic — not formal financial advice.' — ONLY at the very end. Never mid-response. Also use different versions of this statement so that it won't sound generic"
            );
            sb.AppendLine("- Do NOT add 'Follow-ups' to the answer body.");

            return sb.ToString();
        }

        // ─── Task Instruction (per-turn user message) ─────────────────────────────

        private string BuildTaskInstruction(
            string userMessage,
            MarketContext context,
            PromptIntent intent
        )
        {
            var lens = PickLens(intent);
            var symbolLine = context.Symbol is not null
                ? $"Stock in focus: **{context.Symbol}** (NSE/BSE — all prices in ₹)"
                : "No specific symbol — apply general Indian market reasoning.";

            var dataReminder = context.HasLiveData
                ? $"Confirmed live price: ₹{context.LastPrice:N2}. Derive and state: stop-loss (8-10% below = ₹{context.LastPrice * 0.91m:N0}–₹{context.LastPrice * 0.92m:N0}), support/resistance zones relative to CMP, and a target if trend supports it."
                : "NO live price data — do NOT invent or quote any specific price level (Rule 2).";
            var newsReminder =
                context.RecentHeadlines.Count == 0
                    ? "No headlines confirmed — do NOT cite any deal, acquisition or analyst target by name."
                    : $"Only cite these headlines: {string.Join(", ", context.RecentHeadlines)}";

            return $"""
                {symbolLine}
                {dataReminder}
                {newsReminder}

                Analytical lens: {lens}

                User question:
                {userMessage}

                Think through the lens above — do not quote or acknowledge it. Let it invisibly shape your analysis.
                Final check before responding: Is everything in ₹? Did I avoid invented numbers and fabricated news? Good.
                """;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static string PickLens(PromptIntent intent) =>
            intent switch
            {
                PromptIntent.Volume => Pick(VolumeLenses),
                PromptIntent.News => Pick(NewsLenses),
                PromptIntent.Decision => Pick(DecisionLenses),
                PromptIntent.Fundamental => Pick(FundamentalLenses),
                PromptIntent.Risk => Pick(RiskLenses),
                PromptIntent.Comparison => Pick(RiskLenses),
                PromptIntent.Detailed =>
                    "Answer each question the user asked as a separate paragraph with a bold label. Be specific per question — price levels from live data, macro impact as probability, fundamentals from known sector knowledge.",
                _ => Pick(PriceLenses),
            };

        private static string Pick(string[] pool) => pool[_rng.Next(pool.Length)];

        private static PromptIntent DetectIntent(string message)
        {
            var m = message.ToLowerInvariant();
            int questionMarks = m.Count(c => c == '?');
            if (questionMarks >= 2)
                return PromptIntent.Detailed;

            if (
                m.Contains("volume")
                || m.Contains("participation")
                || m.Contains("delivery")
                || m.Contains("block deal")
            )
                return PromptIntent.Volume;

            if (
                m.Contains("why")
                || m.Contains("reason")
                || m.Contains("news")
                || m.Contains("catalyst")
                || m.Contains("trigger")
            )
                return PromptIntent.News;

            if (
                m.Contains("buy")
                || m.Contains("sell")
                || m.Contains("should i")
                || m.Contains("entry")
                || m.Contains("exit")
            )
                return PromptIntent.Decision;

            if (
                m.Contains("pe")
                || m.Contains("valuation")
                || m.Contains("growth")
                || m.Contains("earnings")
                || m.Contains("results")
                || m.Contains("revenue")
                || m.Contains("margin")
            )
                return PromptIntent.Fundamental;

            if (m.Contains(" vs ") || m.Contains("compare") || m.Contains("better than"))
                return PromptIntent.Comparison;

            if (
                m.Contains("risk")
                || m.Contains("overvalued")
                || m.Contains("safe")
                || m.Contains("downside")
                || m.Contains("hedge")
            )
                return PromptIntent.Risk;

            if (
                m.Contains("complete")
                || m.Contains("detailed")
                || m.Contains("full analysis")
                || m.Contains("holding")
                || m.Contains("averaging")
                || m.Contains("loss")
                || m.Contains("portfolio")
            )
                return PromptIntent.Detailed;

            return PromptIntent.Price;
        }
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
