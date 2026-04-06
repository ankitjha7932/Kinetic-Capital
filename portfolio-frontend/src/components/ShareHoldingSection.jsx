import React, { useState, useEffect } from "react";
import {
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  Tooltip,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
} from "recharts";
import {
  MousePointer2,
  BarChart3,
  TrendingUp,
  AlertCircle,
  Zap,
  Info,
  Activity,
  ShieldCheck,
} from "lucide-react";
import ShareholdingTable from "./ShareHoldingTable";

const COLORS = [
  "#6366f1",
  "#10b981",
  "#f59e0b",
  "#f43f5e",
  "#8b5cf6",
  "#0ea5e9",
];

const ShareholdingSection = ({ data, analysis, onOpenTrades }) => {
  const [selectedCategory, setSelectedCategory] = useState(null);

  useEffect(() => {
    if (data?.pieData?.length > 0 && !selectedCategory) {
      setSelectedCategory(data.pieData[0].name);
    }
  }, [data, selectedCategory]);

  if (!data) return null;

  const handover = analysis?.performanceMatrix["Handover"];
  const absorption = analysis?.performanceMatrix["Absorption"];
  const isRetailTrap =
    handover?.toLowerCase().includes("retail") ||
    handover?.toLowerCase().includes("public");

  const getTrendData = () => {
    const categoryRow = data.history.find(
      (h) => h.category === selectedCategory,
    );
    if (!categoryRow) return [];

    return data.quarters.map((q) => ({
      quarter: q,
      value: parseFloat(categoryRow.values[q]) || 0,
    }));
  };

  const trendData = getTrendData();

  return (
    <div className="space-y-10 mt-20 pb-20">
      {/* --- HEADER SECTION --- */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 px-2">
        <div className="flex flex-wrap items-center gap-4">
          <h2 className="text-3xl font-black text-slate-900 tracking-tight whitespace-nowrap">
            Shareholding Pattern
          </h2>

          {/* 1. ADDED: MAIN TRADE INTELLIGENCE TRIGGER */}
          <button
            onClick={onOpenTrades}
            className="flex items-center gap-2 px-4 py-2 bg-slate-900 text-white rounded-2xl text-[10px] font-black uppercase tracking-widest hover:scale-105 active:scale-95 transition-all shadow-xl shadow-slate-200 border border-slate-800"
          >
            <TrendingUp size={14} className="text-emerald-400" />
            Trade Intelligence
            <ShieldCheck size={12} className="text-indigo-400 opacity-50" />
          </button>

          {handover && (
            <div
              className={`flex items-center gap-2 px-4 py-1.5 rounded-full border transition-all duration-500 shadow-sm ${
                isRetailTrap
                  ? "bg-rose-50 border-rose-200 text-rose-600 shadow-rose-100"
                  : "bg-indigo-50 border-indigo-100 text-indigo-600 shadow-indigo-100"
              }`}
            >
              {isRetailTrap ? (
                <AlertCircle size={14} className="animate-pulse" />
              ) : (
                <Zap size={14} className="fill-indigo-600" />
              )}

              <span className="text-[10px] font-black uppercase tracking-wider">
                {isRetailTrap ? "Warning: " : "Flow: "} {handover} ({absorption}{" "}
                Absorption)
              </span>

              <div className="group relative ml-1 flex items-center">
                <Info
                  size={12}
                  className="cursor-help opacity-50 hover:opacity-100"
                />
                <div className="invisible group-hover:visible absolute left-1/2 -translate-x-1/2 bottom-full mb-3 w-56 p-3 bg-slate-900 text-white text-[10px] leading-relaxed rounded-2xl shadow-2xl z-50">
                  {isRetailTrap
                    ? "Smart money is exiting to retail investors. This is a high-risk signal."
                    : "Supply from selling institutions is being swallowed by other strong hands."}
                  <div className="absolute top-full left-1/2 -translate-x-1/2 border-8 border-transparent border-t-slate-900" />
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="hidden xl:flex items-center gap-2 px-3 py-1 bg-slate-100 rounded-full text-[9px] font-black text-slate-500 uppercase tracking-widest">
          <MousePointer2 size={10} /> Click slices to explore trends
        </div>
      </div>

      {/* --- INTERACTIVE VISUALIZATION GRID --- */}
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-8">
        {/* LEFT: PIZZA PIE CHART */}
        <div className="lg:col-span-2 bg-white p-10 rounded-[48px] border border-slate-100 shadow-sm flex flex-col items-center">
          <div className="w-full mb-6 text-center">
            <h3 className="text-[10px] font-black text-slate-400 uppercase tracking-[0.2em] mb-1">
              Current Anchor
            </h3>
            <p className="text-lg font-black text-slate-800">
              {data.latestQuarterName}
            </p>
          </div>

          <div className="h-[340px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={data.pieData}
                  cx="50%"
                  cy="50%"
                  outerRadius={130}
                  innerRadius={0}
                  paddingAngle={2}
                  dataKey="value"
                  stroke="#fff"
                  strokeWidth={4}
                  onClick={(entry) => setSelectedCategory(entry.name)}
                  style={{ cursor: "pointer", outline: "none" }}
                >
                  {data.pieData.map((entry, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={COLORS[index % COLORS.length]}
                      stroke={selectedCategory === entry.name ? "#000" : "#fff"}
                      strokeWidth={selectedCategory === entry.name ? 2 : 4}
                      className="hover:opacity-80 transition-all duration-300"
                    />
                  ))}
                </Pie>
                <Tooltip content={<CustomPieTooltip />} />
              </PieChart>
            </ResponsiveContainer>
          </div>

          <div className="flex flex-wrap justify-center gap-2 mt-6">
            {data.pieData.map((entry, index) => (
              <button
                key={entry.name}
                onClick={() => setSelectedCategory(entry.name)}
                className={`px-3 py-1.5 rounded-xl border text-[10px] font-black uppercase transition-all ${
                  selectedCategory === entry.name
                    ? "bg-slate-900 border-slate-900 text-white shadow-lg scale-105"
                    : "bg-white border-slate-100 text-slate-400 hover:border-slate-300"
                }`}
              >
                <span
                  className="inline-block w-1.5 h-1.5 rounded-full mr-2"
                  style={{ backgroundColor: COLORS[index % COLORS.length] }}
                />
                {entry.name}
              </button>
            ))}
          </div>
        </div>

        {/* RIGHT: DYNAMIC HISTORY BAR GRAPH */}
        <div className="lg:col-span-3 bg-slate-900 p-10 rounded-[48px] shadow-2xl flex flex-col relative overflow-hidden">
          <div className="absolute -top-24 -right-24 w-72 h-72 bg-indigo-500/10 blur-[120px] rounded-full" />

          <div className="relative z-10 flex justify-between items-start mb-14">
            <div>
              <p className="text-[10px] font-black text-indigo-400 uppercase tracking-[0.3em] mb-2">
                Multi-Year Trajectory
              </p>
              <h4 className="text-4xl font-black text-white tracking-tighter">
                {selectedCategory}{" "}
                <span className="text-slate-500 font-medium">History</span>
              </h4>
            </div>
            <div className="p-4 bg-white/5 rounded-3xl border border-white/10">
              <BarChart3 className="text-indigo-400" size={24} />
            </div>
          </div>

          <div className="h-[280px] w-full relative z-10">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={trendData}
                margin={{ top: 20, right: 0, left: -25, bottom: 0 }}
              >
                <CartesianGrid
                  strokeDasharray="3 3"
                  vertical={false}
                  stroke="rgba(255,255,255,0.05)"
                />
                <XAxis
                  dataKey="quarter"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 10, fontWeight: 800, fill: "#64748b" }}
                />
                <YAxis
                  axisLine={false}
                  tickLine={false}
                  tick={{ fontSize: 10, fontWeight: 800, fill: "#64748b" }}
                  unit="%"
                />
                <Tooltip
                  cursor={{ fill: "rgba(255,255,255,0.03)" }}
                  content={<CustomBarTooltip />}
                />
                <Bar
                  dataKey="value"
                  radius={[12, 12, 0, 0]}
                  barSize={38}
                  animationDuration={1200}
                >
                  {trendData.map((entry, index) => {
                    const isGrowing =
                      index > 0 && entry.value > trendData[index - 1].value;
                    return (
                      <Cell
                        key={`bar-${index}`}
                        fill={isGrowing ? "#10b981" : "#6366f1"}
                      />
                    );
                  })}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>

          <div className="mt-8 flex items-center gap-2 text-xs font-bold text-slate-500 italic">
            <TrendingUp size={14} className="text-emerald-500" />
            <span>Green bars indicate accumulation phases.</span>
          </div>
        </div>
      </div>

      {/* --- TABLE SECTION --- */}
      <ShareholdingTable quarters={data.quarters} history={data.history} />
    </div>
  );
};

/* --- MINI HELPERS --- */
const CustomPieTooltip = ({ active, payload }) => {
  if (active && payload?.[0]) {
    return (
      <div className="bg-slate-900 text-white px-4 py-2 rounded-2xl shadow-2xl border border-white/10 text-xs font-black">
        {payload[0].name}: {payload[0].value}%
      </div>
    );
  }
  return null;
};

const CustomBarTooltip = ({ active, payload }) => {
  if (active && payload?.[0]) {
    return (
      <div className="bg-white p-5 rounded-[24px] shadow-2xl border border-slate-100">
        <p className="text-[10px] font-black text-slate-400 uppercase mb-1">
          {payload[0].payload.quarter}
        </p>
        <p className="text-2xl font-black text-slate-900">
          {payload[0].value}%
        </p>
        <div className="mt-2 flex items-center gap-1.5 text-[9px] font-black uppercase text-indigo-500">
          <Activity size={10} /> Holding Value
        </div>
      </div>
    );
  }
  return null;
};

export default ShareholdingSection;
