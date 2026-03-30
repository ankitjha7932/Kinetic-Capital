import React, { useState, useMemo } from "react";
import api from "../api/axios";
import {
  Trash2,
  TrendingUp,
  TrendingDown,
  Activity,
  ChevronRight,
} from "lucide-react";

// --- PROFESSIONAL SPARKLINE WITH BASELINE ---
const Sparkline = ({ data, isPositive }) => {
  if (!data || data.length < 2) {
    return <div className="w-20 h-1 bg-slate-100 rounded-full mx-auto" />;
  }

  const width = 100;
  const height = 30;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;

  // Calculate the Y-coordinate for the baseline (the starting price of the 7D window)
  const baselineValue = data[0];
  const baselineY = height - ((baselineValue - min) / range) * height;

  const points = data
    .map((val, i) => {
      const x = (i / (data.length - 1)) * width;
      const y = height - ((val - min) / range) * height;
      return `${x},${y}`;
    })
    .join(" ");

  return (
    <svg width={width} height={height} className="overflow-visible mx-auto">
      {/* DASHED BASELINE REFERENCE */}
      <line
        x1="0"
        y1={baselineY}
        x2={width}
        y2={baselineY}
        stroke="#475569"
        strokeWidth="1.5"
        strokeDasharray="3,3"
      />

      {/* TREND LINE */}
      <path
        d={`M ${points}`}
        fill="none"
        stroke={isPositive ? "#10b981" : "#f43f5e"}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="drop-shadow-sm"
      />
    </svg>
  );
};

const getCapStyles = (label) => {
  switch (label?.toUpperCase()) {
    case "LARGE-CAP":
      return "bg-indigo-50 text-indigo-600 border-indigo-100";
    case "MID-CAP":
      return "bg-emerald-50 text-emerald-600 border-emerald-100";
    case "SMALL-CAP":
      return "bg-amber-50 text-amber-600 border-amber-100";
    case "MICRO-CAP":
      return "bg-rose-50 text-rose-600 border-rose-100";
    default:
      return "bg-slate-50 text-slate-400 border-slate-100";
  }
};

