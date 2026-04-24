import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  ComposedChart,
  Line,
  Bar,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import {
  Loader2,
  ArrowLeft,
  Newspaper,
  Zap,
  X,
  Activity,
  TrendingUp,
  TrendingDown,
  Eye,
  EyeOff,
  ChevronRight,
  BarChart3,
  Shield,
  Cpu,
  DollarSign,
  Users,
  Sparkles,
} from "lucide-react";
import api from "../api/axios";
import FinancialTable from "./FinancialTable";
import ShareholdingSection from "./ShareHoldingSection";
import PeerComparisonTable from "./PeerComparisonTable";
import TradeModal from "./TradeModal";

/* ─── Pillar icon map ─────────────────────────────────────────── */
const PILLAR_META = {
  "Promoter Stake": { icon: Shield, color: "#6366f1", bg: "rgba(99,102,241,0.12)" },
  "Smart Flow": { icon: TrendingUp, color: "#10b981", bg: "rgba(16,185,129,0.12)" },
  "Technicals": { icon: Cpu, color: "#f59e0b", bg: "rgba(245,158,11,0.12)" },
  "Financials": { icon: BarChart3, color: "#3b82f6", bg: "rgba(59,130,246,0.12)" },
  "Ownership": { icon: Users, color: "#a78bfa", bg: "rgba(167,139,250,0.12)" },
  "Smart Money": { icon: DollarSign, color: "#34d399", bg: "rgba(52,211,153,0.12)" },
};

/* ─── Sentiment config ────────────────────────────────────────── */
const getSentimentConfig = (score) => {
  if (score >= 70) return { gradient: "from-emerald-500 to-teal-400", ring: "#10b981", label: "text-emerald-600", badge: "bg-emerald-50  text-emerald-700 border-emerald-200" };
  if (score >= 55) return { gradient: "from-emerald-400 to-cyan-400", ring: "#34d399", label: "text-emerald-600", badge: "bg-emerald-50  text-emerald-700 border-emerald-200" };
  if (score >= 45) return { gradient: "from-amber-400 to-yellow-300", ring: "#f59e0b", label: "text-amber-600", badge: "bg-amber-50   text-amber-700   border-amber-200" };
  if (score >= 35) return { gradient: "from-orange-500 to-amber-400", ring: "#f97316", label: "text-orange-600", badge: "bg-orange-50  text-orange-700  border-orange-200" };
  return { gradient: "from-rose-500 to-pink-400", ring: "#f43f5e", label: "text-rose-600", badge: "bg-rose-50    text-rose-700    border-rose-200" };
};

