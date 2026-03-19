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
  Label,
} from "recharts";
import {
  Loader2,
  ArrowLeft,
  TrendingUp,
  TrendingDown,
  Newspaper,
  ArrowUpRight,
  Eye,
  EyeOff,
  Info,
  Zap,
  X,
  Activity,
  BarChart3,
} from "lucide-react";
import api from "../api/axios";
import FinancialTable from "./FinancialTable";

export default function StockDetailView() {
  const { symbol } = useParams();
  const navigate = useNavigate();

  const [data, setData] = useState(null);
  const [analysis, setAnalysis] = useState(null);
  const [showAnalysisPanel, setShowAnalysisPanel] = useState(false);
  const [news, setNews] = useState([]);
  const [loading, setLoading] = useState(true);
  const [newsLoading, setNewsLoading] = useState(true);
  const [range, setRange] = useState("1y");

  const [visibleTables, setVisibleTables] = useState({
    quarters: true,
    pl: false,
    balance: false,
    cash: false,
  });

  const [showPrice, setShowPrice] = useState(true);
  const [showDMA50, setShowDMA50] = useState(false);
  const [showDMA200, setShowDMA200] = useState(false);
  const [showVolumeAlways, setShowVolumeAlways] = useState(true);

  // Effect: Fetch Sentiment Analysis
  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchAnalysis = async () => {
      try {
        const res = await api.get(`/stocks/analyze/${symbol}`);
        setAnalysis(res.data);
      } catch (err) {
        console.error("Analysis Error:", err);
      }
    };
    fetchAnalysis();
  }, [symbol]);

  // Effect: Fetch News
  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchNews = async () => {
      setNewsLoading(true);
      try {
        const res = await api.get(`/portfolio/news/${symbol}`);
        setNews(res.data.slice(0, 7));
      } catch (err) {
        console.error("News Fetch Error:", err);
      } finally {
        setNewsLoading(false);
      }
    };
    fetchNews();
  }, [symbol]);

  // Effect: Fetch Stock Details
  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchDetails = async () => {
      setLoading(true);
      try {
        const res = await api.get(`/stocks/details/${symbol}?range=${range}`);
        setData(res.data);
      } catch (err) {
        console.error("Detail Fetch Error:", err);
      } finally {
        setLoading(false);
      }
    };
    fetchDetails();
  }, [symbol, range]);

  const toggleTable = (id) =>
    setVisibleTables((prev) => ({ ...prev, [id]: !prev[id] }));

  const formatNum = (val, decimals = 2) => {
    if (val === null || val === undefined || val === "N/A") return "N/A";
    const num = Number(val);
    return isNaN(num) ? "N/A" : num.toFixed(decimals);
  };

  const isUp = data?.ratios?.priceChange >= 0;
  const themeColor = isUp ? "#10b981" : "#f43f5e";

  const getSentTheme = (sent) => {
    if (!sent) return "bg-slate-50 text-slate-400 border-slate-100";
    if (sent.includes("Strong") || sent.includes("Positive"))
      return "bg-emerald-50 text-emerald-700 border-emerald-100";
    if (sent.includes("Pressure") || sent.includes("Correction"))
      return "bg-rose-50 text-rose-700 border-rose-100";
    return "bg-amber-50 text-amber-700 border-amber-100";
  };

  // --- X-AXIS IST DATE FORMATTING ---
  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    if (range === "1d") {
      return date.toLocaleTimeString("en-IN", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      });
    }
    if (range === "1w") {
      return (
        date.toLocaleDateString("en-IN", { day: "2-digit", month: "short" }) +
        " " +
        date.toLocaleTimeString("en-IN", {
          hour: "2-digit",
          minute: "2-digit",
          hour12: false,
        })
      );
    }
    const options =
      range === "1m" || range === "3m"
        ? { day: "2-digit", month: "short" }
        : { month: "short", year: "2-digit" };
    return date.toLocaleDateString("en-IN", options);
  };

  if (loading && !data)
    return (
      <div className="flex h-screen items-center justify-center bg-slate-50">
        <Loader2 className="animate-spin text-indigo-600" />
      </div>
    );

  return (
    <div className="max-w-7xl mx-auto p-4 space-y-8 bg-slate-50 min-h-screen font-sans pb-24">
      {/* HEADER SECTION */}
      <div className="flex justify-between items-center bg-white p-8 rounded-3xl shadow-sm border border-slate-100">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate("/")}
            className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          >
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="text-3xl font-black text-slate-900 leading-tight">
              {data?.symbol || symbol}
            </h1>
            <div className="flex items-center gap-2 mt-2">
              <div
                className={`flex items-center gap-1.5 px-3 py-1 rounded-full border text-[10px] font-black uppercase tracking-wider ${getSentTheme(analysis?.sentiment)}`}
              >
                {analysis?.score >= 1 ? (
                  <Zap size={12} fill="currentColor" />
                ) : (
                  <Activity size={12} />
                )}
                {analysis?.sentiment || "Analyzing..."}
              </div>
              <button
                onClick={() => setShowAnalysisPanel(true)}
                className="p-1 text-slate-400 hover:text-indigo-600 transition-colors"
              >
                <Info size={16} />
              </button>
            </div>
          </div>
        </div>
        <div className="text-right">
          <div
            className={`text-4xl font-black ${isUp ? "text-emerald-600" : "text-rose-600"}`}
          >
            ₹ {formatNum(data?.ratios?.currentPrice)}
          </div>
          <div
            className={`flex items-center justify-end gap-1 text-sm font-bold ${isUp ? "text-emerald-500" : "text-rose-500"}`}
          >
            {isUp ? <TrendingUp size={16} /> : <TrendingDown size={16} />}
            {isUp ? "+" : ""}
            {formatNum(data?.ratios?.priceChange)} (
            {formatNum(data?.ratios?.priceChangePercent)}%)
          </div>
        </div>
      </div>

      {/* ANALYSIS SIDE-OVER PANEL */}
      {showAnalysisPanel && (
        <div className="fixed inset-0 z-[100] flex justify-end">
          <div
            className="absolute inset-0 bg-slate-900/40 backdrop-blur-md"
            onClick={() => setShowAnalysisPanel(false)}
          />
          <div className="relative w-full max-w-md bg-white/95 h-full shadow-2xl p-8 flex flex-col border-l border-white/20">
            <div className="flex justify-between items-center mb-10">
              <h3 className="text-2xl font-black text-slate-900 tracking-tight">
                Kinetic Analysis
              </h3>
              <button
                onClick={() => setShowAnalysisPanel(false)}
                className="p-2 hover:bg-slate-100 rounded-xl transition-colors text-slate-400"
              >
                <X size={24} />
              </button>
            </div>
            <div className="space-y-8 overflow-y-auto pr-2">
              <div className="p-8 rounded-[2.5rem] bg-gradient-to-br from-slate-900 to-indigo-950 text-white shadow-xl">
                <p className="text-[10px] font-black opacity-50 uppercase tracking-widest mb-1">
                  Current Trend
                </p>
                <h4 className="text-3xl font-black mb-6">
                  {analysis?.sentiment}
                </h4>
                <div className="flex justify-between items-end border-t border-white/10 pt-4">
                  <BarChart3 size={32} className="text-indigo-400 opacity-50" />
                  <div className="text-right">
                    <span className="text-[10px] block opacity-40 font-bold uppercase">
                      Logic Score
                    </span>
                    <span className="text-2xl font-black text-indigo-300">
                      {analysis?.score}/3
                    </span>
                  </div>
                </div>
              </div>
              <div className="space-y-4">
                <h5 className="text-[10px] font-black text-slate-400 uppercase tracking-widest ml-1">
                  Key Evidence
                </h5>
                {analysis?.reasons.map((reason, idx) => (
                  <div
                    key={idx}
                    className="flex gap-4 p-4 rounded-2xl bg-slate-50 border border-slate-100 hover:border-indigo-100 transition-colors"
                  >
                    <div className="w-8 h-8 rounded-xl bg-white shadow-sm flex items-center justify-center shrink-0">
                      <Zap size={14} className="text-indigo-600" />
                    </div>
                    <p className="text-sm font-bold text-slate-700 leading-snug">
                      {reason}
                    </p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* RATIOS & NEWS SECTION */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 bg-white rounded-3xl p-8 shadow-sm border border-slate-100 grid grid-cols-1 md:grid-cols-2 gap-y-6 gap-x-12 content-start">
          <RatioItem label="Market Cap" value={data?.ratios?.marketCap} />
          <RatioItem
            label="Current Price"
            value={`₹ ${formatNum(data?.ratios?.currentPrice)}`}
          />
          <RatioItem
            label="52W High / Low"
            value={`₹ ${formatNum(data?.ratios?.high52W)} / ${formatNum(data?.ratios?.low52W)}`}
          />
          <RatioItem label="Stock P/E" value={data?.ratios?.stockPE} />
          <RatioItem
            label="Dividend Yield"
            value={data?.ratios?.dividendYield}
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

        <div className="bg-white rounded-3xl shadow-sm border border-slate-100 flex flex-col overflow-hidden">
          <div className="p-6 border-b border-slate-50 flex items-center gap-2 bg-slate-50/50">
            <Newspaper className="text-indigo-600" size={18} />
            <h3 className="font-bold text-slate-800 text-sm">Latest News</h3>
          </div>
          <div className="overflow-y-auto max-h-[400px] divide-y divide-slate-50">
            {!newsLoading &&
              news.map((item, idx) => (
                <a
                  key={idx}
                  href={item.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="group block p-4 hover:bg-slate-50 transition-all"
                >
                  <div className="flex justify-between items-start gap-3">
                    <div className="space-y-1">
                      <span className="text-[9px] font-black text-indigo-600 uppercase">
                        {item.source}
                      </span>
                      <h4 className="text-[12px] font-bold text-slate-700 leading-snug group-hover:text-indigo-600 transition-colors line-clamp-2">
                        {item.title}
                      </h4>
                    </div>
                    <ArrowUpRight
                      size={14}
                      className="text-slate-300 group-hover:text-indigo-600 shrink-0"
                    />
                  </div>
                </a>
              ))}
          </div>
        </div>
      </div>

      {/* CHART SECTION */}
      <div className="bg-white rounded-3xl p-6 shadow-sm border border-slate-100">
        <div className="flex justify-between items-center mb-8">
          <div className="flex bg-slate-100 p-1 rounded-xl gap-1">
            {["1d", "1w", "1m", "3m", "6m", "1y", "max"].map((f) => (
              <button
                key={f}
                onClick={() => setRange(f)}
                className={`px-4 py-1.5 rounded-lg text-xs font-black uppercase transition-all ${range === f ? "bg-white text-indigo-600 shadow-sm" : "text-slate-400 hover:text-slate-600"}`}
              >
                {f}
              </button>
            ))}
          </div>
          <div className="flex gap-2 bg-slate-50 p-1 rounded-xl border border-slate-100">
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

        <div
          className="h-[300px] w-full min-w-0"
          style={{ position: "relative" }}
        >
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart
              data={data?.chartData}
              margin={{ left: 10, right: 45, bottom: 10, top: 10 }}
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
                minTickGap={40}
                tick={{ fontSize: 10, fontWeight: 600, fill: "#94a3b8" }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                yAxisId="vol"
                orientation="left"
                domain={[0, (dataMax) => dataMax * 1.5]}
                tick={false}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                yAxisId="price"
                orientation="right"
                domain={["auto", "auto"]}
                tickCount={6}
                tick={{ fontSize: 10, fill: "#64748b", fontWeight: 700 }}
                axisLine={false}
                tickLine={false}
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
                stroke="none"
                fill="url(#colorTrend)"
                connectNulls
              />
              {showVolumeAlways && (
                <Bar
                  yAxisId="vol"
                  dataKey="volume"
                  fill="#cbd5e1"
                  opacity={0.4}
                  barSize={range === "1d" ? 15 : 6}
                />
              )}
              {showPrice && (
                <Line
                  yAxisId="price"
                  type="monotone"
                  dataKey="price"
                  stroke={themeColor}
                  strokeWidth={2.5}
                  dot={false}
                  activeDot={{ r: 4, strokeWidth: 0 }}
                />
              )}
              {showDMA50 && (
                <Line
                  yAxisId="price"
                  type="monotone"
                  dataKey="dmA50"
                  stroke="#f59e0b"
                  strokeWidth={1.5}
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
                  strokeWidth={1.5}
                  dot={false}
                  connectNulls
                />
              )}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* FINANCIAL ANALYSIS TABLES */}
      <div className="space-y-6">
        <div className="flex flex-wrap bg-white p-2 rounded-2xl shadow-sm border border-slate-100 gap-2 w-fit">
          {[
            { id: "quarters", label: "Quarterly Results" },
            { id: "pl", label: "Profit & Loss" },
            { id: "balance", label: "Balance Sheet" },
            { id: "cash", label: "Cash Flow" },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => toggleTable(tab.id)}
              className={`flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-black transition-all ${visibleTables[tab.id] ? "bg-indigo-600 text-white shadow-lg shadow-indigo-200" : "bg-slate-50 text-slate-400 hover:text-slate-600 hover:bg-slate-100 border border-transparent hover:border-slate-200"}`}
            >
              {visibleTables[tab.id] ? <Eye size={14} /> : <EyeOff size={14} />}{" "}
              {tab.label}
            </button>
          ))}
        </div>

        <div className="space-y-12">
          {visibleTables.quarters && (
            <FinancialTable
              title="Quarterly Results"
              data={data?.quarterlyResults}
            />
          )}
          {visibleTables.pl && (
            <FinancialTable
              title="Annual Profit & Loss"
              data={data?.profitAndLoss}
            />
          )}
          {visibleTables.balance && (
            <FinancialTable title="Balance Sheet" data={data?.balanceSheet} />
          )}
          {visibleTables.cash && (
            <FinancialTable title="Cash Flow Statement" data={data?.cashFlow} />
          )}
        </div>
      </div>
    </div>
  );
}

const RatioItem = ({ label, value }) => (
  <div className="flex justify-between items-center border-b border-slate-50 pb-2">
    <span className="text-slate-400 text-sm font-medium">{label}</span>
    <span className="text-slate-900 font-bold text-sm tracking-tight">
      {typeof value === "number"
        ? value.toLocaleString("en-IN")
        : value || "N/A"}
    </span>
  </div>
);

const ToggleButton = ({ label, active, onClick, color }) => (
  <button
    onClick={onClick}
    className={`px-3 py-1.5 rounded-lg text-[10px] font-bold border transition-all ${active ? "bg-white border-slate-200 shadow-sm" : "bg-transparent border-transparent text-slate-400"}`}
    style={{ color: active ? color : undefined }}
  >
    {label}
  </button>
);

// --- RECTANGULAR STRIP COMPONENT ---
const CustomTooltip = ({ active, payload, range, toggles = {} }) => {
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    const dateObj = new Date(data.date);

    let label = "";
    if (range === "1d")
      label = dateObj.toLocaleTimeString("en-IN", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      });
    else if (range === "1w")
      label = dateObj
        .toLocaleString("en-IN", {
          day: "2-digit",
          month: "short",
          hour: "2-digit",
          minute: "2-digit",
        })
        .replace(",", " |");
    else
      label = dateObj.toLocaleDateString("en-IN", {
        day: "2-digit",
        month: "short",
        year: "numeric",
      });

    return (
      <div className="bg-slate-900/95 text-white p-3 rounded-2xl text-[11px] shadow-2xl border border-slate-800 min-w-[150px] backdrop-blur-md animate-in fade-in zoom-in duration-200">
        <p className="font-black text-indigo-300 border-b border-slate-800 pb-1.5 mb-2 text-center tracking-tight uppercase">
          {label}
        </p>
        <div className="space-y-1.5">
          {/* ALWAYS SHOWN: PRICE */}
          <div className="flex justify-between gap-4">
            <span className="text-slate-400 font-bold">Price:</span>
            <span className="font-black text-white">
              ₹{Number(data.price || 0).toFixed(2)}
            </span>
          </div>

          {/* CONDITIONAL: 50 DMA */}
          {toggles?.showDMA50 && data.dmA50 && (
            <div className="flex justify-between gap-4 border-t border-slate-800/50 pt-1">
              <span className="text-amber-500 font-bold">50 DMA:</span>
              <span className="font-bold text-amber-200">
                ₹{Number(data.dmA50).toFixed(2)}
              </span>
            </div>
          )}

          {/* CONDITIONAL: 200 DMA */}
          {toggles?.showDMA200 && data.dmA200 && (
            <div className="flex justify-between gap-4 border-t border-slate-800/50 pt-1">
              <span className="text-slate-400 font-bold">200 DMA:</span>
              <span className="font-bold text-slate-300">
                ₹{Number(data.dmA200).toFixed(2)}
              </span>
            </div>
          )}

          {/* ALWAYS SHOWN: VOLUME */}
          <div className="flex justify-between gap-4 pt-1 border-t border-slate-800">
            <span className="text-slate-500 font-bold">Vol:</span>
            <span className="text-slate-300 font-bold">
              {data.volume >= 1000000
                ? `${(data.volume / 1000000).toFixed(2)}M`
                : (data.volume || 0).toLocaleString("en-IN")}
            </span>
          </div>
        </div>
      </div>
    );
  }
  return null;
};
