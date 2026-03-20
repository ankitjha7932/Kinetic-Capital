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
  ArrowUpRight,
  Eye,
  EyeOff,
  Zap,
} from "lucide-react";
import api from "../api/axios";
import FinancialTable from "./FinancialTable";

export default function StockDetailView() {
  const { symbol } = useParams();
  const navigate = useNavigate();

  const [data, setData] = useState(null);
  const [analysis, setAnalysis] = useState(null);
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

  const [showDMA50, setShowDMA50] = useState(false);
  const [showDMA200, setShowDMA200] = useState(false);
  const [showVolumeAlways, setShowVolumeAlways] = useState(true);

  useEffect(() => {
    if (!symbol || symbol === "undefined") return;
    const fetchData = async () => {
      try {
        setLoading(true);
        const [analRes, newsRes, detRes] = await Promise.all([
          api.get(`/stocks/analyze/${symbol}`),
          api.get(`/portfolio/news/${symbol}`),
          api.get(`/stocks/details/${symbol}?range=${range}`),
        ]);
        setAnalysis(analRes.data);
        setNews(newsRes.data.slice(0, 7));
        setData(detRes.data);
      } catch (err) {
        console.error("Fetch error:", err);
      } finally {
        setLoading(false);
        setNewsLoading(false);
      }
    };
    fetchData();
  }, [symbol, range]);

  const toggleTable = (id) =>
    setVisibleTables((prev) => ({ ...prev, [id]: !prev[id] }));

  const formatNum = (val, decimals = 2) => {
    if (val === null || val === undefined || val === "N/A") return "N/A";
    return Number(val).toLocaleString("en-IN", {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
  };

  // 2. HELPER: Proper markings for Volume Y-Axis (Left)
  const formatVolumeLabel = (val) => {
    if (val >= 10000000) return `${(val / 10000000).toFixed(1)}Cr`;
    if (val >= 1000000) return `${(val / 1000000).toFixed(1)}M`;
    if (val >= 1000) return `${(val / 1000).toFixed(0)}K`;
    return val;
  };

  const isUp = data?.ratios?.priceChange >= 0;
  const themeColor = isUp ? "#10b981" : "#f43f5e";

  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    if (range === "1d")
      return date.toLocaleTimeString("en-IN", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      });
    if (range === "1w" || range === "1m")
      return date.toLocaleDateString("en-IN", {
        day: "2-digit",
        month: "short",
      });
    return date.toLocaleDateString("en-IN", {
      month: "short",
      year: "2-digit",
    });
  };

  if (loading && !data)
    return (
      <div className="flex h-screen items-center justify-center bg-slate-50">
        <Loader2 className="animate-spin text-indigo-600" size={40} />
      </div>
    );

  return (
    <div className="max-w-7xl mx-auto p-4 space-y-6 bg-slate-50 min-h-screen font-sans relative">
      {/* HEADER SECTION */}
      <div className="flex justify-between items-center bg-white p-5 rounded-2xl shadow-sm border border-slate-100">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate("/")}
            className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          >
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="text-2xl font-black text-slate-900 leading-tight flex items-center gap-3">
              {data?.symbol || symbol}
              <div
                className={`px-3 py-1 rounded-full border text-[10px] font-black uppercase tracking-wider ${isUp ? "bg-emerald-50 text-emerald-700 border-emerald-100" : "bg-rose-50 text-rose-700 border-rose-100"}`}
              >
                {analysis?.sentiment || "Analyzing..."}
              </div>
            </h1>
            <button
              onClick={() => navigate(`/strategy/${symbol}`)}
              className="mt-3 flex items-center gap-2 px-5 py-2.5 bg-indigo-600 text-white rounded-xl font-black text-[10px] shadow-lg shadow-indigo-100 hover:scale-105 transition-all group uppercase tracking-widest"
            >
              <Zap size={14} className="fill-white group-hover:animate-pulse" />
              Open Strategic Command
            </button>
          </div>
        </div>
        <div className="text-right">
          <div
            className={`text-4xl font-black ${isUp ? "text-emerald-600" : "text-rose-600"} tracking-tighter`}
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

      {/* RATIOS & NEWS GRID */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 bg-white rounded-2xl p-7 shadow-sm border border-slate-100 grid grid-cols-1 md:grid-cols-2 gap-y-5 gap-x-12 content-start">
          <RatioItem label="Market Cap" value={`${data?.ratios?.marketCap}`} />
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
            value={`${data?.ratios?.dividendYield}%`}
          />
          <RatioItem
            label="ROCE / ROE"
            value={`${data?.ratios?.roce}% / ${data?.ratios?.roe}%`}
          />
          <RatioItem
            label="Historical High"
            value={`₹ ${formatNum(data?.ratios?.historicalHigh)}`}
          />
          <RatioItem label="Face Value" value={data?.ratios?.faceValue} />
        </div>

        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 flex flex-col overflow-hidden max-h-[300px]">
          <div className="p-4 border-b border-slate-50 flex items-center gap-2 bg-slate-50/50">
            <Newspaper className="text-indigo-600" size={18} />
            <h3 className="font-bold text-slate-800 text-sm">Latest News</h3>
          </div>
          <div className="overflow-y-auto divide-y divide-slate-50 no-scrollbar">
            {!newsLoading &&
              news.map((item, idx) => (
                <a
                  key={idx}
                  href={item.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="group block p-4 hover:bg-slate-50 transition-all"
                >
                  <span className="text-[9px] font-black text-indigo-600 uppercase">
                    {item.source}
                  </span>
                  <h4 className="text-[12px] font-bold text-slate-700 leading-snug group-hover:text-indigo-600 line-clamp-2 mt-1">
                    {item.title}
                  </h4>
                </a>
              ))}
          </div>
        </div>
      </div>

      {/* CHART SECTION */}
      <div className="bg-white rounded-3xl p-6 shadow-sm border border-slate-100">
        <div className="flex justify-between items-center mb-6">
          <div className="flex bg-slate-100 p-1 rounded-xl gap-1">
            {["1d", "1w", "1m", "3m", "6m", "1y", "3y", "max"].map((f) => (
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
          className="h-[450px] w-full"
          style={{ position: "relative", minHeight: "450px" }}
        >
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart
              data={data?.chartData}
              margin={{ left: 35, right: 10, bottom: 0, top: 10 }}
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
                minTickGap={50}
                tick={{ fontSize: 10, fontWeight: 600, fill: "#94a3b8" }}
                axisLine={false}
                tickLine={false}
              />

              {/* 2. VOLUME YAXIS (LEFT) WITH LABEL & PROPER MARKINGS */}
              <YAxis
                yAxisId="vol"
                orientation="left"
                domain={[0, (dataMax) => dataMax * 1.15]} // Squashes bars to bottom 25%
                tickFormatter={formatVolumeLabel}
                tick={{ fontSize: 10, fill: "#64748b", fontWeight: 700 }}
                axisLine={false}
                tickLine={false}
                label={{
                  value: "Volume",
                  angle: -90,
                  position: "insideLeft",
                  offset: -25,
                  fill: "#94a3b8",
                  fontSize: 10,
                  fontWeight: 800,
                }}
              />

              <YAxis
                yAxisId="price"
                orientation="right"
                domain={["auto", "auto"]}
                tickCount={8}
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

              {/* 1. VOLUME BARS (MORE WEIGHT & VISIBILITY) */}
              {showVolumeAlways && (
                <Bar
                  yAxisId="vol"
                  dataKey="volume"
                  fill="#6366f1" // Strong Indigo
                  opacity={0.7} // Higher weight
                  radius={[2, 2, 0, 0]}
                  barSize={range === "1d" ? 3 : 8}
                />
              )}

              <Line
                yAxisId="price"
                type="monotone"
                dataKey="price"
                stroke={themeColor}
                strokeWidth={2.5}
                dot={false}
                connectNulls={true}
                activeDot={{ r: 4, strokeWidth: 0 }}
              />

              {showDMA50 && (
                <Line
                  yAxisId="price"
                  type="monotone"
                  dataKey="dmA50"
                  stroke="#f59e0b"
                  strokeWidth={1.5}
                  dot={false}
                  connectNulls={true}
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
                  connectNulls={true}
                />
              )}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* FINANCIAL TABLES */}
      <div className="space-y-8 pt-10">
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
              className={`flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-black transition-all ${visibleTables[tab.id] ? "bg-indigo-600 text-white shadow-lg shadow-indigo-200" : "bg-slate-50 text-slate-400 hover:text-slate-600 border border-transparent"}`}
            >
              {visibleTables[tab.id] ? <Eye size={14} /> : <EyeOff size={14} />}{" "}
              {tab.label}
            </button>
          ))}
        </div>
        <div className="space-y-16">
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
  <div className="flex justify-between items-center border-b border-slate-50 pb-2.5">
    <span className="text-slate-400 text-xs font-medium">{label}</span>
    <span className="text-slate-900 font-bold text-xs tracking-tight">
      {value || "N/A"}
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

const CustomTooltip = ({ active, payload, range, toggles = {} }) => {
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    const dateObj = new Date(data.date);
    let label =
      range === "1d"
        ? dateObj.toLocaleTimeString("en-IN")
        : dateObj.toLocaleDateString("en-IN", {
            day: "2-digit",
            month: "short",
            year: "numeric",
          });
    return (
      <div className="bg-slate-900/95 text-white p-3 rounded-2xl text-[11px] shadow-2xl border border-slate-800 min-w-[170px] backdrop-blur-md">
        <p className="font-black text-indigo-300 border-b border-slate-800 pb-1.5 mb-2 text-center uppercase tracking-tight">
          {label}
        </p>
        <div className="space-y-1.5">
          <div className="flex justify-between gap-4">
            <span className="text-slate-400 font-bold">Price:</span>
            <span className="font-black text-white">
              ₹{Number(data.price || 0).toFixed(2)}
            </span>
          </div>
          {toggles?.showDMA50 && data.dmA50 && (
            <div className="flex justify-between gap-4">
              <span className="text-amber-500 font-bold">50 DMA:</span>
              <span className="font-black text-amber-200">
                ₹{Number(data.dmA50).toFixed(2)}
              </span>
            </div>
          )}
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
