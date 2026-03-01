import React from 'react';
import api from '../api/axios';
import { Trash2, TrendingUp, TrendingDown, Tag } from 'lucide-react';

// Added onSelectStock to props
export default function PositionsList({ holdings, onRefresh, onSelectStock }) {
  
  const handleDelete = async (id) => {
    if (window.confirm("Are you sure you want to remove this holding?")) {
      try {
        await api.delete(`/holdings/${id}`);
        onRefresh();
      } catch (err) {
        alert("Delete failed: " + (err.response?.data || err.message));
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
      <div className="p-6 border-b border-slate-50 flex justify-between items-center bg-slate-50/50">
        <h3 className="text-xl font-black text-slate-800 tracking-tight">Current Positions</h3>
        <span className="px-3 py-1 bg-indigo-100 text-indigo-700 text-xs font-bold rounded-full">
          {holdings.length} Assets
        </span>
      </div>

      <div className="overflow-x-auto">
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
            {holdings.map((h) => {
              const pnlPercent = ((h.currentPrice - h.avgBuyPrice) / h.avgBuyPrice) * 100;
              const isProfit = pnlPercent >= 0;

              return (
                <tr key={h.id} className="hover:bg-slate-50/50 transition-colors group">
                  <td className="px-6 py-4">
                    {/* Minimal Change: Added onClick and cursor-pointer to the container */}
                    <div 
                      className="flex flex-col cursor-pointer group/item" 
                      onClick={() => onSelectStock(h.symbol)}
                    >
                      <span className="font-black text-slate-800 text-lg uppercase group-hover/item:text-indigo-600">
                        {h.symbol}
                      </span>
                      <div className="flex items-center gap-1 text-slate-400">
                        <Tag size={10} />
                        <span className="text-[10px] font-bold uppercase tracking-tighter">
                          {h.tags || 'Equity'}
                        </span>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-center">
                    <span className="font-bold text-slate-600 bg-slate-100 px-2 py-1 rounded-lg text-sm">
                      {h.quantity}
                    </span>
                  </td>
                  <td className="px-6 py-4 font-medium text-slate-600 text-sm">₹{h.avgBuyPrice?.toLocaleString()}</td>
                  <td className="px-6 py-4 font-medium text-slate-600 text-sm">₹{h.currentPrice?.toLocaleString()}</td>
                  <td className="px-6 py-4">
                    <div className={`flex items-center gap-1 font-black ${isProfit ? 'text-green-600' : 'text-red-600'}`}>
                      {isProfit ? <TrendingUp size={14}/> : <TrendingDown size={14}/>}
                      <span>{isProfit ? '+' : ''}{pnlPercent.toFixed(2)}%</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <button 
                      onClick={() => handleDelete(h.id)}
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
      </div>
    </div>
  );
}