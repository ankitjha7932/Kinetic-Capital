import React, { useState, useMemo } from "react";
import { X, Zap, Info, ArrowUpCircle, ArrowDownCircle, ShieldCheck, Clock } from "lucide-react";

const TRADE_DEFINITIONS = {
  insider: "Direct market activity by company promoters or management. Acquisitions often suggest internal value recognition.",
  bulk: "Large transactions (>0.5% equity) typically executed by institutional funds during market hours.",
  block: "Massive pre-arranged institutional trades executed via a specific exchange window.",
  sast: "Regulatory disclosures triggered when ownership levels cross specific percentage thresholds."
};

const TradeModal = ({ isOpen, onClose, trades, symbol }) => {
  const [activeTab, setActiveTab] = useState("insider");

  const tradeData = useMemo(() => trades?.trades || trades || {}, [trades]);
  const currentData = useMemo(() => tradeData[activeTab] || [], [tradeData, activeTab]);

  const stats = useMemo(() => {
    let buyCount = 0;
    let sellCount = 0;
    currentData.forEach(item => {
      const actionRaw = (item.action || item.transaction || "").toUpperCase();
      const qtyNum = parseFloat(String(item.quantity || "0").replace(/,/g, ''));
      if (actionRaw.includes("BUY") || actionRaw.includes("ACQ") || actionRaw === "B" || qtyNum > 0) buyCount++;
      else if (actionRaw.includes("SELL") || actionRaw.includes("SALE") || actionRaw.includes("DISP") || actionRaw === "S" || qtyNum < 0) sellCount++;
    });
    return { buyCount, sellCount };
  }, [currentData]);

  const tabs = useMemo(() => [
    { id: "insider", label: "Insider", count: tradeData?.insider?.length || 0 },
    { id: "bulk", label: "Bulk", count: tradeData?.bulk?.length || 0 },
    { id: "block", label: "Block", count: tradeData?.block?.length || 0 },
    { id: "sast", label: "SAST", count: tradeData?.sast?.length || 0 },
  ], [tradeData]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[1000] flex items-center justify-center p-2 md:p-4">
      <div className="absolute inset-0 bg-slate-900/60 backdrop-blur-md transition-opacity" onClick={onClose} />

      <div className="relative bg-white w-full max-w-6xl h-[95vh] md:h-[90vh] rounded-[24px] md:rounded-[48px] shadow-[0_32px_64px_-12px_rgba(0,0,0,0.2)] flex flex-col overflow-hidden border border-slate-100 animate-in zoom-in-95 duration-300">
        
        {/* PREMIUM HEADER */}
        <div className="px-4 md:px-10 py-5 md:py-8 flex justify-between items-start bg-gradient-to-b from-slate-50 to-white shrink-0">
          <div className="flex gap-3 md:gap-6">
            <div className="h-12 w-12 md:h-16 md:w-16 bg-indigo-600 rounded-2xl md:rounded-[24px] flex items-center justify-center shadow-xl shadow-indigo-100 shrink-0">
              <Zap fill="white" className="text-white w-6 h-6 md:w-8 md:h-8" />
            </div>
            <div>
              <div className="flex items-center flex-wrap gap-2 md:gap-3">
                <h2 className="text-lg sm:text-2xl md:text-4xl font-black text-slate-900 tracking-tighter uppercase leading-tight line-clamp-2 md:line-clamp-none">
                  {trades?.companyName || "Asset Hub"}
                </h2>
                <span className="px-2.5 md:px-4 py-1 md:py-1.5 bg-slate-100 text-slate-600 rounded-full text-[9px] md:text-xs font-black tracking-widest border border-slate-200">
                  {trades?.symbol || symbol}
                </span>
              </div>
              <div className="flex flex-wrap items-center gap-2 md:gap-4 mt-2">
                <div className="flex items-center gap-1.5 px-2 md:px-3 py-1 bg-emerald-50 text-emerald-600 rounded-lg text-[10px] md:text-xs font-black uppercase border border-emerald-100">
                   <div className="w-1.5 h-1.5 md:w-2 md:h-2 rounded-full bg-emerald-500 animate-pulse" />
                   Live Feed
                </div>
                <div className="flex items-center gap-1.5 text-slate-500 text-[10px] md:text-xs font-bold uppercase tracking-widest">
                   <Clock size={12} className="md:w-3.5 md:h-3.5" /> Real-time filings
                </div>
              </div>
            </div>
          </div>
          <button onClick={onClose} className="p-2 md:p-4 hover:bg-slate-100 rounded-xl md:rounded-[20px] text-slate-400 transition-all hover:rotate-90 shrink-0">
            <X className="w-6 h-6 md:w-8 md:h-8" />
          </button>
        </div>

        {/* DEFINITION & STATS BAR */}
        <div className="px-4 md:px-10 mb-4 md:mb-8 grid grid-cols-1 md:grid-cols-3 gap-3 md:gap-6 shrink-0">
          <div className="md:col-span-2 p-3.5 md:p-6 bg-indigo-50/30 border border-indigo-100/50 rounded-2xl md:rounded-[24px] flex flex-col sm:flex-row items-start sm:items-center gap-2 md:gap-4">
            <div className="px-2 md:px-3 py-1 bg-indigo-600 text-white text-[9px] md:text-xs font-black rounded-md md:rounded-lg uppercase tracking-tighter shrink-0">Context</div>
            <p className="text-[11px] md:text-sm font-bold text-indigo-900/60 leading-relaxed italic">
              "{TRADE_DEFINITIONS[activeTab]}"
            </p>
          </div>
          <div className="flex gap-2.5 md:gap-3">
             <div className="flex-1 p-3 md:p-5 bg-emerald-50/50 border border-emerald-100/50 rounded-2xl md:rounded-[24px] flex flex-col items-center justify-center">
                <p className="text-[9px] md:text-xs font-black text-emerald-600 uppercase mb-0.5 md:mb-1">Buying</p>
                <p className="text-xl md:text-2xl font-black text-emerald-700">{stats.buyCount}</p>
             </div>
             <div className="flex-1 p-3 md:p-5 bg-rose-50/50 border border-rose-100/50 rounded-2xl md:rounded-[24px] flex flex-col items-center justify-center">
                <p className="text-[9px] md:text-xs font-black text-rose-600 uppercase mb-0.5 md:mb-1">Selling</p>
                <p className="text-xl md:text-2xl font-black text-rose-700">{stats.sellCount}</p>
             </div>
          </div>
        </div>

        {/* MODERN TAB STRIP */}
        <div className="flex px-4 md:px-10 pb-4 md:pb-6 gap-2 md:gap-3 overflow-x-auto scrollbar-hide shrink-0">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`group relative flex items-center gap-2 md:gap-4 px-4 py-2.5 md:px-10 md:py-5 rounded-xl md:rounded-[24px] transition-all duration-300 border shrink-0 ${
                activeTab === tab.id 
                  ? "bg-slate-900 border-slate-900 text-white shadow-xl md:shadow-2xl translate-y-[-2px]" 
                  : "bg-white border-slate-100 text-slate-400 hover:border-slate-300 hover:bg-slate-50"
              }`}
            >
              <span className="text-[11px] md:text-sm font-black uppercase tracking-widest">{tab.label}</span>
              <div className={`h-5 w-5 md:h-7 md:w-7 rounded-md md:rounded-lg flex items-center justify-center text-[9px] md:text-xs font-black ${activeTab === tab.id ? "bg-white/20" : "bg-slate-100"}`}>
                {tab.count}
              </div>
            </button>
          ))}
        </div>

        {/* TABLE SECTION */}
        <div className="flex-1 overflow-auto px-3 sm:px-5 md:px-10 pb-4 md:pb-10">
          {currentData.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center text-slate-300 bg-slate-50/30 rounded-3xl md:rounded-[40px] border-2 border-dashed border-slate-100 p-6 text-center">
              <Info className="w-10 h-10 md:w-16 md:h-16 mb-4 opacity-5" />
              <p className="font-black uppercase text-xs md:text-sm tracking-[0.2em] md:tracking-[0.3em]">No Historical Activity Reported</p>
            </div>
          ) : (
            <div className="rounded-2xl md:rounded-[32px] overflow-x-auto border border-slate-100 shadow-sm">
              <table className="w-full text-left border-collapse min-w-max">
                <thead>
                  <tr className="bg-slate-900 border-b border-slate-800">
                    <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest whitespace-nowrap">Filing Date</th>
                    <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest whitespace-nowrap">Participating Entity</th>
                    {activeTab !== "sast" && (activeTab === "bulk" || activeTab === "block") && <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest text-center whitespace-nowrap">Action</th>}
                    {activeTab === "insider" && <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest text-right whitespace-nowrap">Value (Lacs)</th>}
                    {activeTab === "sast" && <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest text-right whitespace-nowrap">Stake Change %</th>}
                    {activeTab !== "sast" && <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest text-right whitespace-nowrap">Avg. Price</th>}
                    <th className="px-3 py-3 md:p-6 text-[9px] md:text-xs font-black text-slate-400 uppercase tracking-widest text-right whitespace-nowrap">Quantity</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {currentData.map((row, i) => {
                    const actionRaw = (row.action || row.transaction || "").toUpperCase();
                    const qtyStr = String(row.quantity || "0").replace(/,/g, '');
                    const qtyNum = parseFloat(qtyStr);
                    const isBuy = actionRaw.includes("BUY") || actionRaw.includes("ACQ") || actionRaw === "B" || qtyNum > 0;
                    const displayAction = isBuy ? "BUY" : "SELL";

                    return (
                      <tr key={i} className={`${i % 2 === 0 ? "bg-white" : "bg-slate-50/30"} hover:bg-indigo-50/50 transition-colors group`}>
                        <td className="px-3 py-3 md:p-6 text-xs md:text-sm font-bold text-slate-500 whitespace-nowrap">{row.date}</td>
                        <td className="px-3 py-3 md:p-6 whitespace-nowrap">
                          <p className="text-sm md:text-base font-black text-slate-800 leading-tight group-hover:text-indigo-600 transition-colors">{row.person}</p>
                          {(row.mode || row.transaction) && (
                            <span className={`text-[9px] md:text-[10px] font-black uppercase mt-1.5 md:mt-2 inline-flex items-center gap-1.5 px-2 md:px-3 py-1 rounded-md border ${activeTab === 'sast' ? (isBuy ? 'bg-emerald-50 border-emerald-100 text-emerald-600' : 'bg-rose-50 border-rose-100 text-rose-600') : 'bg-slate-100 border-slate-200 text-slate-500'}`}>
                               {activeTab === 'sast' ? displayAction : row.mode || row.transaction}
                            </span>
                          )}
                        </td>
                        {activeTab !== "sast" && (activeTab === "bulk" || activeTab === "block") && (
                          <td className="px-3 py-3 md:p-6 text-center whitespace-nowrap">
                            <span className={`px-3 md:px-5 py-1 md:py-2 rounded-lg md:rounded-xl text-[9px] md:text-xs font-black uppercase tracking-wider ${isBuy ? "bg-emerald-500 text-white" : "bg-rose-500 text-white"}`}>
                              {displayAction}
                            </span>
                          </td>
                        )}
                        {activeTab === "insider" && <td className="px-3 py-3 md:p-6 text-right text-sm md:text-base font-black text-indigo-600 tabular-nums whitespace-nowrap">{row.valueLacs ? `₹${row.valueLacs}L` : "--"}</td>}
                        {activeTab === "sast" && <td className="px-3 py-3 md:p-6 text-right text-sm md:text-base font-black text-slate-700 tabular-nums whitespace-nowrap">{row.percent || "--"}</td>}
                        {activeTab !== "sast" && <td className="px-3 py-3 md:p-6 text-right text-sm md:text-base font-black text-slate-700 tabular-nums whitespace-nowrap">₹{row.avgPrice || row.price || "0"}</td>}
                        <td className={`px-3 py-3 md:p-6 text-right text-sm md:text-base font-black tabular-nums whitespace-nowrap ${isBuy ? "text-emerald-600" : "text-rose-600"}`}>
                          {row.quantity}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default TradeModal;