import React, { useState, useEffect } from "react";
import { 
  ComposedChart, Line, Bar, Area, XAxis, YAxis, 
  CartesianGrid, Tooltip, ResponsiveContainer, Label 
} from "recharts";
import { 
  Loader2, ArrowLeft, TrendingUp, TrendingDown, 
  Newspaper, ArrowUpRight, Eye, EyeOff 
} from "lucide-react";
import api from "../api/axios";
import FinancialTable from "./FinancialTable";

/**
 * StockDetailView
 * A professional financial dashboard view providing technical charts,
 * key ratios, latest news, and detailed financial statements.
 */
export default function StockDetailView({ symbol, onBack }) {
  const [data, setData] = useState(null);
  const [news, setNews] = useState([]);
  const [loading, setLoading] = useState(true);
  const [newsLoading, setNewsLoading] = useState(true);
  const [range, setRange] = useState("1y");

  // Multi-table visibility state for financial analysis
  const [visibleTables, setVisibleTables] = useState({
    quarters: true,
    pl: false,
    balance: false,
    cash: false
  });

  // Chart toggle states
  const [showPrice, setShowPrice] = useState(true);
  const [showDMA50, setShowDMA50] = useState(false);
  const [showDMA200, setShowDMA200] = useState(false);
  const [showVolumeAlways, setShowVolumeAlways] = useState(true);

  useEffect(() => {
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

  useEffect(() => {
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

  const toggleTable = (id) => {
    setVisibleTables(prev => ({ ...prev, [id]: !prev[id] }));
  };

  const formatNum = (val, decimals = 2) => {
    if (val === null || val === undefined || val === "N/A") return "N/A";
    const num = Number(val);
    return isNaN(num) ? "N/A" : num.toFixed(decimals);
  };

  const isUp = data?.ratios?.priceChange >= 0;
  const themeColor = isUp ? "#10b981" : "#f43f5e";

  const renderDateTick = (tickItem) => {
    const date = new Date(tickItem);
    const options = (range === "1m" || range === "3m") 
      ? { day: "2-digit", month: "short" } 
      : { month: "short", year: "2-digit" };
    return date.toLocaleDateString("en-IN", options);
  };

  if (loading && !data) return (
    <div className="flex h-screen items-center justify-center bg-slate-50">
      <Loader2 className="animate-spin text-indigo-600" />
    </div>
  );

  return (
    <div className="max-w-7xl mx-auto p-4 space-y-8 bg-slate-50 min-h-screen font-sans pb-24">
      
      {/* HEADER SECTION */}
      <div className="flex justify-between items-center bg-white p-8 rounded-3xl shadow-sm border border-slate-100">
        <div className="flex items-center gap-4">
          <button onClick={onBack} className="p-2 hover:bg-slate-100 rounded-full transition-colors">
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="text-3xl font-black text-slate-900 leading-tight">{data?.symbol}</h1>
            <p className="text-slate-400 text-sm font-medium tracking-tight">
              NSE Index • Updated {new Date(data?.lastUpdate).toLocaleTimeString()}
            </p>
          </div>
        </div>
        <div className="text-right">
          <div className={`text-4xl font-black ${isUp ? "text-emerald-600" : "text-rose-600"}`}>
            ₹ {formatNum(data?.ratios?.currentPrice)}
          </div>
          <div className={`flex items-center justify-end gap-1 text-sm font-bold ${isUp ? "text-emerald-500" : "text-rose-500"}`}>
            {isUp ? <TrendingUp size={16} /> : <TrendingDown size={16} />}
            {isUp ? "+" : ""}{formatNum(data?.ratios?.priceChange)} ({formatNum(data?.ratios?.priceChangePercent)}%)
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* RATIOS SECTION */}
        <div className="lg:col-span-2 bg-white rounded-3xl p-8 shadow-sm border border-slate-100 grid grid-cols-1 md:grid-cols-2 gap-y-6 gap-x-12 content-start">
          <RatioItem label="Market Cap" value={data?.ratios?.marketCap} />
          <RatioItem label="Current Price" value={`₹ ${formatNum(data?.ratios?.currentPrice)}`} />
          <RatioItem label="52W High / Low" value={`₹ ${formatNum(data?.ratios?.high52W)} / ${formatNum(data?.ratios?.low52W)}`} />
          <RatioItem label="Stock P/E" value={data?.ratios?.stockPE} />
          <RatioItem label="Book Value" value={data?.ratios?.bookValue === "N/A" ? "N/A" : `₹ ${data?.ratios?.bookValue}`} />
          <RatioItem label="Dividend Yield" value={data?.ratios?.dividendYield} />
          <RatioItem label="ROCE" value={data?.ratios?.roce} />
          <RatioItem label="ROE" value={data?.ratios?.roe} />
          <RatioItem label="Face Value" value={data?.ratios?.faceValue} />
          <RatioItem label="Historical High" value={`₹ ${formatNum(data?.ratios?.historicalHigh)}`} />
        </div>

        {/* NEWS SECTION */}
        <div className="bg-white rounded-3xl shadow-sm border border-slate-100 flex flex-col overflow-hidden">
          <div className="p-6 border-b border-slate-50 flex items-center gap-2 bg-slate-50/50">
            <Newspaper className="text-indigo-600" size={18} />
            <h3 className="font-bold text-slate-800 text-sm">Latest News</h3>
          </div>
          <div className="overflow-y-auto max-h-[400px] divide-y divide-slate-50">
            {newsLoading ? (
              <div className="p-6 space-y-4 animate-pulse">
                {[1, 2, 3].map((i) => <div key={i} className="h-16 bg-slate-50 rounded-xl" />)}
              </div>
            ) : news.map((item, idx) => (
              <a key={idx} href={item.url} target="_blank" rel="noopener noreferrer" className="group block p-4 hover:bg-slate-50 transition-all">
                <div className="flex justify-between items-start gap-3">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-[9px] font-black text-indigo-600 uppercase">{item.source}</span>
                      <span className="text-[9px] text-slate-400 font-bold uppercase">{new Date(item.publishedAt).toLocaleDateString(undefined, { month: "short", day: "numeric" })}</span>
                    </div>
                    <h4 className="text-[12px] font-bold text-slate-700 leading-snug group-hover:text-indigo-600 transition-colors line-clamp-2">{item.title}</h4>
                  </div>
                  <ArrowUpRight size={14} className="text-slate-300 group-hover:text-indigo-600 shrink-0" />
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
            {["1m", "3m", "6m", "1y", "3y", "max"].map((f) => (
              <button key={f} onClick={() => setRange(f)} className={`px-4 py-1.5 rounded-lg text-xs font-black uppercase transition-all ${range === f ? "bg-white text-indigo-600 shadow-sm" : "text-slate-400 hover:text-slate-600"}`}>{f}</button>
            ))}
          </div>
          <div className="flex gap-2 bg-slate-50 p-1 rounded-xl border border-slate-100">
            <ToggleButton label="Volume" active={showVolumeAlways} onClick={() => setShowVolumeAlways(!showVolumeAlways)} color="#475569" />
            <ToggleButton label="50 DMA" active={showDMA50} onClick={() => setShowDMA50(!showDMA50)} color="#f59e0b" />
            <ToggleButton label="200 DMA" active={showDMA200} onClick={() => setShowDMA200(!showDMA200)} color="#64748b" />
          </div>
        </div>

        <div className="h-[280px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data?.chartData} margin={{ left: 10, right: 45, bottom: 10, top: 10 }}>
              <defs>
                <linearGradient id="colorTrend" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={themeColor} stopOpacity={0.15} />
                  <stop offset="95%" stopColor={themeColor} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
              <XAxis dataKey="date" tickFormatter={renderDateTick} minTickGap={40} tick={{ fontSize: 10, fontWeight: 600, fill: "#94a3b8" }} axisLine={false} tickLine={false} />
              <YAxis yAxisId="vol" orientation="left" domain={[0, (dataMax) => dataMax * 1.1]} tick={{ fontSize: 9, fill: "#94a3b8", fontWeight: 600 }} axisLine={false} tickLine={false} tickFormatter={(val) => val >= 1000000 ? `${(val/1000000).toFixed(1)}M` : val}>
                <Label value="Volume" angle={-90} position="insideLeft" style={{ textAnchor: "middle", fill: "#94a3b8", fontSize: 10, fontWeight: 700 }} offset={5} />
              </YAxis>
              <YAxis yAxisId="price" orientation="right" domain={["auto", "auto"]} tickCount={6} tick={{ fontSize: 10, fill: "#64748b", fontWeight: 700 }} axisLine={false} tickLine={false}>
                <Label value="Price (₹)" angle={90} position="right" style={{ textAnchor: "middle", fill: "#64748b", fontSize: 11, fontWeight: 700 }} offset={30} />
              </YAxis>
              <Tooltip content={<CustomTooltip />} cursor={{ stroke: "#94a3b8", strokeWidth: 1, strokeDasharray: "5 5" }} />
              <Area yAxisId="price" type="monotone" dataKey="price" stroke="none" fill="url(#colorTrend)" connectNulls />
              {showVolumeAlways && <Bar yAxisId="vol" dataKey="volume" fill="#475569" opacity={0.3} barSize={range === "1m" ? 25 : 8} />}
              {showPrice && <Line yAxisId="price" type="monotone" dataKey="price" stroke={themeColor} strokeWidth={2.5} dot={false} activeDot={{ r: 4, strokeWidth: 0 }} />}
              {showDMA50 && <Line yAxisId="price" type="monotone" dataKey="dmA50" stroke="#f59e0b" strokeWidth={1.5} dot={false} connectNulls />}
              {showDMA200 && <Line yAxisId="price" type="monotone" dataKey="dmA200" stroke="#64748b" strokeWidth={1.5} dot={false} connectNulls />}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* FINANCIAL ANALYSIS SECTION */}
      <div className="space-y-6">
        <div className="flex items-end justify-between px-2">
          <div>
            <h2 className="text-2xl font-black text-slate-800 tracking-tight">Financial Analysis</h2>
            <p className="text-slate-400 text-[10px] font-bold uppercase tracking-widest mt-1">Detailed Statements in Cr.</p>
          </div>
        </div>
        
        {/* Toggle Controls */}
        <div className="flex flex-wrap bg-white p-2 rounded-2xl shadow-sm border border-slate-100 gap-2 w-fit">
          {[
            { id: 'quarters', label: 'Quarterly Results' },
            { id: 'pl', label: 'Profit & Loss' },
            { id: 'balance', label: 'Balance Sheet' },
            { id: 'cash', label: 'Cash Flow' }
          ].map(tab => (
            <button
              key={tab.id}
              onClick={() => toggleTable(tab.id)}
              className={`flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-black transition-all ${
                visibleTables[tab.id] 
                ? "bg-indigo-600 text-white shadow-lg shadow-indigo-200" 
                : "bg-slate-50 text-slate-400 hover:text-slate-600 hover:bg-slate-100 border border-transparent hover:border-slate-200"
              }`}
            >
              {visibleTables[tab.id] ? <Eye size={14} /> : <EyeOff size={14} />}
              {tab.label}
            </button>
          ))}
        </div>

        {/* Independent Tables - Rendered as standalone Cards */}
        <div className="space-y-12">
          {visibleTables.quarters && (
            <FinancialTable title="Quarterly Results" data={data?.quarterlyResults} />
          )}
          {visibleTables.pl && (
            <FinancialTable title="Annual Profit & Loss" data={data?.profitAndLoss} />
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

// Sub-components
const RatioItem = ({ label, value }) => {
  const displayValue = (typeof value === "number") ? value.toLocaleString("en-IN") : (value || "N/A");
  return (
    <div className="flex justify-between items-center border-b border-slate-50 pb-2">
      <span className="text-slate-400 text-sm font-medium">{label}</span>
      <span className="text-slate-900 font-bold text-sm tracking-tight">{displayValue}</span>
    </div>
  );
};

const ToggleButton = ({ label, active, onClick, color }) => (
  <button 
    onClick={onClick} 
    className={`px-3 py-1.5 rounded-lg text-[10px] font-bold border transition-all ${active ? "bg-white border-slate-200 shadow-sm" : "bg-transparent border-transparent text-slate-400"}`} 
    style={{ color: active ? color : undefined }}
  >
    {label}
  </button>
);

const CustomTooltip = ({ active, payload }) => {
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    return (
      <div className="bg-slate-900/95 text-white p-2 rounded-xl text-[10px] shadow-xl border border-slate-800 min-w-[110px] backdrop-blur-sm">
        <p className="font-bold text-slate-400 border-b border-slate-800 pb-0.5 mb-1 text-center">{data.date}</p>
        <div className="space-y-0.5">
          <div className="flex justify-between gap-3">
            <span className="text-slate-400">Price:</span>
            <span className="font-bold text-indigo-300">₹{Number(data.price || 0).toFixed(2)}</span>
          </div>
          {data.dmA50 && (
            <div className="flex justify-between gap-3">
              <span className="text-amber-400">50 DMA:</span>
              <span className="font-medium">₹{Number(data.dmA50).toFixed(2)}</span>
            </div>
          )}
          {data.dmA200 && (
            <div className="flex justify-between gap-3">
              <span className="text-slate-400">200 DMA:</span>
              <span className="font-medium">₹{Number(data.dmA200).toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between gap-3 pt-0.5 border-t border-slate-800">
            <span className="text-slate-400">Vol:</span>
            <span className="text-slate-300 font-medium">
              {data.volume >= 1000000 ? `${(data.volume / 1000000).toFixed(1)}M` : (data.volume || 0).toLocaleString()}
            </span>
          </div>
        </div>
      </div>
    );
  }
  return null;
};