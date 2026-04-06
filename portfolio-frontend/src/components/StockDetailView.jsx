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
  TrendingUp,
  TrendingDown,
  Newspaper,
  Zap,
  X,
  CheckCircle2,
  AlertCircle,
  Eye,
  EyeOff,
  ArrowUp,
  ArrowDown,
  Activity,
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
    if (score >= 80)
      return {
        bg: "bg-emerald-600",
        text: "text-emerald-600",
        light: "bg-emerald-50",
        border: "border-emerald-200",
        icon: <TrendingUp size={16} />,
      };
    if (score >= 65)
      return {
        bg: "bg-emerald-400",
        text: "text-emerald-500",
        light: "bg-emerald-50/50",
        border: "border-emerald-100",
        icon: <TrendingUp size={16} />,
      };
    if (score >= 45)
      return {
        bg: "bg-amber-500",
        text: "text-amber-600",
        light: "bg-amber-50",
        border: "border-amber-200",
        icon: <Activity size={16} />,
      };
    if (score >= 25)
      return {
        bg: "bg-rose-400",
        text: "text-rose-500",
        light: "bg-rose-50/50",
        border: "border-rose-100",
        icon: <TrendingDown size={16} />,
      };
    return {
      bg: "bg-rose-700",
      text: "text-rose-700",
      light: "bg-rose-50",
      border: "border-rose-200",
      icon: <AlertCircle size={16} />,
    };
  };

  const sentiment = getSentimentConfig(analysis?.score || 0);

  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchData = async () => {
      try {
        setLoading(true);
        const [analRes, newsRes, detRes, shRes, peerRes] = await Promise.all([
          api.get(`/stocks/analyze/${symbol}`),
          api.get(`/portfolio/news/${symbol}`),
          api.get(`/stocks/details/${symbol}?range=${range}`),
          api.get(`/stocks/${symbol}/shareholding`),
          api.get(`/Stocks/peers/${symbol}`),
        ]);
        setAnalysis(analRes.data);
        setNews(newsRes.data.slice(0, 7));
        setData(detRes.data);
        setShareholding(shRes.data);
        if (peerRes.data) {
          setPeerData({
            industry: detRes.data.industry || peerRes.data.industry,
            peers: peerRes.data.peers || peerRes.data,
          });
        }
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
        setNewsLoading(false);
        setShLoading(false);
        setPeerLoading(false);
      }
    };
    fetchData();
  }, [symbol, range]);

  const handleOpenTrades = async () => {
    try {
      if (!trades) {
        const res = await api.get(`/stocks/${symbol}/trades`);
        setTrades(res.data);
      }
      setIsTradeModalOpen(true);
    } catch (err) {
      console.error(err);
    }
  };

  const toggleTable = (id) =>
    setVisibleTables((prev) => ({ ...prev, [id]: !prev[id] }));

  const formatNum = (val, decimals = 2) => {
    if (val === null || val === undefined || val === "N/A") return "N/A";
    return Number(val).toLocaleString("en-IN", {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
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
    .replace("emerald-600", "059669")
    .replace("emerald-500", "10b981")
    .replace("amber-600", "d97706")
    .replace("rose-500", "f43f5e")
    .replace("rose-700", "be123c")
    .replace("text-", "#");

  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    if (range === "1d")
      return date.toLocaleTimeString("en-IN", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      });
    return date.toLocaleDateString("en-IN", { day: "2-digit", month: "short" });
  };

  if (loading && !data)
    return (
      <div className="flex h-screen items-center justify-center bg-slate-50">
        <Loader2 className="animate-spin text-indigo-600" size={40} />
      </div>
    );

  return (
    <div className="w-full max-w-7xl mx-auto p-3 sm:p-4 md:p-6 space-y-4 sm:space-y-6 bg-slate-50 min-h-screen font-sans relative pb-20 overflow-x-hidden">
      {/* ANALYSIS MODAL */}
      {isAnalysisModalOpen && analysis && (
        <div className="fixed inset-0 z-[999] flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm"
            onClick={() => setIsAnalysisModalOpen(false)}
          />
          <div className="relative bg-white w-full max-w-lg rounded-[32px] shadow-2xl overflow-hidden border border-slate-100">
            <div className={`h-2.5 w-full ${sentiment.bg}`} />
            <div className="p-6 sm:p-8">
              <div className="flex justify-between items-start mb-6">
                <div>
                  <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest mb-1">
                    Intelligence Core
                  </p>
                  <h2
                    className={`text-xl sm:text-2xl font-black tracking-tight flex items-center gap-2 ${sentiment.text}`}
                  >
                    {analysis.sentiment}
                  </h2>
                </div>
                <button
                  onClick={() => setIsAnalysisModalOpen(false)}
                  className="p-2 hover:bg-slate-100 rounded-full text-slate-400"
                >
                  <X size={20} />
                </button>
              </div>
              <div className="mb-8 bg-slate-50 p-4 sm:p-5 rounded-2xl border border-slate-100">
                <div className="flex justify-between items-end mb-2">
                  <span className="text-[11px] font-bold text-slate-500 uppercase tracking-tight">
                    Confidence Index
                  </span>
                  <span
                    className={`text-xl sm:text-2xl font-black ${sentiment.text}`}
                  >
                    {analysis.score}%
                  </span>
                </div>
                <div className="h-2 w-full bg-white rounded-full overflow-hidden border border-slate-200/50">
                  <div
                    className={`h-full transition-all duration-1000 ${sentiment.bg}`}
                    style={{ width: `${analysis.score}%` }}
                  />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3 mb-8">
                {Object.entries(analysis.performanceMatrix || {}).map(
                  ([key, val]) => {
                    if (key === "Handover" || key === "Absorption") return null;
                    return (
                      <div
                        key={key}
                        className="bg-white p-3 sm:p-4 rounded-2xl border border-slate-100 shadow-sm"
                      >
                        <p className="text-[9px] font-bold text-slate-400 uppercase mb-1">
                          {key}
                        </p>
                        <p className="text-xs sm:text-sm font-black text-slate-800 tracking-tight">
                          {val}
                        </p>
                      </div>
                    );
                  },
                )}
              </div>
              <button
                onClick={() => setIsAnalysisModalOpen(false)}
                className={`w-full py-4 text-white rounded-2xl font-black text-xs uppercase shadow-lg ${sentiment.bg} hover:brightness-110`}
              >
                Close Intelligence Report
              </button>
            </div>
          </div>
        </div>
      )}

      {/* HEADER */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center bg-white p-4 sm:p-5 rounded-2xl shadow-sm border border-slate-100 gap-4">
        <div className="flex items-start sm:items-center gap-3 sm:gap-4">
          <button
            onClick={() => navigate("/")}
            className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          >
            <ArrowLeft size={20} />
          </button>
          <div className="flex flex-col">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl sm:text-2xl md:text-3xl font-black text-slate-900 tracking-tighter uppercase leading-none">
                {data?.symbol || symbol}
              </h1>
              <button
                onClick={() => setIsAnalysisModalOpen(true)}
                className={`flex items-center gap-1 px-2.5 py-1 rounded-full border text-[9px] font-black uppercase tracking-wider ${sentiment.light} ${sentiment.text} ${sentiment.border}`}
              >
                {sentiment.icon}{" "}
                <span>{analysis?.sentiment || "Analyzing..."}</span>
              </button>
            </div>
            <p className="text-[10px] sm:text-xs font-bold text-slate-400 mt-1 uppercase truncate max-w-[200px] sm:max-w-none">
              {data?.companyName || "Loading Asset Name..."}
            </p>
            <button
              onClick={() => navigate(`/strategy/${symbol}`)}
              className="mt-2 w-fit flex items-center gap-2 px-3 py-1.5 bg-indigo-600 text-white rounded-lg font-black text-[9px] uppercase hover:bg-indigo-700 transition-colors"
            >
              <Zap size={12} className="fill-white" /> Strategic Command
            </button>
          </div>
        </div>
        <div className="w-full md:w-auto text-left md:text-right border-t md:border-0 pt-3 md:pt-0 flex flex-row md:flex-col justify-between items-center md:items-end">
          <div
            className={`text-2xl sm:text-3xl md:text-4xl font-black ${isUp ? "text-emerald-600" : "text-rose-600"} tracking-tighter`}
          >
            ₹ {formatNum(data?.ratios?.currentPrice)}
          </div>
          <div
            className={`flex items-center gap-1 text-xs sm:text-sm font-bold ${isUp ? "text-emerald-500" : "text-rose-500"}`}
          >
            {isUp ? <TrendingUp size={16} /> : <TrendingDown size={16} />}{" "}
            {formatNum(data?.ratios?.priceChange)} (
            {formatNum(data?.ratios?.priceChangePercent)}%)
          </div>
        </div>
      </div>

      {/* RATIOS & NEWS GRID */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
        <div className="lg:col-span-2 bg-white rounded-2xl p-4 sm:p-7 shadow-sm border border-slate-100 grid grid-cols-2 gap-y-4 gap-x-4 sm:gap-x-12 content-start">
          <RatioItem label="Market Cap" value={`${data?.ratios?.marketCap}`} />
          <RatioItem
            label="Price"
            value={`₹ ${formatNum(data?.ratios?.currentPrice)}`}
          />
          <RatioItem
            label="52W High / Low"
            value={`₹${formatNum(data?.ratios?.high52W)} / ${formatNum(data?.ratios?.low52W)}`}
          />
          <RatioItem label="Stock P/E" value={data?.ratios?.stockPE} />
          <RatioItem
            label="Dividend Yield"
            value={`${data?.ratios?.dividendYield}%`}
          />
          <RatioItem
            label="ROCE / ROE"
            value={`${data?.ratios?.roce} / ${data?.ratios?.roe}`}
          />
          <RatioItem
            label="Historical High"
            value={`₹ ${formatNum(data?.ratios?.historicalHigh)}`}
          />
          <RatioItem label="Face Value" value={data?.ratios?.faceValue} />
        </div>
        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 flex flex-col overflow-hidden max-h-[300px]">
          <div className="p-3 border-b border-slate-50 flex items-center gap-2 bg-slate-50/50">
            <Newspaper className="text-indigo-600" size={16} />
            <h3 className="font-bold text-slate-800 text-xs uppercase">
              Latest News
            </h3>
          </div>
          <div className="overflow-y-auto divide-y divide-slate-50">
            {news.map((item, idx) => (
              <a
                key={idx}
                href={item.url}
                target="_blank"
                rel="noopener noreferrer"
                className="group block p-3 hover:bg-slate-50"
              >
                <div className="flex justify-between items-center mb-1">
                  <span className="text-[8px] font-black text-indigo-600 uppercase">
                    {item.source}
                  </span>
                  <span className="text-[8px] font-bold text-slate-400">
                    {new Date(item.publishedAt).toLocaleDateString("en-IN", {
                      day: "2-digit",
                      month: "short",
                    })}
                  </span>
                </div>
                <h4 className="text-[11px] font-bold text-slate-700 leading-snug line-clamp-2 group-hover:text-indigo-600">
                  {item.title}
                </h4>
              </a>
            ))}
          </div>
        </div>
      </div>

      {/* CHART SECTION: Dual Axis with Labels */}
      <div className="bg-white rounded-2xl sm:rounded-3xl p-4 sm:p-6 shadow-sm border border-slate-100">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-6">
          <PeriodCard
            label="Period High"
            value={`₹${formatNum(data?.periodHigh)}`}
            icon={<ArrowUp size={16} />}
            color="text-emerald-500"
          />
          <PeriodCard
            label="Period Low"
            value={`₹${formatNum(data?.periodLow)}`}
            icon={<ArrowDown size={16} />}
            color="text-rose-500"
          />
          <PeriodCard
            label="Return"
            value={`${isPeriodPositive ? "+" : ""}${formatNum(data?.periodReturn)}%`}
            icon={<Activity size={16} />}
            color={isPeriodPositive ? "text-emerald-500" : "text-rose-500"}
          />
        </div>

        <div className="flex flex-col lg:flex-row justify-between items-center mb-6 gap-4">
          <div className="flex bg-slate-100 p-1 rounded-xl gap-1 overflow-x-auto w-full lg:w-auto no-scrollbar">
            {["1d", "1w", "1m", "3m", "6m", "1y", "3y", "max"].map((f) => (
              <button
                key={f}
                onClick={() => setRange(f)}
                className={`flex-shrink-0 px-3 sm:px-4 py-1.5 rounded-lg text-[10px] font-black uppercase transition-all ${range === f ? "bg-white text-indigo-600 shadow-sm" : "text-slate-400 hover:text-slate-600"}`}
              >
                {f}
              </button>
            ))}
          </div>
          <div className="flex gap-2 bg-slate-50 p-1 rounded-xl border border-slate-100 overflow-x-auto w-full lg:w-auto no-scrollbar">
            <ToggleButton
              label="Volume"
              active={showVolumeAlways}
              onClick={() => setShowVolumeAlways(!showVolumeAlways)}
              color="#475569"
            />
            <ToggleButton
              label="50 DMA"
              active={showDMA50}
              onClick={() => setShowDMA50(!showDMA50)}
              color="#f59e0b"
            />
            <ToggleButton
              label="200 DMA"
              active={showDMA200}
              onClick={() => setShowDMA200(!showDMA200)}
              color="#64748b"
            />
          </div>
        </div>

        <div className="h-[300px] sm:h-[400px] md:h-[450px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart
              data={data?.chartData}
              margin={{ left: 35, right: 35, bottom: 0, top: 10 }}
            >
              <defs>
                <linearGradient id="colorTrend" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={themeColor} stopOpacity={0.15} />
                  <stop offset="95%" stopColor={themeColor} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid
                strokeDasharray="3 3"
                vertical={false}
                stroke="#f1f5f9"
              />
              <XAxis
                dataKey="date"
                tickFormatter={renderDateTick}
                minTickGap={30}
                tick={{ fontSize: 9, fontWeight: 600, fill: "#94a3b8" }}
                axisLine={false}
                tickLine={false}
              />

              {/* LEFT Y-AXIS: VOLUME */}
              <YAxis
                yAxisId="vol"
                orientation="left"
                domain={[0, (dataMax) => dataMax * 1.2]}
                tickFormatter={formatVolumeLabel}
                tick={{ fontSize: 9, fill: "#6366f1" }}
                axisLine={false}
                tickLine={false}
                label={{
                  value: "VOLUME",
                  angle: -90,
                  position: "insideLeft",
                  offset: -25,
                  style: { fontSize: 9, fontWeight: 900, fill: "#cbd5e1" },
                }}
              />

              {/* RIGHT Y-AXIS: PRICE */}
              <YAxis
                yAxisId="price"
                orientation="right"
                domain={["auto", "auto"]}
                tick={{ fontSize: 10, fill: "#1e293b", fontWeight: 700 }}
                axisLine={false}
                tickLine={false}
                label={{
                  value: "PRICE (₹)",
                  angle: 90,
                  position: "insideRight",
                  offset: -5,
                  style: { fontSize: 9, fontWeight: 900, fill: "#cbd5e1" },
                }}
              />

              <Tooltip
                content={
                  <CustomTooltip
                    range={range}
                    toggles={{ showDMA50, showDMA200 }}
                  />
                }
                cursor={{
                  stroke: "#94a3b8",
                  strokeWidth: 1,
                  strokeDasharray: "5 5",
                }}
              />

              <Area
                yAxisId="price"
                type="monotone"
                dataKey="price"
                fill="url(#colorTrend)"
                stroke="none"
                connectNulls
              />
              {showVolumeAlways && (
                <Bar
                  yAxisId="vol"
                  dataKey="volume"
                  fill="#6366f1" /* Vibrant Indigo */
                  opacity={0.6} /* Higher opacity for "Bold" look */
                  radius={[4, 4, 0, 0]} /* Slightly more rounded top */
                  barSize={range === "1d" ? 6 : 15} /* Thicker bars */
                />
              )}
              <Line
                yAxisId="price"
                type="monotone"
                dataKey="price"
                stroke={themeColor}
                strokeWidth={2}
                dot={false}
                connectNulls
              />
              {showDMA50 && (
                <Line
                  yAxisId="price"
                  type="monotone"
                  dataKey="dmA50"
                  stroke="#f59e0b"
                  strokeWidth={1.2}
                  dot={false}
                  connectNulls
                />
              )}
              {showDMA200 && (
                <Line
                  yAxisId="price"
                  type="monotone"
                  dataKey="dmA200"
                  stroke="#64748b"
                  strokeWidth={1.2}
                  dot={false}
                  connectNulls
                />
              )}
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
        <div className="flex bg-white p-1.5 rounded-xl shadow-sm border border-slate-100 gap-1 overflow-x-auto no-scrollbar w-full md:w-fit">
          {[
            { id: "quarters", label: "Quarters" },
            { id: "pl", label: "P&L" },
            { id: "balance", label: "Balance" },
            { id: "cash", label: "Cash" },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => toggleTable(tab.id)}
              className={`whitespace-nowrap px-6 py-3.5 rounded-xl text-xs sm:text-sm font-black transition-all shadow-sm ${visibleTables[tab.id] ? "bg-indigo-600 text-white shadow-lg shadow-indigo-200 scale-105" : "bg-slate-50 text-slate-400 hover:bg-slate-100"}`}
            >
              {visibleTables[tab.id] ? <Eye size={12} /> : <EyeOff size={12} />}{" "}
              {tab.label}
            </button>
          ))}
        </div>
        <div className="space-y-12">
          {visibleTables.quarters && (
            <div className="overflow-x-auto no-scrollbar">
              <FinancialTable
                title="Quarterly Results"
                data={data?.quarterlyResults}
              />
            </div>
          )}
          {visibleTables.pl && (
            <div className="overflow-x-auto no-scrollbar">
              <FinancialTable
                title="Annual Profit & Loss"
                data={data?.profitAndLoss}
              />
            </div>
          )}
          {visibleTables.balance && (
            <div className="overflow-x-auto no-scrollbar">
              <FinancialTable title="Balance Sheet" data={data?.balanceSheet} />
            </div>
          )}
          {visibleTables.cash && (
            <div className="overflow-x-auto no-scrollbar">
              <FinancialTable
                title="Cash Flow Statement"
                data={data?.cashFlow}
              />
            </div>
          )}
        </div>
      </div>

      {!shLoading && shareholding && (
        <ShareholdingSection
          data={shareholding}
          analysis={analysis}
          onOpenTrades={handleOpenTrades}
        />
      )}

      <TradeModal
        isOpen={isTradeModalOpen}
        onClose={() => setIsTradeModalOpen(false)}
        trades={trades}
        symbol={symbol}
      />
    </div>
  );
}

