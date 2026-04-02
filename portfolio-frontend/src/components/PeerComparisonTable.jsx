import React, { useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { PieChart, Pie, Cell, ResponsiveContainer } from "recharts";
import {
  Layers,
  ChevronUp,
  ChevronDown,
  PieChart as PieIcon,
  Activity,
} from "lucide-react";

const COLORS = [
  "#6366f1",
  "#8b5cf6",
  "#ec4899",
  "#f43f5e",
  "#f59e0b",
  "#10b981",
  "#0ea5e9",
];

export default function PeerComparisonTable({ data }) {
  const navigate = useNavigate();
  const [sortConfig, setSortConfig] = useState({
    key: "marketCap",
    direction: "desc",
  });
  const [activeSlice, setActiveSlice] = useState(null);

  if (!data || !data.peers || data.peers.length === 0) return null;

  // --- HELPERS ---
  const parseValue = (val) => {
    if (!val || val === "—") return 0;
    const cleanVal = val.toString().replace(/[₹%,]/g, "");
    const num = parseFloat(cleanVal);
    return isNaN(num) ? 0 : num;
  };

  const formatIndian = (val, isCurrency = false) => {
    if (!val || val === "—") return "—";
    const cleanVal = val.toString().replace(/[₹%,]/g, "");
    const num = parseFloat(cleanVal);
    if (isNaN(num)) return val;

    const formatted = new Intl.NumberFormat("en-IN", {
      maximumFractionDigits: 2,
      minimumFractionDigits: 2,
    }).format(num);

    const suffix = val.toString().includes("%") ? "%" : "";
    return isCurrency ? `₹${formatted}` : `${formatted}${suffix}`;
  };

  const sortedPeers = useMemo(() => {
    let sortableItems = [...data.peers];
    sortableItems.sort((a, b) => {
      const aVal = parseValue(a[sortConfig.key]);
      const bVal = parseValue(b[sortConfig.key]);
      return sortConfig.direction === "asc" ? aVal - bVal : bVal - aVal;
    });
    return sortableItems;
  }, [data.peers, sortConfig]);

  const pieData = useMemo(() => {
    return sortedPeers
      .map((p, index) => ({
        name: p.name,
        value: Math.abs(parseValue(p[sortConfig.key])),
        symbol: p.symbol,
        fill: COLORS[index % COLORS.length],
      }))
      .filter((p) => p.value > 0);
  }, [sortedPeers, sortConfig.key]);

  const displayData = activeSlice || (pieData.length > 0 ? pieData[0] : null);

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 xl:grid-cols-7 gap-6">
        {/* --- LEFT: DONUT ANALYSIS & LEGEND --- */}
        <div className="xl:col-span-2 bg-white rounded-[2rem] p-6 shadow-sm border border-slate-100 flex flex-col items-center min-h-[600px]">
          <div className="w-full flex items-center gap-2 mb-2">
            <PieIcon size={14} className="text-indigo-500" />
            <h3 className="text-[9px] font-black text-slate-400 uppercase tracking-widest">
              Benchmarking Distribution
            </h3>
          </div>

          <div className="flex-1 w-full relative min-h-[320px]">
            <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none z-10 translate-y-2">
              <span className="text-[9px] font-black text-slate-400 uppercase tracking-[0.2em] mb-1">
                {sortConfig.key.replace(/([A-Z])/g, " $1").trim()}
              </span>
              <span className="text-3xl font-black text-slate-900 tabular-nums tracking-tight">
                {displayData
                  ? formatIndian(
                      displayData.value,
                      sortConfig.key === "marketCap",
                    )
                  : "0.00"}
              </span>
              <span className="text-[8px] font-black text-indigo-500 uppercase mt-2 px-3 py-1 bg-indigo-50 rounded-full border border-indigo-100 max-w-[160px] truncate">
                {displayData?.name || "Select Peer"}
              </span>
            </div>

            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={pieData}
                  innerRadius={95}
                  outerRadius={130}
                  paddingAngle={4}
                  dataKey="value"
                  stroke="none"
                  onMouseEnter={(_, index) => setActiveSlice(pieData[index])}
                  onMouseLeave={() => setActiveSlice(null)}
                >
                  {pieData.map((entry, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={entry.fill}
                      className="cursor-pointer outline-none transition-all duration-300"
                      opacity={
                        activeSlice && activeSlice.name !== entry.name ? 0.3 : 1
                      }
                    />
                  ))}
                </Pie>
              </PieChart>
            </ResponsiveContainer>
          </div>

          {/* DYNAMIC LEGEND BELOW PIE */}
          <div className="w-full mt-6 pt-6 border-t border-slate-50">
            <div className="grid grid-cols-2 gap-x-4 gap-y-3">
              {pieData.map((entry) => (
                <div
                  key={entry.name}
                  onMouseEnter={() => setActiveSlice(entry)}
                  onMouseLeave={() => setActiveSlice(null)}
                  className={`flex items-center gap-2.5 p-1.5 rounded-xl transition-all duration-300 cursor-default ${
                    displayData?.name === entry.name
                      ? "bg-slate-50 translate-x-1"
                      : "opacity-60 hover:opacity-100"
                  }`}
                >
                  <div
                    className="w-2.5 h-2.5 rounded-full shrink-0 shadow-sm"
                    style={{ backgroundColor: entry.fill }}
                  />
                  <span className="text-[10px] font-black text-slate-900 truncate uppercase tracking-tighter">
                    {entry.name}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* --- RIGHT: DATA TERMINAL (STICKY & BOLD) --- */}
        <div className="xl:col-span-5 bg-white rounded-[2rem] shadow-sm border border-slate-100 overflow-hidden flex flex-col">
          <div className="p-8 flex justify-between items-center border-b border-slate-50">
            <div className="flex items-center gap-4">
              <div className="p-3 bg-slate-900 rounded-2xl text-white shadow-lg">
                <Activity size={18} />
              </div>
              <div>
                <h2 className="text-xl font-black text-slate-900 tracking-tight leading-none">
                  Peer Intelligence
                </h2>
                <div className="mt-2">
                  <span className="text-[10px] font-black text-white bg-indigo-600 px-3 py-1 rounded-full uppercase tracking-wider">
                    {data.industry}
                  </span>
                </div>
              </div>
            </div>
            <div className="text-right">
              <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest bg-slate-50 px-2 py-1 rounded">
                Values in ₹ Crores
              </span>
            </div>
          </div>

          <div className="p-6 overflow-x-auto flex-1 scrollbar-thin scrollbar-thumb-slate-200">
            <div className="overflow-hidden rounded-[1.5rem] border border-slate-100 shadow-sm min-w-[1000px]">
              <table className="w-full table-fixed border-separate border-spacing-0">
                <thead>
                  <tr className="bg-[#1e293b] text-white uppercase text-[9px] font-black tracking-widest whitespace-nowrap">
                    {/* STICKY HEADER CELL */}
                    <th className="w-[18%] p-5 text-left sticky left-0 z-30 bg-[#1e293b] rounded-tl-[1.5rem] shadow-[2px_0_5px_rgba(0,0,0,0.1)]">
                      Company
                    </th>
                    <SortHeader
                      label="P/E"
                      sKey="pe"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="Mar Cap"
                      sKey="marketCap"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="Div Yld"
                      sKey="divYield"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="NP Qtr"
                      sKey="netProfitQtr"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="Profit Var"
                      sKey="profitVarQtr"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="Sales Qtr"
                      sKey="salesQtr"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="Sales Var"
                      sKey="salesVarQtr"
                      config={sortConfig}
                      setConfig={setSortConfig}
                    />
                    <SortHeader
                      label="ROCE"
                      sKey="roce"
                      config={sortConfig}
                      setConfig={setSortConfig}
                      isLast={true}
                    />
                  </tr>
                </thead>
                <tbody className="text-[13px] font-black text-slate-900">
                  {sortedPeers.map((peer) => {
                    const isProfitNeg = peer.profitVarQtr
                      ?.toString()
                      .startsWith("-");
                    const isSalesNeg = peer.salesVarQtr
                      ?.toString()
                      .startsWith("-");

                    return (
                      <tr key={peer.name} className="group transition-colors">
                        {/* STICKY BODY CELL - OPAQUE BG */}
                        <td
                          className={`p-5 whitespace-nowrap sticky left-0 z-20 border-b border-slate-50 shadow-[2px_0_5px_rgba(0,0,0,0.05)] transition-colors
                          ${peer.isCurrent ? "bg-[#f5f7ff]" : "bg-white group-hover:bg-[#f8fafc]"}`}
                        >
                          <button
                            onClick={() =>
                              peer.symbol && navigate(`/stock/${peer.symbol}`)
                            }
                            className="text-indigo-600 hover:underline text-left font-black"
                          >
                            {peer.name}
                          </button>
                        </td>
                        <td className="p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80">
                          {peer.pe}
                        </td>
                        <td className="p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80">
                          {formatIndian(peer.marketCap, true)}
                        </td>
                        <td className="p-5 text-center font-black text-slate-500 border-b border-slate-50 bg-white group-hover:bg-slate-50/80">
                          {peer.divYield}
                        </td>
                        <td className="p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80">
                          {formatIndian(peer.netProfitQtr)}
                        </td>
                        <td
                          className={`p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80 ${isProfitNeg ? "text-rose-600" : "text-emerald-600"}`}
                        >
                          {peer.profitVarQtr}
                        </td>
                        <td className="p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80">
                          {formatIndian(peer.salesQtr)}
                        </td>
                        <td
                          className={`p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80 ${isSalesNeg ? "text-rose-600" : "text-emerald-600"}`}
                        >
                          {peer.salesVarQtr}
                        </td>
                        <td className="p-5 text-center font-black border-b border-slate-50 bg-white group-hover:bg-slate-50/80 text-slate-700">
                          {peer.roce}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

const SortHeader = ({ label, sKey, config, setConfig, isLast }) => {
  const isActive = config.key === sKey;
  return (
    <th
      className={`p-5 cursor-pointer hover:bg-slate-700 transition-colors border-b border-slate-50/10 
      ${isActive ? "bg-slate-800" : ""} 
      ${isLast ? "rounded-tr-[1.5rem]" : ""}`}
      onClick={() =>
        setConfig({
          key: sKey,
          direction:
            config.key === sKey && config.direction === "desc" ? "asc" : "desc",
        })
      }
    >
      <div className="flex items-center justify-center gap-1">
        <span className={isActive ? "text-indigo-400" : ""}>{label}</span>
        {isActive &&
          (config.direction === "desc" ? (
            <ChevronDown size={8} />
          ) : (
            <ChevronUp size={8} />
          ))}
      </div>
    </th>
  );
};
