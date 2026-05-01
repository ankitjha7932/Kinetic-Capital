import React, { useState, useMemo } from "react";
import api from "../api/axios";
import {
  Trash2,
  TrendingUp,
  TrendingDown,
  ChevronRight,
} from "lucide-react";

// --- LOGO LOGIC ---
const getLogo = (sym) =>
  `https://assets-netstorage.groww.in/stock-assets/logos2/${sym.replace(".NS", "").toUpperCase()}.webp`;

const LogoAvatar = ({ symbol }) => {
  const [failed, setFailed] = useState(false);
  const ticker = symbol.replace(".NS", "").toUpperCase();

  if (failed) {
    return (
      <div className="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center text-[10px] font-black shrink-0 shadow-sm">
        {ticker.slice(0, 3)}
      </div>
    );
  }

  return (
    <div className="w-10 h-10 rounded-xl bg-slate-50 border border-slate-100 flex items-center justify-center overflow-hidden shrink-0 shadow-sm group-hover:border-indigo-100 transition-colors">
      <img
        src={getLogo(symbol)}
        alt={ticker}
        className="w-7 h-7 object-contain"
        onError={() => setFailed(true)}
      />
    </div>
  );
};

// --- PROFESSIONAL SPARKLINE ---
const Sparkline = ({ data, isPositive }) => {
  if (!data || data.length < 2) {
    return <div className="w-20 h-1 bg-slate-100 rounded-full mx-auto" />;
  }
  const width = 100;
  const height = 30;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const baselineValue = data[0];
  const baselineY = height - ((baselineValue - min) / range) * height;

  const points = data
    .map((val, i) => {
      const x = (i / (data.length - 1)) * width;
      const y = height - ((val - min) / range) * height;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(" ");

  return (
    <svg width={width} height={height} className="overflow-visible mx-auto">
      <line x1="0" y1={baselineY} x2={width} y2={baselineY} stroke="#e2e8f0" strokeWidth="1" strokeDasharray="2,2" />
      <path
        d={`M ${points}`}
        fill="none"
        stroke={isPositive ? "#10b981" : "#f43f5e"}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
};

const getCapStyles = (label) => {
  switch (label?.toUpperCase()) {
    case "LARGE-CAP": return "bg-indigo-50 text-indigo-600 border-indigo-100";
    case "MID-CAP": return "bg-emerald-50 text-emerald-600 border-emerald-100";
    case "SMALL-CAP": return "bg-amber-50 text-amber-600 border-amber-100";
    case "MICRO-CAP": return "bg-rose-50 text-rose-600 border-rose-100";
    default: return "bg-slate-50 text-slate-400 border-slate-100";
  }
};

export default function PositionsList({ positions, onRefresh, onSelectStock }) {
  const [filter, setFilter] = useState("ALL");
  const [viewMode, setViewMode] = useState("returns");

  const filteredPositions = useMemo(() => {
    return filter === "ALL" ? positions : positions.filter((p) => p.marketCapLabel === filter);
  }, [filter, positions]);

  const handleDelete = async (id, e) => {
    e.stopPropagation();
    if (window.confirm("Remove this holding?")) {
      try {
        await api.delete(`/portfolio/holding/${id}`);
        onRefresh();
      } catch (err) {
        console.error("Delete error:", err);
      }
    }
  };

  const formatCurrency = (val) => {
    return val?.toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  };

  return (
    <div className="w-full mt-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
      <div className="flex justify-between items-center mb-8 px-2">
        <div>
          <h2 className="text-3xl font-black text-slate-900 tracking-tighter mb-1">Positions</h2>
          <p className="text-slate-400 text-[10px] font-black uppercase tracking-[0.2em]">Monitoring {positions?.length || 0} active assets</p>
        </div>
        
        <div className="flex gap-1.5 overflow-x-auto no-scrollbar">
          {["ALL", "LARGE", "MID", "SMALL", "MICRO"].map((cap) => (
            <button
              key={cap}
              onClick={() => setFilter(cap === "ALL" ? "ALL" : `${cap}-CAP`)}
              className={`px-3 py-1.5 rounded-lg text-[9px] font-black uppercase tracking-widest border transition-all ${
                filter.includes(cap) || (cap === "ALL" && filter === "ALL")
                  ? "bg-slate-900 text-white border-slate-900 shadow-md"
                  : "bg-white text-slate-400 border-slate-100 hover:border-slate-200"
              }`}
            >
              {cap}
            </button>
          ))}
        </div>
      </div>

      <div className="bg-white rounded-[2.5rem] border border-slate-100 shadow-2xl shadow-slate-200/50 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <thead>
              <tr className="bg-slate-50/50 border-b border-slate-100">
                <th className="pl-10 pr-6 py-6 text-left text-[10px] font-black text-slate-400 uppercase tracking-widest">Asset Details</th>
                <th className="px-6 py-6 text-center text-[10px] font-black text-slate-400 uppercase tracking-widest">7D Trend</th>
                <th className="px-6 py-6 text-right text-[10px] font-black text-slate-400 uppercase tracking-widest">Holdings</th>
                <th className="px-6 py-6 text-right">
                    <div className="flex justify-end bg-slate-200/50 p-1 rounded-xl border border-slate-200 w-fit ml-auto">
                        {[
                            { id: "returns", label: "Returns %" },
                            { id: "price", label: "Spot" },
                            { id: "profit", label: "P&L" }
                        ].map((m) => (
                            <button
                                key={m.id}
                                onClick={(e) => { e.stopPropagation(); setViewMode(m.id); }}
                                className={`px-3 py-1 rounded-lg text-[8px] font-black uppercase tracking-tighter transition-all ${
                                    viewMode === m.id ? "bg-white text-indigo-600 shadow-sm" : "text-slate-500"
                                }`}
                            >
                                {m.label}
                            </button>
                        ))}
                    </div>
                </th>
                <th className="pl-6 pr-10 py-6 text-right text-[10px] font-black text-slate-400 uppercase tracking-widest">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {filteredPositions?.map((pos) => {
                const isProfit = pos.pnlPercent >= 0;
                const totalPnlValue = (pos.currentPrice - pos.avgBuyPrice) * pos.quantity;

                return (
                  <tr
                    key={pos.holdingId}
                    onClick={() => onSelectStock(pos.symbol)}
                    className="group hover:bg-slate-50/80 transition-all cursor-pointer"
                  >
                    {/* ASSET DETAILS WITH LOGO */}
                    <td className="pl-10 pr-6 py-6">
                      <div className="flex items-center gap-4">
                        <LogoAvatar symbol={pos.symbol} />
                        <div className="flex flex-col">
                          <span className="font-black text-slate-900 text-xl tracking-tighter uppercase group-hover:text-indigo-600 transition-colors">
                            {pos.symbol.split(".")[0]}
                          </span>
                          <div className="flex items-center gap-2 mt-1">
                            <span className={`px-2 py-0.5 rounded-md border text-[8px] font-black uppercase tracking-widest ${getCapStyles(pos.marketCapLabel)}`}>
                              {pos.marketCapLabel || "Equity"}
                            </span>
                            <span className="text-[9px] font-bold text-slate-400 uppercase tracking-tight italic">{pos.action.replace("_", " ")}</span>
                          </div>
                        </div>
                      </div>
                    </td>

                    <td className="px-6 py-6 text-center">
                      <Sparkline data={pos.history} isPositive={isProfit} />
                    </td>

                    <td className="px-6 py-6 text-right">
                      <div className="flex flex-col">
                        <span className="font-bold text-slate-700 tabular-nums text-sm">₹{formatCurrency(pos.avgBuyPrice)}</span>
                        <span className="text-[10px] font-black text-slate-300 uppercase tracking-tighter">Qty: {pos.quantity}</span>
                      </div>
                    </td>

                    <td className="px-6 py-6 text-right">
                      <div className="flex justify-end items-center">
                        {viewMode === "price" && (
                          <span className="font-black text-slate-800 tabular-nums text-lg tracking-tighter">₹{formatCurrency(pos.currentPrice)}</span>
                        )}

                        {viewMode === "returns" && (
                          <div className={`flex items-center gap-1 font-black px-4 py-1.5 rounded-2xl border-2 shadow-sm ${isProfit ? "bg-emerald-50 text-emerald-600 border-emerald-100" : "bg-rose-50 text-rose-600 border-rose-100"}`}>
                            {isProfit ? <TrendingUp size={12} /> : <TrendingDown size={12} />}
                            <span className="text-xs tabular-nums">{isProfit ? "+" : ""}{pos.pnlPercent?.toFixed(2)}%</span>
                          </div>
                        )}

                        {viewMode === "profit" && (
                          <div className={`flex flex-col items-end tabular-nums font-black ${isProfit ? "text-emerald-500" : "text-rose-500"}`}>
                            <span className="text-lg tracking-tighter">₹{formatCurrency(Math.abs(totalPnlValue))}</span>
                            <span className="text-[8px] opacity-60 uppercase tracking-widest">
                                {isProfit ? "Net Gain" : "Net Loss"}
                            </span>
                          </div>
                        )}
                      </div>
                    </td>

                    <td className="pl-6 pr-10 py-6 text-right">
                      <div className="flex items-center justify-end gap-3 opacity-20 group-hover:opacity-100 transition-opacity">
                        <button
                          onClick={(e) => handleDelete(pos.holdingId, e)}
                          className="p-2 text-slate-400 hover:text-rose-500 hover:bg-rose-50 rounded-xl transition-all"
                        >
                          <Trash2 size={16} />
                        </button>
                        <ChevronRight size={16} className="text-slate-300 group-hover:text-indigo-400 transition-all" />
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}