/* ─── Scorecard metric block ──────────────────────────────────── */
const ScoreBlock = ({ label, value, score, breakdowns, isPercent }) => {
  const [open, setOpen] = useState(false);
  const meta = PILLAR_META[label] || { icon: Sparkles, color: "#94a3b8", bg: "rgba(148,163,184,0.1)" };
  const Icon = meta.icon;

  // derive numeric score for mini arc
  const numScore = isPercent ? parseFloat(value) : null;
  const arc = numScore !== null ? numScore : (score || 0);
  const circumference = 2 * Math.PI * 14;
  const dash = (arc / 100) * circumference;

  return (
    <div
      className="relative group cursor-pointer select-none"
      onClick={() => breakdowns?.length && setOpen(!open)}
    >
      <div
        className={`rounded-2xl border transition-all duration-300 overflow-hidden
          ${open
            ? "border-slate-200 shadow-lg shadow-black/30"
            : "border-slate-100 hover:border-slate-200 hover:shadow-md hover:shadow-black/20"
          }`}
        style={{ background: open ? "#f1f5f9" : "#f8fafc" }}
      >
        <div className="p-4">
          {/* Icon + label row */}
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <div className="w-7 h-7 rounded-lg flex items-center justify-center" style={{ background: meta.bg }}>
                <Icon size={14} strokeWidth={2.2} style={{ color: meta.color }} />
              </div>
              <span className="text-[10px] font-bold tracking-widest uppercase text-slate-400">{label}</span>
            </div>
            {breakdowns?.length > 0 && (
              <ChevronRight
                size={13}
                className={`text-slate-500 transition-transform duration-300 ${open ? "rotate-90 text-slate-600" : ""}`}
              />
            )}
          </div>

          {/* Value + mini arc */}
          <div className="flex items-end justify-between">
            <span className="text-xl font-black text-slate-900 tracking-tight leading-none">{value}</span>
            {numScore !== null && (
              <svg width="36" height="36" viewBox="0 0 36 36" className="-mb-1">
                <circle cx="18" cy="18" r="14" fill="none" stroke="#e2e8f0" strokeWidth="3" />
                <circle
                  cx="18" cy="18" r="14"
                  fill="none"
                  stroke={meta.color}
                  strokeWidth="3"
                  strokeDasharray={`${dash} ${circumference}`}
                  strokeLinecap="round"
                  transform="rotate(-90 18 18)"
                  style={{ transition: "stroke-dasharray 1s ease" }}
                />
              </svg>
            )}
          </div>
        </div>

        {/* Expanded breakdown */}
        {open && breakdowns?.length > 0 && (
          <div
            className="border-t border-slate-100 px-4 py-3 space-y-2 animate-in slide-in-from-top-2 duration-200"
            style={{ background: "#f1f5f9" }}
          >
            {breakdowns.map((b, i) => (
              <div key={i} className="flex items-start gap-2">
                <div className="w-1 h-1 rounded-full mt-[7px] shrink-0" style={{ background: meta.color }} />
                <p className="text-[11px] text-slate-600 leading-relaxed font-medium">{b.explanation}</p>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

/* ─── Confidence arc (big) ────────────────────────────────────── */
const ConfidenceArc = ({ score, gradient, ring }) => {
  const r = 54, cx = 64, cy = 64;
  const circ = Math.PI * r; // semicircle
  const dash = (score / 100) * circ;

  return (
    <div className="flex flex-col items-center">
      <svg width="128" height="72" viewBox="0 0 128 80">
        <defs>
          <linearGradient id="arcGrad" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor={ring} stopOpacity="0.4" />
            <stop offset="100%" stopColor={ring} />
          </linearGradient>
        </defs>
        {/* track */}
        <path
          d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
          fill="none" stroke="#e2e8f0" strokeWidth="8" strokeLinecap="round"
        />
        {/* fill */}
        <path
          d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
          fill="none" stroke="url(#arcGrad)" strokeWidth="8" strokeLinecap="round"
          strokeDasharray={`${dash} ${circ}`}
          style={{ transition: "stroke-dasharray 1.2s cubic-bezier(.4,0,.2,1)" }}
        />
        {/* glow dot */}
        <circle
          cx={cx + r * Math.cos(Math.PI - (score / 100) * Math.PI)}
          cy={cy - r * Math.sin((score / 100) * Math.PI)}
          r="5" fill={ring}
          style={{ filter: `drop-shadow(0 0 6px ${ring})` }}
        />
      </svg>
      <div className="text-center -mt-2">
        <div className="text-4xl font-black text-slate-900 tracking-tighter">{score}<span className="text-xl text-slate-400 font-bold">%</span></div>
        <div className="text-[10px] font-bold tracking-[0.2em] text-slate-500 uppercase mt-0.5">Confidence Index</div>
      </div>
    </div>
  );
};

/* ═══════════════════════════════════════════════════════════════ */
export default function StockDetailView() {
  const { symbol } = useParams();
  const navigate = useNavigate();

  const [data, setData] = useState(null);
  const [analysis, setAnalysis] = useState(null);
  const [shareholding, setShareholding] = useState(null);
  const [loading, setLoading] = useState(true);
  const [newsLoading, setNewsLoading] = useState(true);
  const [shLoading, setShLoading] = useState(true);
  const [news, setNews] = useState([]);
  const [range, setRange] = useState("1d");
  const [isAnalysisModalOpen, setIsAnalysisModalOpen] = useState(false);
  const [peerData, setPeerData] = useState(null);
  const [peerLoading, setPeerLoading] = useState(true);
  const [isTradeModalOpen, setIsTradeModalOpen] = useState(false);
  const [trades, setTrades] = useState(null);
  const [showDMA50, setShowDMA50] = useState(false);
  const [showDMA200, setShowDMA200] = useState(false);
  const [showVolumeAlways, setShowVolumeAlways] = useState(true);

  const [visibleTables, setVisibleTables] = useState({
    quarters: true, pl: false, balance: false, cash: false,
  });

  const sentiment = getSentimentConfig(analysis?.score || 0);

  const safeFetch = async (url, setter, loadingSetter) => {
    try {
      const res = await api.get(url);
      if (res.data && res.data.success !== false) {
        setter(res.data.data || res.data);
      } else {
        if (url.includes("analyze")) setter({ sentiment: "Busy", score: 0, message: res.data.message });
      }
    } catch (err) {
      console.error(`Request Failed for ${url}:`, err);
    } finally {
      if (loadingSetter) loadingSetter(false);
    }
  };

  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchStockBasics = async () => {
      setNewsLoading(true); setShLoading(true); setPeerLoading(true); setTrades(null);
      await Promise.allSettled([
        safeFetch(`/stocks/analyze/${symbol}`, setAnalysis, null),
        safeFetch(`/portfolio/news/${symbol}`, (val) => setNews(val.slice(0, 7)), setNewsLoading),
        safeFetch(`/stocks/${symbol}/shareholding`, setShareholding, setShLoading),
        safeFetch(`/Stocks/peers/${symbol}`, (val) => setPeerData((prev) => ({ ...prev, peers: val.peers || val })), setPeerLoading),
      ]);
    };
    fetchStockBasics();
  }, [symbol]);

  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchChartOnly = async () => {
      setLoading(true);
      try {
        const res = await api.get(`/stocks/details/${symbol}?range=${range}`);
        if (res.data && res.data.success !== false) {
          const d = res.data.data || res.data;
          setData(d);
          setPeerData((prev) => ({ ...prev, industry: d.industry || prev?.industry }));
        }
      } catch (err) {
        console.error("Chart Fetch Failed", err);
      } finally {
        setLoading(false);
      }
    };
    fetchChartOnly();
  }, [symbol, range]);

  const handleOpenTrades = async () => {
    try {
      if (!trades) {
        const res = await api.get(`/stocks/${symbol}/trades`);
        if (res.data && res.data.success !== false) setTrades(res.data.data || res.data);
      }
      setIsTradeModalOpen(true);
    } catch (err) { console.error(err); }
  };

  const toggleTable = (id) => setVisibleTables((prev) => ({ ...prev, [id]: !prev[id] }));

  const formatNum = (val, decimals = 2) => {
    if (val === null || val === undefined || val === "N/A") return "N/A";
    return Number(val).toLocaleString("en-IN", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
  };

  const formatVolumeLabel = (val) => {
    if (val >= 10000000) return `${(val / 10000000).toFixed(1)}Cr`;
    if (val >= 1000000) return `${(val / 1000000).toFixed(1)}M`;
    if (val >= 1000) return `${(val / 1000).toFixed(0)}K`;
    return val;
  };

  const isUp = data?.ratios?.priceChange >= 0;
  const isPeriodPositive = data?.periodReturn >= 0;

  const chartColor = isUp ? "#10b981" : "#f43f5e";

  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    if (range === "1d") return date.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true });
    return date.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "2-digit" });
  };

  // Build scorecard rows — exclude Sentiment
  const scorecardEntries = Object.entries(analysis?.performanceMatrix || {}).filter(
    ([key]) => key !== "Sentiment"
  );

  if (loading && !data) return (
    <div className="flex h-screen items-center justify-center bg-slate-50">
      <Loader2 className="animate-spin text-indigo-600" size={40} />
    </div>
  );

  return (
    <div className="w-full max-w-7xl mx-auto p-3 sm:p-4 md:p-6 space-y-4 sm:space-y-6 bg-slate-50 min-h-screen font-sans relative pb-24 overflow-x-hidden">

      {/* ── ANALYSIS DRAWER ───────────────────────────────────────── */}
      {isAnalysisModalOpen && analysis && (
        <div className="fixed inset-0 z-[999] flex items-end sm:items-center justify-center">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/70 backdrop-blur-sm"
            onClick={() => setIsAnalysisModalOpen(false)}
          />

          {/* Panel */}
          <div className="relative w-full sm:max-w-lg max-h-[92vh] overflow-y-auto no-scrollbar rounded-t-3xl sm:rounded-3xl animate-in slide-in-from-bottom-4 sm:zoom-in-95 duration-300"
            style={{ background: "white", border: "1px solid #e2e8f0" }}>

            {/* Top accent bar */}
            <div className={`h-1 w-full rounded-t-3xl bg-gradient-to-r ${sentiment.gradient}`} />

            {/* Drag pill (mobile) */}
            <div className="flex justify-center pt-3 sm:hidden">
              <div className="w-10 h-1 rounded-full bg-slate-200" />
            </div>

            <div className="p-5 sm:p-7">
              {/* Header */}
              <div className="flex justify-between items-start mb-6">
                <div>
                  <p className="text-[10px] font-black tracking-[0.25em] text-slate-500 uppercase mb-1.5">
                    Intelligence Core · {data?.symbol || symbol}
                  </p>
                  <h2 className={`text-2xl font-black tracking-tight ${sentiment.label}`}>
                    {analysis.sentiment || "Status Unavailable"}
                  </h2>
                </div>
                <button
                  onClick={() => setIsAnalysisModalOpen(false)}
                  className="p-2 rounded-full bg-slate-50 hover:bg-slate-100 text-slate-500 transition-colors"
                >
                  <X size={18} />
                </button>
              </div>

              {analysis.sentiment === "Busy" ? (
                <div className="py-16 text-center">
                  <Loader2 className="animate-spin text-indigo-400 mx-auto mb-4" size={32} />
                  <p className="font-bold text-slate-400">{analysis.message}</p>
                </div>
              ) : (
                <>
                  {/* Confidence arc */}
                  <div className="flex justify-center mb-8">
                    <ConfidenceArc score={analysis.score} gradient={sentiment.gradient} ring={sentiment.ring || "#10b981"} />
                  </div>

                  {/* Score grid */}
                  <div className="grid grid-cols-2 gap-2.5 mb-6">
                    {scorecardEntries.map(([key, val]) => {
                      const breakdowns = analysis.breakdown?.filter(
                        (b) => b.pillar.toLowerCase().startsWith(key.toLowerCase().substring(0, 4))
                      );
                      const isPercent = typeof val === "string" && val.endsWith("%");
                      return (
                        <ScoreBlock
                          key={key}
                          label={key}
                          value={val}
                          breakdowns={breakdowns}
                          isPercent={isPercent}
                        />
                      );
                    })}
                  </div>

                  {/* Insight hint */}
                  <p className="text-center text-[10px] text-slate-600 font-medium mb-6">
                    Tap any card with → to expand insights
                  </p>
                </>
              )}

              <button
                onClick={() => setIsAnalysisModalOpen(false)}
                className={`w-full py-3.5 text-white rounded-2xl font-black text-xs uppercase tracking-widest transition-all hover:opacity-90 active:scale-95 bg-gradient-to-r ${sentiment.gradient}`}
                style={{ boxShadow: `0 8px 24px -4px ${sentiment.ring || "#10b981"}40` }}
              >
                Return to Dashboard
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── HEADER ────────────────────────────────────────────────── */}
      <div
        className="flex flex-col md:flex-row justify-between items-start md:items-center p-4 sm:p-5 rounded-2xl gap-4"
        style={{ background: "white", border: "1px solid #f1f5f9" }}
      >
        <div className="flex items-start sm:items-center gap-3 sm:gap-4">
          <button onClick={() => navigate("/")} className="p-2 hover:bg-slate-100 rounded-full transition-colors mt-1 sm:mt-0">
            <ArrowLeft size={20} className="text-slate-400" />
          </button>
          <div className="flex flex-col">
            <div className="flex flex-wrap items-center gap-2 mb-1">
              <h1 className="text-xl sm:text-2xl md:text-3xl font-black text-slate-900 tracking-tighter uppercase leading-none">
                {data?.symbol || symbol}
              </h1>
              <button
                onClick={() => setIsAnalysisModalOpen(true)}
                className={`flex items-center gap-1.5 px-3 py-1 rounded-full border text-[10px] font-black uppercase tracking-wider ${sentiment.badge}`}
              >
                <div className="w-1.5 h-1.5 rounded-full animate-pulse" style={{ background: "currentColor" }} />
                {analysis?.sentiment || "Analyzing..."}
              </button>
            </div>
            <p className="text-[10px] sm:text-xs font-semibold text-slate-500 uppercase truncate max-w-[220px] sm:max-w-none">
              {data?.companyName || "Loading..."}
            </p>
            <button
              onClick={() => navigate(`/strategy/${symbol}`)}
              className="mt-2.5 w-fit flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-black text-[9px] uppercase tracking-wider transition-all hover:brightness-110 active:scale-95"
              style={{ background: "linear-gradient(135deg,#6366f1,#8b5cf6)", boxShadow: "0 4px 12px rgba(99,102,241,0.35)", color: "#fff" }}
            >
              <Zap size={11} className="fill-white" /> Strategic Command
            </button>
          </div>
        </div>

        <div className="w-full sm:w-auto text-left sm:text-right border-t border-slate-100 sm:border-0 pt-3 sm:pt-0">
          <div className={`text-3xl sm:text-4xl font-black tracking-tighter ${isUp ? "text-emerald-600" : "text-rose-600"}`}>
            ₹{formatNum(data?.ratios?.currentPrice)}
          </div>
          <div className={`flex items-center sm:justify-end gap-1.5 text-xs font-bold mt-0.5 ${isUp ? "text-emerald-500" : "text-rose-500"}`}>
            {isUp ? <TrendingUp size={13} /> : <TrendingDown size={13} />}
            {formatNum(data?.ratios?.priceChange)} ({formatNum(data?.ratios?.priceChangePercent)}%)
          </div>
        </div>
      </div>

      {/* ── RATIO + NEWS ──────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-5">
        <div
          className="lg:col-span-2 rounded-2xl p-5 sm:p-6 grid grid-cols-2 gap-y-4 gap-x-8 content-start"
          style={{ background: "white", border: "1px solid #f1f5f9" }}
        >
          <RatioItem label="Market Cap" value={data?.ratios?.marketCap} />
          <RatioItem label="Price" value={`₹${formatNum(data?.ratios?.currentPrice)}`} />
          <RatioItem label="52W H/L" value={`${formatNum(data?.ratios?.high52W)} / ${formatNum(data?.ratios?.low52W)}`} />
          <RatioItem label="P/E" value={data?.ratios?.stockPE} />
          <RatioItem label="Div Yield" value={`${data?.ratios?.dividendYield}%`} />
          <RatioItem label="ROCE / ROE" value={`${data?.ratios?.roce} / ${data?.ratios?.roe}`} />
          <RatioItem label="Hist. High" value={`₹${formatNum(data?.ratios?.historicalHigh)}`} />
          <RatioItem label="Face Value" value={data?.ratios?.faceValue} />
        </div>

        <div
          className="rounded-2xl flex flex-col overflow-hidden h-[250px] sm:h-auto sm:max-h-[300px]"
          style={{ background: "white", border: "1px solid #f1f5f9" }}
        >
          <div className="p-3.5 border-b border-slate-100 flex items-center gap-2">
            <Newspaper className="text-indigo-500" size={14} />
            <h3 className="font-bold text-slate-700 text-[11px] uppercase tracking-widest">Latest News</h3>
          </div>
          <div className="overflow-y-auto divide-y divide-slate-100 flex-1">
            {newsLoading ? (
              <div className="p-6 flex justify-center"><Loader2 className="animate-spin text-slate-600" size={20} /></div>
            ) : news.length > 0 ? (
              news.map((item, idx) => (
                <a key={idx} href={item.url} target="_blank" rel="noopener noreferrer"
                  className="group block p-3 hover:bg-slate-50 transition-colors">
                  <div className="flex justify-between items-center mb-1">
                    <span className="text-[8px] font-black text-indigo-400 uppercase tracking-wide">{item.source}</span>
                    <span className="text-[8px] font-semibold text-slate-600">
                      {new Date(item.publishedAt).toLocaleDateString("en-IN", { day: "2-digit", month: "short" })}
                    </span>
                  </div>
                  <h4 className="text-[11px] font-semibold text-slate-700 line-clamp-2 group-hover:text-slate-900 transition-colors">{item.title}</h4>
                </a>
              ))
            ) : (
              <p className="text-[10px] p-5 text-slate-600 italic">No recent news found.</p>
            )}
          </div>
        </div>
      </div>

      {/* ── CHART ─────────────────────────────────────────────────── */}
      <div
        className="rounded-2xl p-4 sm:p-6"
        style={{ background: "white", border: "1px solid #f1f5f9" }}
      >
        {/* Period cards */}
        <div className="grid grid-cols-3 gap-3 mb-6">
          <PeriodCard label="Period High" value={`₹${formatNum(data?.periodHigh)}`} color="text-emerald-600" />
          <PeriodCard label="Period Low" value={`₹${formatNum(data?.periodLow)}`} color="text-rose-600" />
          <PeriodCard
            label="Return"
            value={`${isPeriodPositive ? "+" : ""}${formatNum(data?.periodReturn)}%`}
            color={isPeriodPositive ? "text-emerald-600" : "text-rose-400"}
          />
        </div>

        {/* Controls */}
        <div className="flex flex-col md:flex-row justify-between items-center mb-5 gap-3">
          <div className="flex p-1 rounded-xl gap-1 overflow-x-auto w-full md:w-auto no-scrollbar"
            style={{ background: "#f8fafc", border: "1px solid #e2e8f0" }}>
            {["1d", "1w", "1m", "3m", "6m", "1y", "3y", "max"].map((f) => (
              <button key={f} onClick={() => setRange(f)}
                className={`flex-shrink-0 px-3 sm:px-4 py-1.5 rounded-lg text-[10px] font-black uppercase transition-all ${range === f
                    ? "bg-indigo-600 text-white shadow-sm shadow-indigo-900"
                    : "text-slate-500 hover:text-slate-600"
                  }`}>
                {f}
              </button>
            ))}
          </div>
          <div className="flex gap-2 p-1 rounded-xl overflow-x-auto w-full md:w-auto no-scrollbar"
            style={{ background: "#f8fafc", border: "1px solid #e2e8f0" }}>
            <ChartToggle label="Volume" active={showVolumeAlways} onClick={() => setShowVolumeAlways(!showVolumeAlways)} color="#6366f1" />
            <ChartToggle label="50 DMA" active={showDMA50} onClick={() => setShowDMA50(!showDMA50)} color="#f59e0b" />
            <ChartToggle label="200 DMA" active={showDMA200} onClick={() => setShowDMA200(!showDMA200)} color="#64748b" />
          </div>
        </div>

        <div className="h-[250px] sm:h-[300px] md:h-[360px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data?.chartData} margin={{ left: 35, right: 35, bottom: 0, top: 10 }}>
              <defs>
                <linearGradient id="colorTrend" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={chartColor} stopOpacity={0.18} />
                  <stop offset="95%" stopColor={chartColor} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
              <XAxis dataKey="date" tickFormatter={renderDateTick} minTickGap={30}
                tick={{ fontSize: 9, fontWeight: 600, fill: "#475569" }} axisLine={false} tickLine={false} />
              <YAxis yAxisId="vol" orientation="left" domain={[0, (m) => m * 1.2]}
                tickFormatter={formatVolumeLabel}
                tick={{ fontSize: 9, fill: "#4f46e5" }} axisLine={false} tickLine={false}
                label={{ value: "VOLUME", angle: -90, position: "insideLeft", offset: -25, style: { fontSize: 9, fontWeight: 900, fill: "#1e293b" } }} />
              <YAxis yAxisId="price" orientation="right" domain={["auto", "auto"]}
                tick={{ fontSize: 10, fill: "#94a3b8", fontWeight: 700 }} axisLine={false} tickLine={false}
                label={{ value: "PRICE (₹)", angle: 90, position: "insideRight", offset: -5, style: { fontSize: 9, fontWeight: 900, fill: "#1e293b" } }} />
              <Tooltip content={<CustomTooltip toggles={{ showDMA50, showDMA200 }} />}
                cursor={{ stroke: "#94a3b8", strokeWidth: 1, strokeDasharray: "4 4" }} />
              <Area yAxisId="price" type="monotone" dataKey="price" fill="url(#colorTrend)" stroke="none" connectNulls />
              {showVolumeAlways && (
                <Bar yAxisId="vol" dataKey="volume" fill="#6366f1" opacity={0.5}
                  radius={[3, 3, 0, 0]} barSize={range === "1d" ? 5 : 12} />
              )}
              <Line yAxisId="price" type="monotone" dataKey="price" stroke={chartColor} strokeWidth={2} dot={false} connectNulls />
              {showDMA50 && <Line yAxisId="price" type="monotone" dataKey="dmA50" stroke="#f59e0b" strokeWidth={1.2} dot={false} connectNulls />}
              {showDMA200 && <Line yAxisId="price" type="monotone" dataKey="dmA200" stroke="#64748b" strokeWidth={1.2} dot={false} connectNulls />}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* ── PEER TABLE ────────────────────────────────────────────── */}
      {!peerLoading && peerData?.peers && (
        <div className="my-6 overflow-x-auto no-scrollbar">
          <PeerComparisonTable data={peerData} />
        </div>
      )}

      {/* ── FINANCIAL TABLES ──────────────────────────────────────── */}
      <div className="space-y-5 pt-2">
        <div className="flex p-1.5 rounded-2xl gap-1.5 overflow-x-auto no-scrollbar w-full md:w-fit"
          style={{ background: "#f8fafc", border: "1px solid #e2e8f0" }}>
          {[{ id: "quarters", label: "Quarters" }, { id: "pl", label: "P&L" }, { id: "balance", label: "Balance" }, { id: "cash", label: "Cash" }].map((tab) => (
            <button
              key={tab.id}
              onClick={() => toggleTable(tab.id)}
              className={`whitespace-nowrap flex items-center gap-1.5 px-5 py-2.5 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all ${visibleTables[tab.id]
                  ? "bg-indigo-600 text-white shadow-lg shadow-indigo-900/50"
                  : "text-slate-500 hover:text-slate-600"
                }`}
            >
              {visibleTables[tab.id] ? <Eye size={11} /> : <EyeOff size={11} />} {tab.label}
            </button>
          ))}
        </div>

        <div className="space-y-10">
          {visibleTables.quarters && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Quarterly Results" data={data?.quarterlyResults} /></div>}
          {visibleTables.pl && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Annual Profit & Loss" data={data?.profitAndLoss} /></div>}
          {visibleTables.balance && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Balance Sheet" data={data?.balanceSheet} /></div>}
          {visibleTables.cash && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Cash Flow Statement" data={data?.cashFlow} /></div>}
        </div>
      </div>

      {!shLoading && shareholding && (
        <ShareholdingSection data={shareholding} analysis={analysis} onOpenTrades={handleOpenTrades} />
      )}
      <TradeModal isOpen={isTradeModalOpen} onClose={() => setIsTradeModalOpen(false)} trades={trades} symbol={symbol} />
    </div>
  );
}

/* ─── Sub-components ──────────────────────────────────────────── */
const RatioItem = ({ label, value }) => (
  <div className="flex justify-between items-center border-b border-slate-100 pb-2.5">
    <span className="text-slate-500 text-[10px] sm:text-[11px] font-semibold uppercase tracking-wider">{label}</span>
    <span className="text-slate-800 font-bold text-[10px] sm:text-[11px] ml-3 tabular-nums">{value || "N/A"}</span>
  </div>
);

const PeriodCard = ({ label, value, color }) => (
  <div className="rounded-xl p-3 flex flex-col gap-1"
    style={{ background: "#f1f5f9", border: "1px solid #e2e8f0" }}>
    <p className="text-[8px] font-black text-slate-600 uppercase tracking-widest">{label}</p>
    <p className={`text-sm sm:text-base font-black tracking-tight truncate ${color}`}>{value}</p>
  </div>
);

const ChartToggle = ({ label, active, onClick, color }) => (
  <button
    onClick={onClick}
    className={`whitespace-nowrap px-3 py-1.5 rounded-lg text-[9px] font-bold transition-all ${active ? "bg-slate-100 shadow-sm" : "text-slate-600 hover:text-slate-400"
      }`}
    style={{ color: active ? color : undefined }}
  >
    {label}
  </button>
);

const CustomTooltip = ({ active, payload, toggles }) => {
  if (active && payload && payload.length) {
    const d = payload[0].payload;
    return (
      <div className="p-3 rounded-xl text-[10px] shadow-2xl min-w-[150px]"
        style={{ background: "rgba(15,23,42,0.95)", border: "1px solid rgba(255,255,255,0.1)", backdropFilter: "blur(12px)" }}>
        <p className="font-black text-indigo-300 border-b border-white/10 pb-1.5 mb-2 text-center uppercase text-[9px] tracking-wider">
          {new Date(d.date).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" })}
        </p>
        <div className="space-y-1.5">
          <Row label="Price" value={`₹${Number(d.price || 0).toFixed(1)}`} color="#f8fafc" />
          {toggles.showDMA50 && d.dmA50 && <Row label="50D" value={`₹${Number(d.dmA50).toFixed(1)}`} color="#f59e0b" />}
          {toggles.showDMA200 && d.dmA200 && <Row label="200D" value={`₹${Number(d.dmA200).toFixed(1)}`} color="#64748b" />}
          <div className="border-t border-white/10 pt-1.5">
            <Row label="Vol" value={d.volume?.toLocaleString("en-IN")} color="#94a3b8" />
          </div>
        </div>
      </div>
    );
  }
  return null;
};

const Row = ({ label, value, color }) => (
  <div className="flex justify-between gap-4">
    <span style={{ color: "#475569" }}>{label}</span>
    <span className="font-bold" style={{ color }}>{value}</span>
  </div>
);