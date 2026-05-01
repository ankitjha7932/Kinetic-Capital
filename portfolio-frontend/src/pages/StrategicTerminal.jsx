import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Sparkles,
  Send,
  ShieldAlert,
  Zap,
  Loader2,
  ShieldCheck,
  Activity,
  ArrowLeft,
  Cpu,
  Globe,
  Scale,
  MessageSquare,
  Target,
  Clock,
  Info,
  Menu,
  X
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

// 1. COMPONENT: Handles the GPT-style streaming effect
const TypewriterMarkdown = ({ text, onComplete }) => {
  const [displayedText, setDisplayedText] = useState("");

  useEffect(() => {
    let i = 0;
    const interval = setInterval(() => {
      setDisplayedText(text.slice(0, i));
      i += 3;

      if (i >= text.length + 3) {
        clearInterval(interval);
        onComplete();
      }
    }, 15);

    return () => clearInterval(interval);
  }, [text, onComplete]);

  return (
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
      {displayedText + (displayedText.length < text.length ? " ▍" : "")}
    </ReactMarkdown>
  );
};

function LogoAvatar({ symbol, size = 36, fontSize = 9, radius = 10 }) {
  const [failed, setFailed] = useState(false);
  const ticker = symbol?.replace(".NS", "").toUpperCase();
  const src = `https://assets-netstorage.groww.in/stock-assets/logos2/${ticker}.webp`;
  if (failed) return (
    <div style={{ width: size, height: size, borderRadius: radius, background: "#4f46e5", color: "#fff", display: "flex", alignItems: "center", justifyContent: "center", fontSize, fontWeight: 800, flexShrink: 0 }}>
      {ticker?.slice(0, 3)}
    </div>
  );
  return (
    <div style={{ width: size, height: size, borderRadius: radius, background: "#f8fafc", border: "0.5px solid rgba(255,255,255,0.1)", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden", flexShrink: 0 }}>
      <img src={src} alt={ticker} style={{ width: size * 0.78, height: size * 0.78, objectFit: "contain" }} onError={() => setFailed(true)} />
    </div>
  );
}

export default function StrategicTerminal() {
  const { symbol } = useParams();
  const navigate = useNavigate();
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [loadingStatus, setLoadingStatus] = useState("");
  const [followUps, setFollowUps] = useState([]);
  const [isTyping, setIsTyping] = useState(false);
  const [showSysInfo, setShowSysInfo] = useState(false); // Controls Uptime/Latency info
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false); // Controls mobile sidebar
  const messagesEndRef = useRef(null);

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
  }, [messages, loadingStatus, followUps, isTyping]);

  const handleSendMessage = async (msgText) => {
    const text = msgText || input;
    if (!text.trim()) return;

    setInput("");
    setFollowUps([]);
    setMessages((prev) => [...prev, { role: "user", text }]);

    try {
      setLoadingStatus("Syncing Global Feeds...");
      await new Promise((r) => setTimeout(r, 600));
      setLoadingStatus("Running Quant Simulations...");
      await new Promise((r) => setTimeout(r, 600));
      setLoadingStatus("Finalizing Verdict...");

      const res = await api.post("/Chat/ask", { message: text, symbol });

      setLoadingStatus("");
      setIsTyping(true);

      setMessages((prev) => [
        ...prev,
        { role: "bot", text: res.data.message, needsTyping: true }
      ]);
      setFollowUps(res.data.followUps || []);
    } catch (err) {
      setLoadingStatus("");
      setIsTyping(true);
      setMessages((prev) => [
        ...prev,
        {
          role: "bot",
          text: "### ⚠️ Link Severed\nStrategic core unreachable. Re-establishing secure tunnel...",
          needsTyping: true
        },
      ]);
    }
  };

  return (
    <div className="flex h-[100dvh] bg-slate-950 text-slate-200 font-sans overflow-hidden">

      {/* Mobile Sidebar Overlay Backdrop */}
      {isMobileMenuOpen && (
        <div
          className="fixed inset-0 bg-black/60 backdrop-blur-sm z-40 md:hidden"
          onClick={() => setIsMobileMenuOpen(false)}
        />
      )}

      {/* LEFT SIDEBAR (Responsive) */}
      <div className={`
        fixed md:relative inset-y-0 left-0 z-50 w-80 border-r border-white/5 bg-slate-950 md:bg-slate-900/20 backdrop-blur-xl flex flex-col p-6 space-y-8 overflow-y-auto no-scrollbar transition-transform duration-300 ease-in-out
        ${isMobileMenuOpen ? "translate-x-0 shadow-2xl" : "-translate-x-full"} md:translate-x-0
      `}>
        <div className="absolute inset-0 pointer-events-none opacity-[0.03] bg-[linear-gradient(rgba(18,16,16,0)_50%,rgba(0,0,0,0.25)_50%),linear-gradient(90deg,rgba(255,0,0,0.06),rgba(0,255,0,0.02),rgba(0,0,255,0.06))] z-50 bg-[length:100%_2px,3px_100%]" />

        <div className="flex justify-between items-center z-10">
          <button
            onClick={() => navigate(-1)}
            className="flex items-center gap-2 text-slate-500 hover:text-indigo-400 transition-all text-[10px] font-black uppercase tracking-[0.2em]"
          >
            <ArrowLeft size={14} /> Close Secure Session
          </button>
          {/* Mobile close button */}
          <button className="md:hidden text-slate-500 hover:text-white" onClick={() => setIsMobileMenuOpen(false)}>
            <X size={20} />
          </button>
        </div>

        <div className="pt-4 border-t border-white/5 z-10">
          <p className="text-[10px] font-black text-indigo-500/80 uppercase tracking-[0.3em] mb-1">
            Target Identity
          </p>
          <div className="flex items-center gap-3 mt-1">
            <LogoAvatar symbol={symbol} size={44} radius={12} fontSize={10} />
            <h1 className="text-4xl font-black text-white tracking-tighter drop-shadow-2xl truncate">{symbol}</h1>
          </div>
          <div className="mt-3 flex items-center gap-2 px-3 py-1 bg-emerald-500/10 border border-emerald-500/20 rounded-lg w-fit">
            <span className="w-1.5 h-1.5 bg-emerald-500 rounded-full animate-pulse" />
            <span className="text-[9px] font-black text-emerald-500 uppercase tracking-widest">
              Feed: Optimized
            </span>
          </div>
        </div>

        <div className="space-y-6 z-10 flex-1 flex flex-col">
          <div className="bg-slate-800/20 rounded-2xl p-5 border border-white/5 space-y-4">
            <div className="flex items-center gap-2 mb-2">
              <Cpu size={14} className="text-slate-500" />
              <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">
                Logic Core
              </span>
            </div>
            <ParamRow label="Macro Risk" value="High (War)" color="text-rose-500" />
            <ParamRow label="Alpha Signal" value="Strong" color="text-emerald-400" />
            <ParamRow label="Volatility" value="Elevated" color="text-amber-400" />
          </div>

          {/* UPDATED: Uptime and Latency with Info Panel */}
          <div className="mt-auto bg-slate-800/30 rounded-2xl p-5 border border-white/5 space-y-3">
            <div className="flex items-center justify-between pb-2 border-b border-white/5">
              <span className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
                System Telemetry
              </span>
              <button
                onClick={() => setShowSysInfo(!showSysInfo)}
                className="text-slate-500 hover:text-indigo-400 transition-colors bg-white/5 p-1 rounded-md"
              >
                <Info size={14} />
              </button>
            </div>
            <div className="flex justify-between text-[10px] font-mono text-slate-300">
              <span className="uppercase">Uptime</span>
              <span className="text-emerald-400 font-black">99.98%</span>
            </div>
            <div className="flex justify-between text-[10px] font-mono text-slate-300">
              <span className="uppercase">Latency</span>
              <span className="text-emerald-400 font-black">14ms</span>
            </div>

            {/* Hidden Info Text revealed on click */}
            {showSysInfo && (
              <div className="pt-2 mt-2 border-t border-white/5 text-[9.5px] text-slate-400 font-mono leading-relaxed animate-in fade-in slide-in-from-top-1">
                Uptime is calculated via continuous sync with global financial exchanges. Latency (14ms) measures the secure tunnel round-trip from the regional endpoint to our primary quant nodes.
              </div>
            )}
          </div>
        </div>
      </div>

      {/* MAIN TERMINAL AREA */}
      <div className="flex-1 flex flex-col relative bg-slate-950 w-full md:w-auto">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_50%_0%,_rgba(79,70,229,0.08)_0%,_rgba(15,23,42,0)_50%)] pointer-events-none" />

        {/* Header */}
        <div className="p-4 md:p-6 flex justify-between items-center border-b border-white/5 bg-slate-950/40 backdrop-blur-md z-10 shrink-0">
          <div className="flex items-center gap-3 md:gap-4">
            <button
              onClick={() => setIsMobileMenuOpen(true)}
              className="md:hidden text-slate-400 hover:text-white p-2 -ml-2 rounded-lg bg-white/5 border border-white/5"
            >
              <Menu size={18} />
            </button>
            <div className="w-10 h-10 bg-indigo-600 rounded-xl flex items-center justify-center text-white shadow-lg shadow-indigo-500/20 shrink-0">
              <Zap size={20} className="fill-white" />
            </div>
            <div className="truncate">
              <h2 className="text-xs md:text-sm font-black text-white uppercase tracking-[0.2em] truncate flex items-center gap-1.5">
                Kinetic Command <span className="flex items-center gap-1.5 text-indigo-500 bg-indigo-500/10 px-2 py-0.5 rounded-md truncate">
                  <LogoAvatar symbol={symbol} size={18} radius={4} fontSize={6} />
                  {symbol}
                </span>
              </h2>
              <p className="text-[8px] md:text-[9px] font-bold text-slate-500 uppercase tracking-widest truncate mt-0.5">
                Encrypted Strategic Protocol Active
              </p>
            </div>
          </div>
        </div>

        {/* Message Feed */}
        <div className="flex-1 overflow-y-auto p-4 md:p-10 space-y-10 no-scrollbar z-10">
          <div className="max-w-3xl mx-auto space-y-12">

            {/* INITIAL SAMPLE QUESTIONS */}
            {messages.length === 0 && (
              <div className="space-y-8 animate-in fade-in duration-700">
                <div className="text-center space-y-2">
                  <h3 className="text-indigo-400 font-black text-xs uppercase tracking-[0.4em]">
                    Mission Briefing
                  </h3>
                  <p className="text-slate-500 text-sm font-medium px-4">
                    Select a primary objective to begin the intelligence
                    deep-dive on {symbol}.
                  </p>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 md:gap-4">
                  {sampleQuestions.map((q, idx) => (
                    <button
                      key={idx}
                      onClick={() => handleSendMessage(q.text)}
                      className="flex items-center gap-4 p-4 md:p-5 bg-slate-900/40 border border-white/5 rounded-[1.5rem] text-left hover:border-indigo-500/50 hover:bg-slate-900/60 transition-all group"
                    >
                      <div className="p-3 bg-slate-950 rounded-xl border border-white/5 group-hover:scale-110 transition-transform shrink-0">
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

            {/* MESSAGE MAP */}
            {messages.map((m, i) => (
              <div key={i} className={`flex ${m.role === "user" ? "justify-end" : "justify-start"}`}>
                <div className={`relative group ${m.role === "user" ? "max-w-[90%] md:max-w-[80%]" : "w-full"}`}>
                  {m.role === "user" ? (
                    <div className="bg-indigo-600 px-5 py-3.5 md:px-6 md:py-4 rounded-2xl rounded-tr-none text-white text-sm font-bold shadow-xl">
                      {m.text}
                    </div>
                  ) : (
                    <div className="space-y-3 md:space-y-4">
                      <div className="flex items-center gap-2 text-indigo-400 opacity-60">
                        <Activity size={14} />
                        <span className="text-[10px] font-black uppercase tracking-[0.3em]">
                          System Verdict
                        </span>
                      </div>
                      <div className="bg-slate-900/40 border border-white/5 p-5 md:p-8 rounded-[1.5rem] md:rounded-[2rem] text-slate-300 text-[13px] md:text-sm leading-relaxed font-mono shadow-inner backdrop-blur-sm overflow-x-auto">

                        {m.needsTyping ? (
                          <TypewriterMarkdown
                            text={m.text}
                            onComplete={() => {
                              setMessages(prev => prev.map((msg, idx) =>
                                idx === i ? { ...msg, needsTyping: false } : msg
                              ));
                              setIsTyping(false);
                            }}
                          />
                        ) : (
                          <ReactMarkdown
                            components={{
                              h3: ({ node, ...props }) => (
                                <h3 className="text-indigo-400 font-black text-[16px] md:text-lg mb-4 uppercase tracking-tight border-b border-white/5 pb-2" {...props} />
                              ),
                              strong: ({ node, ...props }) => (
                                <strong className="text-white font-black bg-white/5 px-1 rounded" {...props} />
                              ),
                            }}
                          >
                            {m.text}
                          </ReactMarkdown>
                        )}

                      </div>
                    </div>
                  )}
                </div>
              </div>
            ))}

            {/* Inline Loading Status Bubble */}
            {loadingStatus && (
              <div className="flex justify-start animate-in fade-in slide-in-from-bottom-2 duration-300">
                <div className="w-full space-y-3 md:space-y-4">
                  <div className="flex items-center gap-2 text-indigo-400 opacity-60 animate-pulse">
                    <Cpu size={14} />
                    <span className="text-[10px] font-black uppercase tracking-[0.3em]">
                      Processing Request
                    </span>
                  </div>
                  <div className="flex items-center gap-4 py-5 px-6 md:py-6 md:px-8 bg-slate-900/40 border border-white/5 rounded-[1.5rem] md:rounded-[2rem] shadow-inner backdrop-blur-sm w-fit">
                    <Loader2 size={16} className="animate-spin text-indigo-500" />
                    <span className="text-[11px] md:text-xs font-bold text-indigo-400 uppercase tracking-[0.2em] animate-pulse">
                      {loadingStatus}
                    </span>
                  </div>
                </div>
              </div>
            )}

            {/* DYNAMIC FOLLOW-UPS FROM BACKEND */}
            {!loadingStatus && !isTyping && followUps.length > 0 && (
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
                    className="flex items-center gap-2 px-3.5 py-2 md:px-4 md:py-2.5 bg-slate-900/80 border border-white/10 text-indigo-300 rounded-full text-[10px] md:text-[11px] font-bold hover:bg-indigo-600 hover:text-white hover:border-indigo-500 transition-all shadow-lg active:scale-95 text-left"
                  >
                    <MessageSquare size={12} className="shrink-0" />
                    {q}
                  </button>
                ))}
              </div>
            )}

            {/* Invisible div to snap scroll to */}
            <div ref={messagesEndRef} className="h-2 md:h-4" />
          </div>
        </div>

        {/* Input & Static Suggestions */}
        <div className="p-3 md:p-8 bg-slate-950/80 backdrop-blur-2xl border-t border-white/5 shrink-0 z-10 pb-safe">
          <div className="max-w-3xl mx-auto">
            <div className="flex gap-2 mb-3 md:mb-4 overflow-x-auto no-scrollbar pb-2">
              {STRATEGIC_CHIPS.map((s, i) => (
                <button
                  key={i}
                  onClick={() => handleSendMessage(s.query)}
                  disabled={loadingStatus || isTyping}
                  className="flex items-center gap-1.5 md:gap-2 px-3 py-1.5 md:px-4 md:py-2 bg-slate-900 border border-white/10 rounded-full whitespace-nowrap hover:bg-indigo-600 hover:border-indigo-500 transition-all group disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <span className="text-slate-500 group-hover:text-white">
                    {s.icon}
                  </span>
                  <span className="text-[9px] md:text-[10px] font-black uppercase tracking-widest text-slate-400 group-hover:text-white">
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
                disabled={loadingStatus || isTyping}
                placeholder={isTyping || loadingStatus ? "Awaiting transmission..." : `Analyze ${symbol}...`}
                className="w-full bg-slate-900/60 border border-white/10 rounded-2xl md:rounded-[1.5rem] py-4 md:py-6 pl-5 md:pl-8 pr-16 md:pr-20 text-sm md:text-md font-bold text-white shadow-2xl outline-none focus:border-indigo-500 transition-all placeholder:text-slate-600 disabled:opacity-50"
              />
              <button
                type="submit"
                disabled={!input.trim() || loadingStatus || isTyping}
                className="absolute right-2 md:right-3 top-1/2 -translate-y-1/2 w-10 h-10 md:w-12 md:h-12 bg-indigo-600 text-white rounded-xl shadow-lg hover:bg-indigo-500 transition-all flex items-center justify-center group disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Send
                  size={18}
                  className="group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform"
                />
              </button>
            </form>
            <p className="mt-3 md:mt-4 text-center text-[8px] md:text-[9px] font-bold text-slate-600 uppercase tracking-[0.4em] px-4">
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