export default function PositionsList({ positions, onRefresh, onSelectStock }) {
  const [filter, setFilter] = useState("ALL");
  const [viewMode, setViewMode] = useState("returns");

  const filteredPositions = useMemo(() => {
    return filter === "ALL"
      ? positions
      : positions.filter((p) => p.marketCapLabel === filter);
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

  return (
    <div className="w-full mt-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
      {/* --- HEADER & TOGGLES --- */}
      <div className="flex flex-col lg:flex-row justify-between items-end lg:items-center mb-8 px-2 gap-4">
        <div>
          <h2 className="text-3xl font-black text-slate-900 tracking-tighter mb-1">
            Positions
          </h2>
          <p className="text-slate-400 text-xs font-medium uppercase tracking-widest">
            Monitoring {positions?.length || 0} active investments
          </p>
        </div>

        <div className="flex bg-slate-100 p-1 rounded-2xl border border-slate-200">
          {["returns", "price", "profit"].map((m) => (
            <button
              key={m}
              onClick={() => setViewMode(m)}
              className={`px-6 py-2 rounded-xl text-[10px] font-black uppercase tracking-tighter transition-all ${
                viewMode === m
                  ? "bg-white text-indigo-600 shadow-sm"
                  : "text-slate-500 hover:text-slate-800"
              }`}
            >
              {m === "returns"
                ? "Returns %"
                : m === "price"
                  ? "Spot Price"
                  : "Net P&L"}
            </button>
          ))}
        </div>
      </div>

      {/* --- MARKET CAP FILTERS --- */}
      <div className="flex gap-2 mb-6 px-2 overflow-x-auto no-scrollbar">
        {["ALL", "LARGE-CAP", "MID-CAP", "SMALL-CAP", "MICRO-CAP"].map(
          (cap) => (
            <button
              key={cap}
              onClick={() => setFilter(cap)}
              className={`px-4 py-2 rounded-xl text-[9px] font-black uppercase tracking-widest border transition-all whitespace-nowrap ${
                filter === cap
                  ? "bg-slate-900 text-white border-slate-900 shadow-md"
                  : "bg-white text-slate-400 border-slate-100 hover:border-slate-300"
              }`}
            >
              {cap.replace("-CAP", "")}
            </button>
          ),
        )}
      </div>

      {/* --- DATA TABLE --- */}
      <div className="bg-white rounded-[2.5rem] border border-slate-100 shadow-xl shadow-slate-200/40 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <thead>
              <tr className="bg-slate-50/50 border-b border-slate-100">
                <th className="pl-10 pr-6 py-6 text-left text-[10px] font-black text-slate-400 uppercase tracking-widest">
                  Asset Details
                </th>
                <th className="px-6 py-6 text-center text-[10px] font-black text-slate-400 uppercase tracking-widest">
                  7D Trend (Rel. to Start)
                </th>
                <th className="px-6 py-6 text-right text-[10px] font-black text-slate-400 uppercase tracking-widest">
                  Holdings
                </th>
                <th className="px-6 py-6 text-right text-[10px] font-black text-slate-400 uppercase tracking-widest">
                  {viewMode === "returns"
                    ? "Returns"
                    : viewMode === "price"
                      ? "Current Price"
                      : "Unrealized P&L"}
                </th>
                <th className="pl-6 pr-10 py-6 text-right text-[10px] font-black text-slate-400 uppercase tracking-widest">
                  Action
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {filteredPositions?.map((pos) => {
                const isProfit = pos.pnlPercent >= 0;
                const totalPnlValue =
                  (pos.currentPrice - pos.avgBuyPrice) * pos.quantity;

                return (
                  <tr
                    key={pos.holdingId}
                    onClick={() => onSelectStock(pos.symbol)}
                    className="group hover:bg-slate-50/80 transition-all cursor-pointer"
                  >
                    {/* ASSET DETAILS */}
                    <td className="pl-10 pr-6 py-6">
                      <div className="flex flex-col">
                        {/* 🚀 UPDATED: High-contrast, tight-tracking, bold symbol */}
                        <span className="font-black text-slate-900 text-xl tracking-tighter uppercase group-hover:text-indigo-600 transition-colors">
                          {pos.symbol.split(".")[0]}
                        </span>

                        <div className="flex items-center gap-2 mt-1">
                          <span
                            className={`px-2 py-0.5 rounded-md border text-[8px] font-black uppercase tracking-widest ${getCapStyles(pos.marketCapLabel)}`}
                          >
                            {pos.marketCapLabel || "Equity"}
                          </span>
                          <span className="text-[9px] font-bold text-slate-400 uppercase tracking-tight italic">
                            {pos.action.replace("_", " ")}
                          </span>
                        </div>
                      </div>
                    </td>

                    {/* SPARKLINE WITH BASELINE */}
                    <td className="px-6 py-6 text-center">
                      <Sparkline data={pos.history} isPositive={isProfit} />
                    </td>

                    {/* HOLDINGS INFO */}
                    <td className="px-6 py-6 text-right">
                      <div className="flex flex-col">
                        <span className="font-bold text-slate-700 tabular-nums text-sm">
                          ₹{pos.avgBuyPrice?.toLocaleString()}
                        </span>
                        <span className="text-[10px] font-black text-slate-300 uppercase tracking-tighter">
                          Qty: {pos.quantity}
                        </span>
                      </div>
                    </td>

                    {/* DYNAMIC VIEW COLUMN */}
                    <td className="px-6 py-6 text-right">
                      <div className="flex justify-end items-center h-full">
                        {viewMode === "price" && (
                          <span className="font-black text-slate-800 tabular-nums text-base tracking-tighter">
                            ₹{pos.currentPrice?.toLocaleString()}
                          </span>
                        )}

                        {viewMode === "returns" && (
                          <div
                            className={`flex items-center gap-1 font-black px-4 py-1.5 rounded-2xl border-2 shadow-sm ${
                              isProfit
                                ? "bg-emerald-50 text-emerald-600 border-emerald-100"
                                : "bg-rose-50 text-rose-600 border-rose-100"
                            }`}
                          >
                            {isProfit ? (
                              <TrendingUp size={12} />
                            ) : (
                              <TrendingDown size={12} />
                            )}
                            <span className="text-xs tabular-nums">
                              {isProfit ? "+" : ""}
                              {pos.pnlPercent?.toFixed(2)}%
                            </span>
                          </div>
                        )}

                        {viewMode === "profit" && (
                          <div
                            className={`flex flex-col items-end tabular-nums font-black ${isProfit ? "text-emerald-500" : "text-rose-500"}`}
                          >
                            <span className="text-base tracking-tighter">
                              ₹{Math.abs(totalPnlValue).toLocaleString()}
                            </span>
                            <span className="text-[8px] opacity-60 uppercase tracking-widest">
                              Net Gain
                            </span>
                          </div>
                        )}
                      </div>
                    </td>

                    {/* ACTIONS */}
                    <td className="pl-6 pr-10 py-6 text-right">
                      <div className="flex items-center justify-end gap-3 opacity-20 group-hover:opacity-100 transition-opacity">
                        <button
                          onClick={(e) => handleDelete(pos.holdingId, e)}
                          className="p-2 text-slate-400 hover:text-rose-500 hover:bg-rose-50 rounded-xl transition-all"
                        >
                          <Trash2 size={16} />
                        </button>
                        <ChevronRight
                          size={16}
                          className="text-slate-300 group-hover:text-indigo-400 transition-all"
                        />
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