/* --- SUB-COMPONENTS --- */
const RatioItem = ({ label, value }) => (
  <div className="flex justify-between items-center border-b border-slate-50 pb-2">
    <span className="text-slate-400 text-[10px] sm:text-xs font-medium uppercase tracking-tight">
      {label}
    </span>
    <span className="text-slate-900 font-bold text-[10px] sm:text-xs tracking-tight ml-2">
      {value || "N/A"}
    </span>
  </div>
);

const PeriodCard = ({ label, value, icon, color }) => (
  <div className="bg-slate-50/50 border border-slate-100 p-3 rounded-xl flex items-center gap-3">
    <div className={`p-1.5 bg-white rounded-lg shadow-sm ${color}`}>{icon}</div>
    <div className="min-w-0">
      <p className="text-[8px] font-black text-slate-400 uppercase tracking-widest">
        {label}
      </p>
      <p className="text-sm sm:text-base font-black text-slate-900 truncate tracking-tight">
        {value}
      </p>
    </div>
  </div>
);

const ToggleButton = ({ label, active, onClick, color }) => (
  <button
    onClick={onClick}
    className={`whitespace-nowrap px-3 py-1.5 rounded-lg text-[9px] font-bold border transition-all ${active ? "bg-white border-slate-200 shadow-sm" : "bg-transparent border-transparent text-slate-400"}`}
    style={{ color: active ? color : undefined }}
  >
    {label}
  </button>
);

