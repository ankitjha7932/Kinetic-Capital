import React, { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts';
import { Layers, ChevronUp, ChevronDown, PieChart as PieIcon, Activity } from 'lucide-react';

const COLORS = ["#6366f1", "#8b5cf6", "#ec4899", "#f43f5e", "#f59e0b", "#10b981", "#0ea5e9"];

export default function PeerComparisonTable({ data }) {
  const navigate = useNavigate();
  const [sortConfig, setSortConfig] = useState({ key: 'marketCap', direction: 'desc' });
  const [activeSlice, setActiveSlice] = useState(null);

  if (!data || !data.peers || data.peers.length === 0) return null;

  const parseValue = (val) => {
    if (!val || val === "—") return 0;
    return parseFloat(val.toString().replace(/,/g, '').replace(/%/g, ''));
  };

  const formatIndian = (val, isCurrency = false) => {
    if (!val || val === "—") return "—";
    const num = parseFloat(val.toString().replace(/,/g, ''));
    const formatted = new Intl.NumberFormat('en-IN', { 
      maximumFractionDigits: 2,
      minimumFractionDigits: 2 
    }).format(num);
    return isCurrency ? `₹${formatted}` : formatted;
  };

  const sortedPeers = useMemo(() => {
    let sortableItems = [...data.peers];
    sortableItems.sort((a, b) => {
      const aVal = parseValue(a[sortConfig.key]);
      const bVal = parseValue(b[sortConfig.key]);
      return sortConfig.direction === 'asc' ? aVal - bVal : bVal - aVal;
    });
    return sortableItems;
  }, [data.peers, sortConfig]);

  const pieData = useMemo(() => {
    return sortedPeers.map((p, index) => ({
      name: p.name,
      value: Math.abs(parseValue(p[sortConfig.key])),
      symbol: p.symbol,
      fill: COLORS[index % COLORS.length]
    })).filter(p => p.value > 0);
  }, [sortedPeers, sortConfig.key]);

  const displayData = activeSlice || (pieData.length > 0 ? pieData[0] : null);

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
        
        {/* --- LEFT: SCALED UP DONUT --- */}
        <div className="lg:col-span-2 bg-white rounded-[2rem] p-6 shadow-sm border border-slate-100 flex flex-col items-center min-h-[520px]">
          <div className="w-full flex items-center gap-2 mb-2">
            <PieIcon size={14} className="text-indigo-500" />
            <h3 className="text-[9px] font-black text-slate-400 uppercase tracking-widest">
              Distribution Analysis
            </h3>
          </div>
          
          {/* Increased Height and Flex-1 to give Donut more space */}
          <div className="flex-1 w-full relative min-h-[340px]">
            <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none z-10 translate-y-2">
              <span className="text-[9px] font-black text-slate-400 uppercase tracking-[0.2em] mb-1">
                {sortConfig.key}
              </span>
              {/* SHORTER FONT: Changed from text-3xl to text-2xl to prevent overflow */}
              <span className="text-2xl font-black text-slate-900 tabular-nums tracking-tight">
                {displayData ? formatIndian(displayData.value, sortConfig.key === 'marketCap') : "0.00"}
              </span>
              <span className="text-[8px] font-black text-indigo-500 uppercase mt-2 px-3 py-1 bg-indigo-50 rounded-full border border-indigo-100 max-w-[160px] truncate">
                {displayData?.name || "Select Peer"}
              </span>
            </div>

            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={pieData}
                  innerRadius={95} // Increased to create more room for text
                  outerRadius={135} // Increased to make the pie look bigger
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
                      className="cursor-pointer outline-none"
                      opacity={activeSlice && activeSlice.name !== entry.name ? 0.3 : 1}
                    />
                  ))}
                </Pie>
              </PieChart>
            </ResponsiveContainer>
          </div>

          {/* --- LEGEND AT BOTTOM --- */}
          <div className="w-full grid grid-cols-2 gap-x-4 gap-y-2 pt-6 border-t border-slate-50 mt-auto">
            {pieData.slice(0, 6).map((entry) => (
              <div 
                key={entry.name}
                className={`flex items-center gap-2 p-1.5 rounded-lg transition-all ${
                  displayData?.name === entry.name ? 'bg-slate-50' : ''
                }`}
              >
                <div className="w-1.5 h-1.5 rounded-full shrink-0" style={{ backgroundColor: entry.fill }} />
                <span className="text-[9px] font-black text-slate-500 truncate uppercase tracking-tighter">
                  {entry.name}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* --- RIGHT: DATA TERMINAL --- */}
        <div className="lg:col-span-3 bg-white rounded-[2rem] shadow-sm border border-slate-100 overflow-hidden flex flex-col">
          <div className="p-8 flex justify-between items-center border-b border-slate-50">
            <div className="flex items-center gap-4">
              <div className="p-3 bg-slate-900 rounded-2xl text-white shadow-lg">
                <Activity size={18} />
              </div>
              <div>
                <h2 className="text-xl font-black text-slate-900 tracking-tight leading-none">Peer Intelligence</h2>
                <div className="mt-1.5 flex items-center gap-2">
                  <span className="text-[9px] font-black text-white bg-indigo-600 px-2 py-0.5 rounded uppercase tracking-widest">
                    {data.industry}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <div className="overflow-x-auto flex-1">
            <table className="w-full table-fixed border-collapse">
              <thead>
                <tr className="bg-[#1e293b] text-white uppercase text-[9px] font-black tracking-[0.15em]">
                  <th className="w-[30%] p-5 text-left rounded-tl-2xl">Company</th>
                  <SortHeader label="P/E" sKey="pe" config={sortConfig} setConfig={setSortConfig} />
                  <SortHeader label="Mar Cap" sKey="marketCap" config={sortConfig} setConfig={setSortConfig} />
                  <SortHeader label="Profit Var" sKey="profitVarQtr" config={sortConfig} setConfig={setSortConfig} />
                  <SortHeader label="ROCE" sKey="roce" config={sortConfig} setConfig={setSortConfig} isLast />
                </tr>
              </thead>
              <tbody className="text-xs font-bold text-slate-800">
                {sortedPeers.map((peer) => (
                  <tr 
                    key={peer.name} 
                    className={`border-b border-slate-50 transition-colors ${
                      peer.isCurrent ? 'bg-indigo-50/50' : 
                      displayData?.name === peer.name ? 'bg-slate-50' : ''
                    }`}
                  >
                    <td className="p-5 truncate">
                      <button onClick={() => peer.symbol && navigate(`/stock/${peer.symbol}`)} className="text-indigo-600 hover:underline">
                        {peer.name}
                      </button>
                    </td>
                    <td className="p-5 text-center font-mono text-slate-600">{peer.pe}</td>
                    <td className="p-5 text-center font-mono text-slate-900">{formatIndian(peer.marketCap, true)}</td>
                    <td className={`p-5 text-center font-mono ${peer.profitVarQtr?.startsWith('-') ? 'text-rose-500' : 'text-emerald-500'}`}>
                      {peer.profitVarQtr}
                    </td>
                    <td className="p-5 text-center font-mono text-slate-600">{peer.roce}</td>
                  </tr>
                ))}
              </tbody>
            </table>
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
      className={`p-5 cursor-pointer hover:bg-slate-700 transition-colors ${isLast ? 'rounded-tr-2xl' : ''}`}
      onClick={() => setConfig({ key: sKey, direction: config.key === sKey && config.direction === 'desc' ? 'asc' : 'desc' })}
    >
      <div className="flex items-center justify-center gap-1 text-[8px]">
        <span className={isActive ? 'text-indigo-400' : ''}>{label}</span>
        {isActive && (config.direction === 'desc' ? <ChevronDown size={8} /> : <ChevronUp size={8} />)}
      </div>
    </th>
  );
};