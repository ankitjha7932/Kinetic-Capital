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
  CheckCircle2,
  AlertCircle,
  Eye,
  EyeOff,
  Activity,
  Info,
} from "lucide-react";
import api from "../api/axios";
import FinancialTable from "./FinancialTable";
import ShareholdingSection from "./ShareHoldingSection";
import PeerComparisonTable from "./PeerComparisonTable";
import TradeModal from "./TradeModal";

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

  const [activeInfoKey, setActiveInfoKey] = useState(null);

  const [visibleTables, setVisibleTables] = useState({
    quarters: true,
    pl: false,
    balance: false,
    cash: false,
  });

  const [showDMA50, setShowDMA50] = useState(false);
  const [showDMA200, setShowDMA200] = useState(false);
  const [showVolumeAlways, setShowVolumeAlways] = useState(true);

  const getSentimentConfig = (score) => {
    if (score >= 70)
      return { bg: "bg-emerald-900", text: "text-emerald-900", light: "bg-emerald-50", border: "border-emerald-300" };
    if (score >= 55)
      return { bg: "bg-emerald-700", text: "text-emerald-700", light: "bg-emerald-50/70", border: "border-emerald-200" };
    if (score >= 45)
      return { bg: "bg-emerald-400", text: "text-emerald-500", light: "bg-emerald-50/40", border: "border-emerald-100" };
    if (score >= 35)
      return { bg: "bg-amber-400", text: "text-amber-600", light: "bg-amber-50", border: "border-amber-200" };
    if (score >= 20)
      return { bg: "bg-rose-400", text: "text-rose-500", light: "bg-rose-50/50", border: "border-rose-100" };
    return { bg: "bg-rose-900", text: "text-rose-900", light: "bg-rose-50", border: "border-rose-300" };
  };

  const sentiment = getSentimentConfig(analysis?.score || 0);

  // 🔸 REUSABLE FETCH HELPER
  const safeFetch = async (url, setter, loadingSetter) => {
    try {
      const res = await api.get(url);
      if (res.data && res.data.success !== false) {
        setter(res.data.data || res.data);
      } else {
        if (url.includes('analyze')) setter({ sentiment: "Busy", score: 0, message: res.data.message });
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
      setNewsLoading(true);
      setShLoading(true);
      setPeerLoading(true);
      setTrades(null);

      await Promise.allSettled([
        safeFetch(`/stocks/analyze/${symbol}`, setAnalysis, null),
        safeFetch(`/portfolio/news/${symbol}`, (val) => setNews(val.slice(0, 7)), setNewsLoading),
        safeFetch(`/stocks/${symbol}/shareholding`, setShareholding, setShLoading),
        safeFetch(`/Stocks/peers/${symbol}`, (val) => setPeerData(prev => ({ ...prev, peers: val.peers || val })), setPeerLoading),
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
          const detailData = res.data.data || res.data;
          setData(detailData);
          setPeerData(prev => ({ ...prev, industry: detailData.industry || prev?.industry }));
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
        if (res.data && res.data.success !== false) {
          setTrades(res.data.data || res.data);
        }
      }
      setIsTradeModalOpen(true);
    } catch (err) {
      console.error(err);
    }
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

  const themeColor = sentiment.text
    .replace("emerald-900", "064e3b").replace("emerald-700", "047857").replace("emerald-500", "10b981")
    .replace("amber-600", "d97706").replace("rose-500", "f43f5e").replace("rose-900", "881337")
    .replace("text-", "#");

  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    if (range === "1d") return date.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true });
    return date.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "2-digit" });
  };

  if (loading && !data) return (
    <div className="flex h-screen items-center justify-center bg-slate-50">
      <Loader2 className="animate-spin text-indigo-600" size={40} />
    </div>
  );

  return (
    <div className="w-full max-w-7xl mx-auto p-3 sm:p-4 md:p-6 space-y-4 sm:space-y-6 bg-slate-50 min-h-screen font-sans relative pb-20 overflow-x-hidden">

      {/* OPTIMIZED ANALYSIS MODAL */}
      {isAnalysisModalOpen && analysis && (
        <div className="fixed inset-0 z-[999] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-slate-900/80 backdrop-blur-md" onClick={() => setIsAnalysisModalOpen(false)} />
          <div className="relative bg-white w-full max-w-lg max-h-[90vh] overflow-y-auto no-scrollbar rounded-2xl sm:rounded-3xl shadow-2xl border border-slate-100 animate-in fade-in zoom-in duration-300">
            <div className={`h-2 w-full shrink-0 ${sentiment.bg}`} />
            <div className="p-4 sm:p-6 md:p-8">
              <div className="flex justify-between items-start mb-4 sm:mb-6">
                <div>
                  <p className="text-[9px] sm:text-[11px] font-black text-slate-400 uppercase tracking-[0.2em] mb-1 sm:mb-1.5">Intelligence Core</p>
                  <h2 className={`text-lg sm:text-xl md:text-2xl font-extrabold tracking-tight leading-tight ${sentiment.text}`}>
                    {analysis.sentiment || "Status Unavailable"}
                  </h2>
                </div>
                <button onClick={() => setIsAnalysisModalOpen(false)} className="p-2 bg-slate-50 hover:bg-slate-100 rounded-full text-slate-400 transition-colors shrink-0">
                  <X size={18} className="sm:w-5 sm:h-5" />
                </button>
              </div>

              {analysis.sentiment === "Busy" ? (
                <div className="p-8 sm:p-10 text-center">
                  <Loader2 className="animate-spin text-indigo-600 mx-auto mb-4" size={32} />
                  <p className="font-bold text-slate-600">{analysis.message}</p>
                </div>
              ) : (
                <>
                  <div className="mb-4 sm:mb-6 bg-slate-50/50 p-4 sm:p-6 rounded-xl sm:rounded-2xl border border-slate-100 shadow-inner">
                    <div className="flex justify-between items-end mb-2 sm:mb-3">
                      <span className="text-[9px] sm:text-sm font-black text-slate-500 uppercase tracking-widest">Confidence Index</span>
                      <span className={`text-2xl sm:text-3xl md:text-4xl font-extrabold ${sentiment.text}`}>{analysis.score}%</span>
                    </div>
                    <div className="h-2 sm:h-3 w-full bg-white rounded-full overflow-hidden border border-slate-200/50 shadow-sm">
                      <div className={`h-full transition-all duration-1000 ${sentiment.bg}`} style={{ width: `${analysis.score}%` }} />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2 sm:gap-3 mb-6 sm:mb-8">
                    {Object.entries(analysis.performanceMatrix || {}).map(([key, val]) => {
                      if (key === "Handover" || key === "Absorption") return null;
                      const reasons = analysis.breakdown?.filter((b) => b.pillar.toLowerCase().startsWith(key.toLowerCase().substring(0, 4)));
                      const isActive = activeInfoKey === key;
                      return (
                        <div key={key} className="group relative bg-white p-3 sm:p-4 md:p-5 rounded-xl border border-slate-100 shadow-sm hover:shadow-md transition-all hover:border-indigo-200">
                          <div className="flex justify-between items-start mb-1 sm:mb-1.5">
                            <p className="text-[9px] sm:text-[10px] md:text-[11px] font-black text-slate-400 uppercase tracking-wider truncate mr-1">{key}</p>
                            <button onClick={(e) => { e.stopPropagation(); setActiveInfoKey(isActive ? null : key); }}
                              className={`p-1 sm:p-1.5 rounded-full transition-all shrink-0 ${isActive ? "bg-indigo-600 text-white" : "bg-indigo-50 text-indigo-600 hover:bg-indigo-100"}`}>
                              <Info size={14} className="w-3 h-3 sm:w-4 sm:h-4" strokeWidth={2.5} />
                            </button>
                          </div>
                          <p className="text-xs sm:text-base md:text-lg font-black text-slate-800 tracking-tight leading-tight">{val}</p>
                          {isActive && (
                            <div className="absolute inset-0 bg-white/98 backdrop-blur-sm p-3 sm:p-5 rounded-xl z-10 flex flex-col justify-center animate-in fade-in zoom-in duration-200 shadow-2xl border-2 border-indigo-500/10">
                              <div className="flex justify-between items-center mb-1.5 sm:mb-2">
                                <span className="text-[9px] sm:text-[10px] font-black text-indigo-600 uppercase tracking-widest truncate mr-1">Insight</span>
                                <button onClick={() => setActiveInfoKey(null)} className="p-1 hover:bg-slate-100 rounded-full shrink-0"><X size={12} className="sm:w-[14px] sm:h-[14px] text-slate-400" /></button>
                              </div>
                              <div className="overflow-y-auto max-h-[100px] no-scrollbar">
                                {reasons?.length > 0 ? reasons.map((r, i) => (
                                  <p key={i} className="text-[9px] sm:text-xs leading-relaxed font-bold text-slate-700 mb-1 sm:mb-1.5 last:mb-0 bg-slate-50 p-1 sm:p-1.5 rounded-md">• {r.explanation}</p>
                                )) : <p className="text-[9px] sm:text-xs font-bold text-slate-400 italic">No specific data points available.</p>}
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </>
              )}
              <button onClick={() => setIsAnalysisModalOpen(false)}
                className={`w-full py-3 sm:py-4 text-white rounded-xl font-black text-xs sm:text-sm uppercase shadow-xl ${sentiment.bg} hover:brightness-110 transition-all hover:scale-[1.02] active:scale-95 mt-2`}>
                Return to Dashboard
              </button>
            </div>
          </div>
        </div>
      )}

      {/* HEADER */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center bg-white p-4 sm:p-5 rounded-2xl shadow-sm border border-slate-100 gap-4">
        <div className="flex items-start sm:items-center gap-3 sm:gap-4">
          <button onClick={() => navigate("/")} className="p-2 hover:bg-slate-100 rounded-full transition-colors mt-1 sm:mt-0">
            <ArrowLeft size={20} />
          </button>
          <div className="flex flex-col">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl sm:text-2xl md:text-3xl font-black text-slate-900 tracking-tighter uppercase leading-none">
                {data?.symbol || symbol}
              </h1>
              <button onClick={() => setIsAnalysisModalOpen(true)} className={`flex items-center gap-1 px-2.5 py-1 rounded-full border text-[9px] font-black uppercase tracking-wider ${sentiment.light} ${sentiment.text} ${sentiment.border}`}>
                <span>{analysis?.sentiment || "Analyzing..."}</span>
              </button>
            </div>
            <p className="text-[10px] sm:text-xs font-bold text-slate-400 mt-1 uppercase truncate max-w-[200px] sm:max-w-none">
              {data?.companyName || "Loading..."}
            </p>
            <button onClick={() => navigate(`/strategy/${symbol}`)} className="mt-2 w-fit flex items-center gap-2 px-3 py-1.5 bg-indigo-600 text-white rounded-lg font-black text-[9px] uppercase hover:bg-indigo-700 transition-colors">
              <Zap size={12} className="fill-white" /> Strategic Command
            </button>
          </div>
        </div>
        <div className="w-full sm:w-auto text-left sm:text-right border-t sm:border-0 pt-3 sm:pt-0">
          <div className={`text-2xl sm:text-3xl md:text-4xl font-black ${isUp ? "text-emerald-600" : "text-rose-600"} tracking-tighter`}>
            ₹ {formatNum(data?.ratios?.currentPrice)}
          </div>
          <div className={`flex items-center sm:justify-end gap-1 text-xs sm:text-sm font-bold ${isUp ? "text-emerald-500" : "text-rose-500"}`}>
            {formatNum(data?.ratios?.priceChange)} ({formatNum(data?.ratios?.priceChangePercent)}%)
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
        <div className="lg:col-span-2 bg-white rounded-2xl p-4 sm:p-7 shadow-sm border border-slate-100 grid grid-cols-2 gap-y-4 gap-x-4 sm:gap-x-12 content-start">
          <RatioItem label="Market Cap" value={data?.ratios?.marketCap} />
          <RatioItem label="Price" value={`₹${formatNum(data?.ratios?.currentPrice)}`} />
          <RatioItem label="52W H/L" value={`${formatNum(data?.ratios?.high52W)} / ${formatNum(data?.ratios?.low52W)}`} />
          <RatioItem label="P/E" value={data?.ratios?.stockPE} />
          <RatioItem label="Div Yield" value={`${data?.ratios?.dividendYield}%`} />
          <RatioItem label="ROCE/ROE" value={`${data?.ratios?.roce} / ${data?.ratios?.roe}`} />
          <RatioItem label="Hist. High" value={`₹${formatNum(data?.ratios?.historicalHigh)}`} />
          <RatioItem label="Face Value" value={data?.ratios?.faceValue} />
        </div>
        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 flex flex-col overflow-hidden h-[250px] sm:h-auto sm:max-h-[300px]">
          <div className="p-3 sm:p-4 border-b border-slate-50 flex items-center gap-2 bg-slate-50/50">
            <Newspaper className="text-indigo-600" size={16} />
            <h3 className="font-bold text-slate-800 text-xs sm:text-sm uppercase">Latest News</h3>
          </div>
          <div className="overflow-y-auto divide-y divide-slate-50">
            {newsLoading ? <div className="p-4 flex justify-center"><Loader2 className="animate-spin text-slate-300" size={20} /></div> :
              news.length > 0 ? news.map((item, idx) => (
                <a key={idx} href={item.url} target="_blank" rel="noopener noreferrer" className="group block p-3 hover:bg-slate-50">
                  <div className="flex justify-between items-center mb-1">
                    <span className="text-[8px] font-black text-indigo-600 uppercase">{item.source}</span>
                    <span className="text-[8px] font-bold text-slate-400">{new Date(item.publishedAt).toLocaleDateString("en-IN", { day: "2-digit", month: "short" })}</span>
                  </div>
                  <h4 className="text-[11px] font-bold text-slate-700 line-clamp-2">{item.title}</h4>
                </a>
              )) : <p className="text-[10px] p-4 text-slate-400 font-bold italic">No recent news found.</p>}
          </div>
        </div>
      </div>

      <div className="bg-white rounded-2xl sm:rounded-3xl p-3 sm:p-6 shadow-sm border border-slate-100">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-6">
          <PeriodCard label="Period High" value={`₹${formatNum(data?.periodHigh)}`} icon={<Activity size={16} />} color="text-emerald-500" />
          <PeriodCard label="Period Low" value={`₹${formatNum(data?.periodLow)}`} icon={<Activity size={16} />} color="text-rose-500" />
          <PeriodCard label="Return" value={`${isPeriodPositive ? "+" : ""}${formatNum(data?.periodReturn)}%`} icon={<Activity size={16} />} color={isPeriodPositive ? "text-emerald-500" : "text-rose-500"} />
        </div>

        <div className="flex flex-col md:flex-row justify-between items-center mb-4 gap-4">
          <div className="flex bg-slate-100 p-1 rounded-xl gap-1 overflow-x-auto w-full md:w-auto no-scrollbar">
            {["1d", "1w", "1m", "3m", "6m", "1y", "3y", "max"].map((f) => (
              <button key={f} onClick={() => setRange(f)} className={`flex-shrink-0 px-3 sm:px-4 py-1.5 rounded-lg text-[10px] font-black uppercase transition-all ${range === f ? "bg-white text-indigo-600 shadow-sm" : "text-slate-400"}`}>
                {f}
              </button>
            ))}
          </div>
          <div className="flex gap-2 bg-slate-50 p-1 rounded-xl border border-slate-100 overflow-x-auto w-full md:w-auto no-scrollbar">
            <ToggleButton label="Volume" active={showVolumeAlways} onClick={() => setShowVolumeAlways(!showVolumeAlways)} color="#475569" />
            <ToggleButton label="50 DMA" active={showDMA50} onClick={() => setShowDMA50(!showDMA50)} color="#f59e0b" />
            <ToggleButton label="200 DMA" active={showDMA200} onClick={() => setShowDMA200(!showDMA200)} color="#64748b" />
          </div>
        </div>

        <div className="h-[250px] sm:h-[300px] md:h-[350px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data?.chartData} margin={{ left: 35, right: 35, bottom: 0, top: 10 }}>
              <defs>
                <linearGradient id="colorTrend" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={themeColor} stopOpacity={0.15} />
                  <stop offset="95%" stopColor={themeColor} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
              <XAxis dataKey="date" tickFormatter={renderDateTick} minTickGap={30} tick={{ fontSize: 9, fontWeight: 600, fill: "#94a3b8" }} axisLine={false} tickLine={false} />
              <YAxis yAxisId="vol" orientation="left" domain={[0, (dataMax) => dataMax * 1.2]} tickFormatter={formatVolumeLabel} tick={{ fontSize: 9, fill: "#6366f1" }} axisLine={false} tickLine={false} label={{ value: "VOLUME", angle: -90, position: "insideLeft", offset: -25, style: { fontSize: 9, fontWeight: 900, fill: "#cbd5e1" } }} />
              <YAxis yAxisId="price" orientation="right" domain={["auto", "auto"]} tick={{ fontSize: 10, fill: "#1e293b", fontWeight: 700 }} axisLine={false} tickLine={false} label={{ value: "PRICE (₹)", angle: 90, position: "insideRight", offset: -5, style: { fontSize: 9, fontWeight: 900, fill: "#cbd5e1" } }} />
              <Tooltip content={<CustomTooltip toggles={{ showDMA50, showDMA200 }} />} cursor={{ stroke: "#94a3b8", strokeWidth: 1, strokeDasharray: "5 5" }} />
              <Area yAxisId="price" type="monotone" dataKey="price" fill="url(#colorTrend)" stroke="none" connectNulls />
              {showVolumeAlways && <Bar yAxisId="vol" dataKey="volume" fill="#6366f1" opacity={0.6} radius={[4, 4, 0, 0]} barSize={range === "1d" ? 6 : 15} />}
              <Line yAxisId="price" type="monotone" dataKey="price" stroke={themeColor} strokeWidth={2} dot={false} connectNulls />
              {showDMA50 && <Line yAxisId="price" type="monotone" dataKey="dmA50" stroke="#f59e0b" strokeWidth={1.2} dot={false} connectNulls />}
              {showDMA200 && <Line yAxisId="price" type="monotone" dataKey="dmA200" stroke="#64748b" strokeWidth={1.2} dot={false} connectNulls />}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      {!peerLoading && peerData?.peers && (
        <div className="my-10 overflow-x-auto no-scrollbar">
          <PeerComparisonTable data={peerData} />
        </div>
      )}

      <div className="space-y-6 pt-6">
        <div className="flex bg-white p-2.5 rounded-2xl shadow-md border border-slate-100 gap-2 overflow-x-auto no-scrollbar w-full md:w-fit">
          {[{ id: "quarters", label: "QUARTERS" }, { id: "pl", label: "P&L" }, { id: "balance", label: "BALANCE" }, { id: "cash", label: "CASH" },].map((tab) => (
            <button key={tab.id} onClick={() => toggleTable(tab.id)} className={`whitespace-nowrap px-6 py-3.5 rounded-xl text-xs sm:text-sm font-black transition-all ${visibleTables[tab.id] ? "bg-indigo-600 text-white shadow-lg scale-105" : "bg-slate-50 text-slate-400"}`}>
              {visibleTables[tab.id] ? <Eye size={12} /> : <EyeOff size={12} />} {tab.label}
            </button>
          ))}
        </div>
        <div className="space-y-12">
          {visibleTables.quarters && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Quarterly Results" data={data?.quarterlyResults} /></div>}
          {visibleTables.pl && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Annual Profit & Loss" data={data?.profitAndLoss} /></div>}
          {visibleTables.balance && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Balance Sheet" data={data?.balanceSheet} /></div>}
          {visibleTables.cash && <div className="overflow-x-auto no-scrollbar"><FinancialTable title="Cash Flow Statement" data={data?.cashFlow} /></div>}
        </div>
      </div>

      {!shLoading && shareholding && <ShareholdingSection data={shareholding} analysis={analysis} onOpenTrades={handleOpenTrades} />}
      <TradeModal isOpen={isTradeModalOpen} onClose={() => setIsTradeModalOpen(false)} trades={trades} symbol={symbol} />
    </div>
  );
}

const RatioItem = ({ label, value }) => (
  <div className="flex justify-between items-center border-b border-slate-50 pb-2">
    <span className="text-slate-400 text-[10px] sm:text-xs font-medium uppercase tracking-tight">{label}</span>
    <span className="text-slate-900 font-bold text-[10px] sm:text-xs tracking-tight ml-2">{value || "N/A"}</span>
  </div>
);

const PeriodCard = ({ label, value, icon, color }) => (
  <div className="bg-slate-50/50 border border-slate-100 p-3 rounded-xl flex items-center gap-3">
    <div className={`p-1.5 bg-white rounded-lg shadow-sm ${color}`}>{icon}</div>
    <div className="min-w-0">
      <p className="text-[8px] font-black text-slate-400 uppercase tracking-widest">{label}</p>
      <p className="text-sm sm:text-base font-black text-slate-900 truncate tracking-tight">{value}</p>
    </div>
  </div>
);

const ToggleButton = ({ label, active, onClick, color }) => (
  <button onClick={onClick} className={`whitespace-nowrap px-3 py-1.5 rounded-lg text-[9px] font-bold border transition-all ${active ? "bg-white border-slate-200 shadow-sm" : "bg-transparent border-transparent text-slate-400"}`} style={{ color: active ? color : undefined }}>
    {label}
  </button>
);

const CustomTooltip = ({ active, payload, toggles }) => {
  if (active && payload && payload.length) {
    const d = payload[0].payload;
    return (
      <div className="bg-slate-900/95 text-white p-2.5 rounded-xl text-[10px] shadow-2xl border border-slate-800 min-w-[140px] backdrop-blur-md">
        <p className="font-black text-indigo-300 border-b border-slate-800 pb-1 mb-1.5 text-center uppercase">
          {new Date(d.date).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" })}
        </p>
        <div className="space-y-1">
          <div className="flex justify-between gap-4">
            <span>Price:</span>
            <span className="font-black text-white">₹{Number(d.price || 0).toFixed(1)}</span>
          </div>
          {toggles.showDMA50 && d.dmA50 && (
            <div className="flex justify-between gap-4">
              <span className="text-amber-500">50D:</span>
              <span className="font-black text-amber-200">₹{Number(d.dmA50).toFixed(1)}</span>
            </div>
          )}
          {toggles.showDMA200 && d.dmA200 && (
            <div className="flex justify-between gap-4">
              <span className="text-slate-400">200D:</span>
              <span className="font-black text-slate-300">₹{Number(d.dmA200).toFixed(1)}</span>
            </div>
          )}
          <div className="flex justify-between gap-4 border-t border-slate-800 pt-1">
            <span>Vol:</span>
            <span className="font-bold">{d.volume?.toLocaleString("en-IN")}</span>
          </div>
        </div>
      </div>
    );
  }
  return null;
};