const CustomTooltip = ({ active, payload, toggles }) => {
  if (active && payload && payload.length) {
    const d = payload[0].payload;
    return (
      <div className="bg-slate-900/95 text-white p-2.5 rounded-xl text-[10px] shadow-2xl border border-slate-800 min-w-[140px] backdrop-blur-md">
        <p className="font-black text-indigo-300 border-b border-slate-800 pb-1 mb-1.5 text-center uppercase">
          {new Date(d.date).toLocaleDateString("en-IN", {
            day: "2-digit",
            month: "short",
          })}
        </p>
        <div className="space-y-1">
          <div className="flex justify-between gap-4">
            <span>Price:</span>
            <span className="font-black text-white">
              ₹{Number(d.price || 0).toFixed(1)}
            </span>
          </div>
          {toggles.showDMA50 && d.dmA50 && (
            <div className="flex justify-between gap-4">
              <span className="text-amber-500">50D:</span>
              <span className="font-black text-amber-200">
                ₹{Number(d.dmA50).toFixed(1)}
              </span>
            </div>
          )}
          {toggles.showDMA200 && d.dmA200 && (
            <div className="flex justify-between gap-4">
              <span className="text-slate-400">200D:</span>
              <span className="font-black text-slate-300">
                ₹{Number(d.dmA200).toFixed(1)}
              </span>
            </div>
          )}
          <div className="flex justify-between gap-4 border-t border-slate-800 pt-1">
            <span>Vol:</span>
            <span className="font-bold">
              {d.volume?.toLocaleString("en-IN")}
            </span>
          </div>
        </div>
      </div>
    );
  }
  return null;
};
