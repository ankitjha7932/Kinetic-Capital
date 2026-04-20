import React from 'react';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';

const ShareholdingTable = ({ quarters, history, selectedCategory, onCategorySelect }) => {
  if (!history || !quarters) return null;
  const renderValueWithDelta = (currentVal, prevVal, isShareholderCount = false) => {
    const curr = parseFloat(String(currentVal).replace(/,/g, '')) || 0;
    const prev = parseFloat(String(prevVal).replace(/,/g, '')) || 0;

    // Logic for Delta calculation
    let deltaDisplay = "0.00";
    let isPositive = false;
    let isNegative = false;

    if (prev !== 0) {
      if (isShareholderCount) {
        // Growth percentage: ((New - Old) / Old) * 100
        const pctChange = ((curr - prev) / prev) * 100;
        deltaDisplay = `${Math.abs(pctChange).toFixed(1)}%`;
        isPositive = pctChange > 0.01;
        isNegative = pctChange < -0.01;
      } else {
        // Simple BP change for percentage rows
        const diff = curr - prev;
        deltaDisplay = `${Math.abs(diff).toFixed(2)}%`;
        isPositive = diff > 0.001;
        isNegative = diff < -0.001;
      }
    }

    return (
      <div className="flex flex-col items-start gap-1">
        {/* Main Value */}
        <span className="text-sm md:text-base font-black text-slate-700 tabular-nums">
          {isShareholderCount ? curr.toLocaleString('en-IN') : `${curr.toFixed(2)}%`}
        </span>
        
        {/* Delta Badge */}
        {isPositive || isNegative ? (
          <div className={`flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-[9px] md:text-[10px] font-black uppercase transition-colors ${
            isPositive ? "bg-emerald-50 text-emerald-600" : "bg-rose-50 text-rose-600"
          }`}>
            {isPositive ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
            {deltaDisplay}
          </div>
        ) : (
          <div className="flex items-center gap-0.5 px-1.5 py-0.5 rounded-md text-[9px] md:text-[10px] font-black text-slate-300 bg-slate-50 uppercase">
            <Minus size={10} /> {isShareholderCount ? "0.0%" : "0.00"}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="bg-white rounded-2xl md:rounded-[40px] border border-slate-100 shadow-sm overflow-hidden">
      <div className="overflow-x-auto scrollbar-thin scrollbar-thumb-slate-200 scrollbar-track-transparent">
        <table className="w-full text-left border-collapse min-w-max">
          <thead>
            <tr className="bg-slate-900">
              <th className="w-[180px] md:w-[240px] p-4 md:p-6 text-[10px] md:text-xs font-black text-slate-400 uppercase tracking-[0.2em] sticky left-0 z-30 bg-slate-900 border-r border-slate-800 shadow-[4px_0_10px_rgba(0,0,0,0.3)]">
                Ownership Category
              </th>
              {quarters.map((q) => (
                <th key={q} className="p-4 md:p-6 text-[10px] md:text-xs font-black text-slate-400 uppercase tracking-[0.2em] whitespace-nowrap">
                  {q}
                </th>
              ))}
            </tr>
          </thead>
          
          <tbody className="divide-y divide-slate-100">
            {history.map((row, idx) => {
              const isSHRow = row.category?.toLowerCase().includes("shareholders");
              const isSelected = selectedCategory === row.category;

              return (
                <tr 
                  key={idx} 
                  onClick={() => onCategorySelect?.(row.category)}
                  className={`group cursor-pointer transition-colors odd:bg-[#f9fafb] ${
                    isSelected ? 'bg-indigo-50/60' : 'hover:bg-indigo-50/20'
                  }`}
                >
                  <td className={`p-4 md:p-6 font-black text-xs md:text-sm sticky left-0 z-20 whitespace-nowrap border-r border-slate-100 shadow-[4px_0_8px_-4px_rgba(0,0,0,0.15)] transition-colors 
                    ${isSelected 
                      ? '!bg-[#f1f5ff] text-indigo-600' 
                      : 'bg-white group-odd:bg-[#f9fafb] text-slate-800'
                    }`}
                  >
                    {row.category}
                  </td>
                  
                  {quarters.map((q, qIdx) => {
                    const currentVal = row.values[q];
                    // Get previous quarter value (returns 0 for the very first quarter in list)
                    const prevVal = qIdx > 0 ? row.values[quarters[qIdx - 1]] : 0;
                    
                    return (
                      <td key={q} className="p-4 md:p-6">
                        {renderValueWithDelta(currentVal, prevVal, isSHRow)}
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ShareholdingTable;