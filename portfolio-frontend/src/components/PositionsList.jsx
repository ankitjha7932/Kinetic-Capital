import React from "react";
import api from "../api/axios";
import { Trash2, TrendingUp, TrendingDown } from "lucide-react";

const getCapStyles = (label) => {
  switch (label) {
    case 'LARGE-CAP':
      return 'bg-indigo-50 text-indigo-600 border-indigo-100';
    case 'MID-CAP':
      return 'bg-emerald-50 text-emerald-600 border-emerald-100';
    case 'SMALL-CAP':
      return 'bg-amber-50 text-amber-600 border-amber-100';
    case 'MICRO-CAP':
      return 'bg-rose-50 text-rose-600 border-rose-100';
    default:
      return 'bg-slate-100 text-slate-500 border-slate-200';
  }
};

export default function PositionsList({ holdings, onRefresh, onSelectStock }) {
  const [filter, setFilter] = React.useState('ALL');

  const filteredHoldings = filter === 'ALL' 
    ? holdings 
    : holdings.filter(h => h.marketCapLabel === filter);

  const handleDelete = async (id) => {
    if (!id || id.toString().trim() === "" || id === "undefined") {
      alert("Error: Holding ID is missing or invalid.");
      return;
    }

    if (window.confirm("Are you sure you want to remove this holding?")) {
      try {
        await api.delete(`/holdings/${id.toString().trim()}`);
        onRefresh();
      } catch (err) {
        const errorMsg = err.response?.data?.message || err.response?.data || err.message;
        alert("Delete failed: " + errorMsg);
      }
    }
  };

  if (!holdings || holdings.length === 0) {
    return (
      <div className="bg-white rounded-2xl p-12 text-center border border-dashed border-slate-300">
        <div className="text-slate-400 mb-2 font-medium">No positions found.</div>
        <p className="text-sm text-slate-500">Click "New Holding" to add your first stock.</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
      <div className="p-6 border-b border-slate-50 bg-slate-50/30">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h3 className="text-xl font-black text-slate-800 tracking-tight">Current Positions</h3>
            <p className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">
              {holdings.length} Assets Total
            </p>
          </div>

          <div className="flex bg-white p-1 rounded-2xl border border-slate-100 shadow-sm overflow-x-auto">
            {['ALL', 'LARGE-CAP', 'MID-CAP', 'SMALL-CAP', 'MICRO-CAP'].map((cap) => (
              <button
                key={cap}
                onClick={() => setFilter(cap)}
                className={`px-3 py-1.5 rounded-xl text-[9px] font-black transition-all whitespace-nowrap
                  ${filter === cap 
                    ? 'bg-indigo-600 text-white shadow-md shadow-indigo-100' 
                    : 'text-slate-400 hover:text-slate-600 hover:bg-slate-50'}`}
              >
                {cap.replace('-CAP', '')}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="overflow-x-auto">
        {filteredHoldings.length === 0 ? (
          <div className="p-12 text-center text-slate-400 font-medium">
            No {filter === 'ALL' ? '' : filter} assets found.
          </div>
        ) : (
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-50/50 text-slate-400 text-[10px] uppercase tracking-[0.2em] font-black">
                <th className="px-6 py-4">Asset</th>
                <th className="px-6 py-4 text-center">Quantity</th>
                <th className="px-6 py-4">Avg. Buy Price</th>
                <th className="px-6 py-4">Current Price</th>
                <th className="px-6 py-4">P&L (%)</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {/* UPDATED: Mapping over filteredHoldings instead of holdings */}
              {filteredHoldings.map((h) => {
                const holdingId = h.id || h._id;
                const pnlPercent = ((h.currentPrice - h.avgBuyPrice) / h.avgBuyPrice) * 100;
                const isProfit = pnlPercent >= 0;

                return (
                  <tr key={holdingId} className="hover:bg-slate-50/50 transition-colors group">
                    <td className="px-6 py-4">
                      <div className="flex flex-col cursor-pointer group/item" onClick={() => onSelectStock(h.symbol)}>
                        <span className="font-black text-slate-800 text-lg uppercase group-hover/item:text-indigo-600">
                          {h.symbol}
                        </span>
                        <div className="flex items-center gap-2 mt-0.5">
                          {h.marketCapLabel ? (
                            <span className={`px-2 py-0.5 rounded-lg border text-[9px] font-black uppercase tracking-tight ${getCapStyles(h.marketCapLabel)}`}>
                              {h.marketCapLabel}
                            </span>
                          ) : (
                            <span className="text-[9px] font-bold text-slate-300 italic">Data Pending</span>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-center">
                      <span className="font-bold text-slate-600 bg-slate-100 px-2 py-1 rounded-lg text-sm">
                        {h.quantity}
                      </span>
                    </td>
                    <td className="px-6 py-4 font-medium text-slate-600 text-sm">
                      ₹{h.avgBuyPrice?.toLocaleString()}
                    </td>
                    <td className="px-6 py-4 font-medium text-slate-600 text-sm">
                      ₹{h.currentPrice?.toLocaleString()}
                    </td>
                    <td className="px-6 py-4">
                      <div className={`flex items-center gap-1 font-black ${isProfit ? "text-green-600" : "text-red-600"}`}>
                        {isProfit ? <TrendingUp size={14} /> : <TrendingDown size={14} />}
                        <span>{isProfit ? "+" : ""}{pnlPercent.toFixed(2)}%</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-right">
                      <button
                        onClick={() => handleDelete(holdingId)}
                        className="p-2 text-slate-300 hover:text-red-500 hover:bg-red-50 rounded-xl transition-all"
                      >
                        <Trash2 size={18} />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}