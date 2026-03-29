import React from 'react';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';

const ShareholdingTable = ({ quarters, history }) => {
  if (!history || !quarters) return null;

  // Helper to calculate delta and return UI
  const renderValueWithDelta = (currentVal, prevVal) => {
    const curr = parseFloat(currentVal) || 0;
    const prev = parseFloat(prevVal) || 0;
    const delta = curr - prev;

    return (
      <div className="flex flex-col items-start gap-1">
        <span className="text-sm font-black text-slate-700">{curr.toFixed(2)}%</span>
        {prev !== 0 && delta !== 0 ? (
          <div className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-[9px] font-black uppercase ${
            delta > 0 ? 'bg-emerald-50 text-emerald-600' : 'bg-rose-50 text-rose-600'
          }`}>
            {delta > 0 ? <TrendingUp size={8} /> : <TrendingDown size={8} />}
            {Math.abs(delta).toFixed(2)}%
          </div>
        ) : (
          <div className="flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-[9px] font-black text-slate-300 bg-slate-50 uppercase">
            <Minus size={8} /> 0.00
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="bg-white rounded-[40px] border border-slate-100 shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-900">
              <th className="p-6 text-[10px] font-black text-slate-400 uppercase tracking-[0.2em] sticky left-0 bg-slate-900 z-20 shadow-[2px_0_5px_rgba(0,0,0,0.1)]">
                Ownership Category
              </th>
              {quarters.map((q) => (
                <th key={q} className="p-6 text-[10px] font-black text-slate-400 uppercase tracking-[0.2em] whitespace-nowrap">
                  {q}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-50">
            {history.map((row, idx) => (
              <tr key={idx} className="hover:bg-slate-50/50 transition-colors group">
                <td className="p-6 font-black text-slate-800 text-xs sticky left-0 bg-white z-10 group-hover:bg-slate-50/50 shadow-[2px_0_5px_rgba(0,0,0,0.02)] border-r border-slate-50 whitespace-nowrap">
                  {row.category}
                </td>
                {quarters.map((q, qIdx) => {
                  const currentVal = row.values[q];
                  const prevVal = qIdx > 0 ? row.values[quarters[qIdx - 1]] : 0;
                  return (
                    <td key={q} className="p-6">
                      {renderValueWithDelta(currentVal, prevVal)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ShareholdingTable;