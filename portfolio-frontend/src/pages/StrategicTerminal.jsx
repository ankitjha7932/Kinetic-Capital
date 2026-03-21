import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Sparkles,
  Send,
  ShieldAlert,
  Zap,
  Binary,
  Loader2,
  ArrowUpRight,
  ShieldCheck,
  Activity,
  ArrowLeft,
  Cpu,
  Globe,
  Scale,
  MessageSquare,
  Target,
  Clock,
} from "lucide-react";
import ReactMarkdown from "react-markdown";
import api from "../api/axios";

// Static suggestion chips for the input bar
const STRATEGIC_CHIPS = [
  {
    label: "Valuation",
    icon: <Scale size={12} />,
    query: "Is the current P/E justifiable vs peers?",
  },
  {
    label: "Macro",
    icon: <Globe size={12} />,
    query: "Impact of geopolitical tensions on this sector?",
  },
  {
    label: "Dividends",
    icon: <ShieldCheck size={12} />,
    query: "Is the dividend payout sustainable?",
  },
];

export default function StrategicTerminal() {
  const { symbol } = useParams();
  const navigate = useNavigate();
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [loadingStatus, setLoadingStatus] = useState("");
  const [followUps, setFollowUps] = useState([]);
  const messagesEndRef = useRef(null);

  // 1. YOUR SPECIFIC SAMPLE QUESTIONS
  const sampleQuestions = [
    {
      text: "Give me a complete analysis with strategy and risk",
      icon: <Target size={14} className="text-indigo-400" />,
    },
    {
      text: `Is ${symbol} a good entry point or should I wait?`,
      icon: <Clock size={14} className="text-amber-400" />,
    },
    {
      text: "What are the biggest risks right now?",
      icon: <ShieldAlert size={14} className="text-rose-400" />,
    },
    {
      text: `Should I buy ${symbol} stock now?`,
      icon: <Zap size={14} className="text-emerald-400" />,
    },
  ];

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, loadingStatus, followUps]);

  const handleSendMessage = async (msgText) => {
    const text = msgText || input;
    if (!text.trim()) return;

    setInput("");
    setFollowUps([]); // Clear suggestions when asking a new question
    setMessages((prev) => [...prev, { role: "user", text }]);

    try {
      setLoadingStatus("Syncing Global Feeds...");
      await new Promise((r) => setTimeout(r, 600));
      setLoadingStatus("Running Quant Simulations...");
      await new Promise((r) => setTimeout(r, 600));
      setLoadingStatus("Finalizing Verdict...");

      const res = await api.post("/Chat/ask", { message: text, symbol });

      // Capture message and follow-ups from backend
      setMessages((prev) => [...prev, { role: "bot", text: res.data.message }]);
      setFollowUps(res.data.followUps || []);
    } catch (err) {
      setMessages((prev) => [
        ...prev,
        {
          role: "bot",
          text: "### ⚠️ Link Severed\nStrategic core unreachable. Re-establishing secure tunnel...",
        },
      ]);
    } finally {
      setLoadingStatus("");
    }
  };

  return (
    <div className="flex h-screen bg-slate-950 text-slate-200 font-sans overflow-hidden">
      {/* LEFT SIDEBAR */}
      <div className="w-80 border-r border-white/5 bg-slate-900/20 backdrop-blur-xl flex flex-col p-6 space-y-8 relative overflow-hidden">
        <div className="absolute inset-0 pointer-events-none opacity-[0.03] bg-[linear-gradient(rgba(18,16,16,0)_50%,rgba(0,0,0,0.25)_50%),linear-gradient(90deg,rgba(255,0,0,0.06),rgba(0,255,0,0.02),rgba(0,0,255,0.06))] z-50 bg-[length:100%_2px,3px_100%]" />

        <button
          onClick={() => navigate(-1)}
          className="flex items-center gap-2 text-slate-500 hover:text-indigo-400 transition-all text-[10px] font-black uppercase tracking-[0.2em]"
        >
          <ArrowLeft size={14} /> Close Secure Session
        </button>

        <div className="pt-4 border-t border-white/5">
          <p className="text-[10px] font-black text-indigo-500/80 uppercase tracking-[0.3em] mb-1">
            Target Identity
          </p>
          <h1 className="text-4xl font-black text-white tracking-tighter drop-shadow-2xl">
            {symbol}
          </h1>
          <div className="mt-3 flex items-center gap-2 px-3 py-1 bg-emerald-500/10 border border-emerald-500/20 rounded-lg w-fit">
            <span className="w-1.5 h-1.5 bg-emerald-500 rounded-full animate-pulse" />
            <span className="text-[9px] font-black text-emerald-500 uppercase tracking-widest">
              Feed: Optimized
            </span>
          </div>
        </div>

        <div className="space-y-6">
          <div className="bg-slate-800/20 rounded-2xl p-5 border border-white/5 space-y-4">
            <div className="flex items-center gap-2 mb-2">
              <Cpu size={14} className="text-slate-500" />
              <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">
                Logic Core
              </span>
            </div>
            <ParamRow
              label="Macro Risk"
              value="High (War)"
              color="text-rose-500"
            />
            <ParamRow
              label="Alpha Signal"
              value="Strong"
              color="text-emerald-400"
            />
            <ParamRow
              label="Volatility"
              value="Elevated"
              color="text-amber-400"
            />
          </div>
          <div className="px-5 space-y-2 opacity-40">
            <div className="flex justify-between text-[9px] font-mono">
              <span className="uppercase">Uptime</span>
              <span>99.98%</span>
            </div>
            <div className="flex justify-between text-[9px] font-mono">
              <span className="uppercase">Latency</span>
              <span>14ms</span>
            </div>
          </div>
        </div>
      </div>

      {/* MAIN TERMINAL AREA */}
      <div className="flex-1 flex flex-col relative bg-slate-950">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_0%,_rgba(79,70,229,0.08)_0%,_rgba(15,23,42,0)_50%)] pointer-events-none" />

        {/* Header */}
        <div className="p-6 flex justify-between items-center border-b border-white/5 bg-slate-950/40 backdrop-blur-md z-10">
          <div className="flex items-center gap-4">
            <div className="w-10 h-10 bg-indigo-600 rounded-xl flex items-center justify-center text-white shadow-lg shadow-indigo-500/20">
              <Zap size={20} className="fill-white" />
            </div>
            <div>
              <h2 className="text-sm font-black text-white uppercase tracking-[0.2em]">
                Kinetic Command{" "}
                <span className="text-indigo-500 ml-1">v4.0</span>
              </h2>
              <p className="text-[9px] font-bold text-slate-500 uppercase tracking-widest">
                Encrypted Strategic Protocol Active
              </p>
            </div>
          </div>
        </div>

        {/* Message Feed */}
        <div className="flex-1 overflow-y-auto p-10 space-y-10 no-scrollbar z-10">
          <div className="max-w-3xl mx-auto space-y-12">
            {/* INITIAL SAMPLE QUESTIONS (Agent Start) */}
            {messages.length === 0 && (
              <div className="space-y-8 animate-in fade-in duration-700">
                <div className="text-center space-y-2">
                  <h3 className="text-indigo-400 font-black text-xs uppercase tracking-[0.4em]">
                    Mission Briefing
                  </h3>
                  <p className="text-slate-500 text-sm font-medium">
                    Select a primary objective to begin the intelligence
                    deep-dive on {symbol}.
                  </p>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {sampleQuestions.map((q, idx) => (
                    <button
                      key={idx}
                      onClick={() => handleSendMessage(q.text)}
                      className="flex items-center gap-4 p-5 bg-slate-900/40 border border-white/5 rounded-[1.5rem] text-left hover:border-indigo-500/50 hover:bg-slate-900/60 transition-all group"
                    >
                      <div className="p-3 bg-slate-950 rounded-xl border border-white/5 group-hover:scale-110 transition-transform">
                        {q.icon}
                      </div>
                      <span className="text-xs font-bold text-slate-300 group-hover:text-white leading-relaxed">
                        {q.text}
                      </span>
                    </button>
                  ))}
                </div>
              </div>
            )}

            {messages.map((m, i) => (
              <div
                key={i}
                className={`flex ${m.role === "user" ? "justify-end" : "justify-start"}`}
              >
                <div
                  className={`relative group ${m.role === "user" ? "max-w-[80%]" : "w-full"}`}
                >
                  {m.role === "user" ? (
                    <div className="bg-indigo-600 px-6 py-4 rounded-2xl rounded-tr-none text-white text-sm font-bold shadow-xl">
                      {m.text}
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <div className="flex items-center gap-2 text-indigo-400 opacity-60">
                        <Activity size={14} />
                        <span className="text-[10px] font-black uppercase tracking-[0.3em]">
                          System Verdict
                        </span>
                      </div>
                      <div className="bg-slate-900/40 border border-white/5 p-8 rounded-[2rem] text-slate-300 text-sm leading-relaxed font-mono shadow-inner backdrop-blur-sm">
                        <ReactMarkdown
                          components={{
                            h3: ({ node, ...props }) => (
                              <h3
                                className="text-indigo-400 font-black text-lg mb-4 uppercase tracking-tight border-b border-white/5 pb-2"
                                {...props}
                              />
                            ),
                            strong: ({ node, ...props }) => (
                              <strong
                                className="text-white font-black bg-white/5 px-1 rounded"
                                {...props}
                              />
                            ),
                          }}
                        >
                          {m.text}
                        </ReactMarkdown>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            ))}

            {/* DYNAMIC FOLLOW-UPS FROM BACKEND */}
            {!loadingStatus && followUps.length > 0 && (
              <div className="flex flex-wrap gap-2 animate-in fade-in slide-in-from-bottom-4 duration-500">
                <div className="w-full mb-1 flex items-center gap-2 px-1">
                  <Sparkles size={12} className="text-indigo-400" />
                  <span className="text-[9px] font-black text-slate-500 uppercase tracking-widest">
                    Recommended Deep-Dive
                  </span>
                </div>
                {followUps.map((q, idx) => (
                  <button
                    key={idx}
                    onClick={() => handleSendMessage(q)}
                    className="flex items-center gap-2 px-4 py-2.5 bg-slate-900/80 border border-white/10 text-indigo-300 rounded-full text-[11px] font-bold hover:bg-indigo-600 hover:text-white hover:border-indigo-500 transition-all shadow-lg active:scale-95"
                  >
                    <MessageSquare size={12} />
                    {q}
                  </button>
                ))}
              </div>
            )}

            {loadingStatus && (
              <div className="flex items-center gap-4 py-4 px-6 bg-indigo-500/5 rounded-2xl border border-indigo-500/20 w-fit animate-pulse">
                <Loader2 size={16} className="animate-spin text-indigo-500" />
                <span className="text-[10px] font-black text-indigo-500 uppercase tracking-[0.2em]">
                  {loadingStatus}
                </span>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>
        </div>

        {/* Input & Static Suggestions */}
        <div className="p-8 bg-slate-950/80 backdrop-blur-2xl border-t border-white/5">
          <div className="max-w-3xl mx-auto">
            <div className="flex gap-2 mb-6 overflow-x-auto no-scrollbar pb-2">
              {STRATEGIC_CHIPS.map((s, i) => (
                <button
                  key={i}
                  onClick={() => handleSendMessage(s.query)}
                  className="flex items-center gap-2 px-4 py-2 bg-slate-900 border border-white/10 rounded-full whitespace-nowrap hover:bg-indigo-600 hover:border-indigo-500 transition-all group"
                >
                  <span className="text-slate-500 group-hover:text-white">
                    {s.icon}
                  </span>
                  <span className="text-[10px] font-black uppercase tracking-widest text-slate-400 group-hover:text-white">
                    {s.label}
                  </span>
                </button>
              ))}
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleSendMessage();
              }}
              className="relative"
            >
              <input
                value={input}
                onChange={(e) => setInput(e.target.value)}
                placeholder={`Analyze ${symbol} strategic position...`}
                className="w-full bg-slate-900/60 border border-white/10 rounded-2xl py-6 pl-8 pr-20 text-md font-bold text-white shadow-2xl outline-none focus:border-indigo-500 transition-all placeholder:text-slate-700"
              />
              <button className="absolute right-3 top-1/2 -translate-y-1/2 w-12 h-12 bg-indigo-600 text-white rounded-xl shadow-lg hover:bg-indigo-500 transition-all flex items-center justify-center group">
                <Send
                  size={20}
                  className="group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform"
                />
              </button>
            </form>
            <p className="mt-4 text-center text-[9px] font-bold text-slate-600 uppercase tracking-[0.4em]">
              Proprietary Intelligence Protocol // Access Restricted
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

const ParamRow = ({ label, value, color }) => (
  <div className="flex justify-between items-center border-b border-white/5 pb-2">
    <span className="text-[10px] font-bold text-slate-600 uppercase tracking-widest">
      {label}
    </span>
    <span
      className={`text-[10px] font-black uppercase tracking-widest ${color}`}
    >
      {value}
    </span>
  </div>
);
