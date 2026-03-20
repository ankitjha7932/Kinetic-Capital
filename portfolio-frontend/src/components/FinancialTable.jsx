import React from 'react';
import { TrendingUp, TrendingDown, AlertCircle, Zap, CheckCircle2 } from 'lucide-react';

const FinancialTable = ({ data, title }) => {
  const filteredData = data?.filter(row => row.metric !== "Raw PDF") || [];
  if (filteredData.length === 0) return null;

  const tablePrefix = title.toLowerCase().includes('quarter') ? 'quarters' : 
                      title.toLowerCase().includes('profit') ? 'pl' : 
                      title.toLowerCase().includes('balance') ? 'balance' : 'cash';

  const headers = Object.keys(filteredData[0].values || {}).sort((a, b) => {
    const parseDate = (s) => (s === 'TTM' ? new Date(2099, 1, 1) : new Date(s));
    return parseDate(a) - parseDate(b);
  });

  const latestHeader = headers[headers.length - 1];
  const prevHeader = headers[headers.length - 2];

  // 1. ADDED: STRATEGIC SENTIMENT LOGIC
  const getSentiment = () => {
    const profitRow = filteredData.find(r => r.metric.match(/Net Profit|Profit after tax/i));
    const salesRow = filteredData.find(r => r.metric.match(/Sales|Revenue/i));
    if (!profitRow || headers.length < 2) return null;

    const currP = parseFloat(profitRow.values[latestHeader] || 0);
    const prevP = parseFloat(profitRow.values[prevHeader] || 0);
    const currS = salesRow ? parseFloat(salesRow.values[latestHeader] || 0) : 0;
    const prevS = salesRow ? parseFloat(salesRow.values[prevHeader] || 0) : 0;

    if (currP > prevP && currS > prevS) {
        return { label: "Growth Multiplier", color: "bg-emerald-50 text-emerald-700 border-emerald-100", icon: <TrendingUp size={12}/> };
    } 
    if (currP > prevP) {
        return { label: "Bottom-line Recovery", color: "bg-blue-50 text-blue-700 border-blue-100", icon: <CheckCircle2 size={12}/> };
    }
    return { label: "Margin Pressure", color: "bg-rose-50 text-rose-700 border-rose-100", icon: <TrendingDown size={12}/> };
  };

  const sentiment = getSentiment();

  const highlights = filteredData.reduce((acc, row) => {
    const current = parseFloat(row.values[latestHeader]);
    const prev = parseFloat(row.values[prevHeader]);
    if (!isNaN(current) && !isNaN(prev) && prev !== 0) {
      const change = ((current - prev) / Math.abs(prev)) * 100;
      if (change >= 50) acc.positive.push(row.metric);
      else if (change <= -50) acc.negative.push(row.metric);
    }
    return acc;
  }, { positive: [], negative: [] });

  const getSlug = (text) => text.toLowerCase().replace(/[^a-z0-9]/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');

  return (
    <div className="space-y-6 mb-20">
      <div className="flex flex-col gap-3">
        <div className="flex items-center gap-4 px-2">
            <h2 className="text-2xl font-black text-slate-800 tracking-tight">{title}</h2>
            
            {/* 2. ADDED: THE STRATEGIC SUMMARY BADGE */}
            {sentiment && (
                <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full border text-[10px] font-black uppercase tracking-widest shadow-sm ${sentiment.color}`}>
                    {sentiment.icon}
                    {sentiment.label}
                </div>
            )}
        </div>

        {(highlights.positive.length > 0 || highlights.negative.length > 0) && (
          <div className="flex flex-wrap gap-2 px-2">
            {highlights.positive.map(m => (
              <div key={m} className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-50 border border-emerald-100 rounded-xl shadow-sm">
                <Zap size={10} className="text-emerald-600 fill-emerald-600" />
                <span className="text-[9px] font-black text-emerald-700 uppercase tracking-widest">{m} Surged</span>
              </div>
            ))}
            {highlights.negative.map(m => (
              <div key={m} className="flex items-center gap-1.5 px-3 py-1.5 bg-rose-50 border border-rose-100 rounded-xl shadow-sm">
                <AlertCircle size={10} className="text-rose-600" />
                <span className="text-[9px] font-black text-rose-700 uppercase tracking-widest">{m} Dropped</span>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="bg-white rounded-[2.5rem] shadow-2xl shadow-slate-200/40 border border-slate-100 overflow-hidden">
        <div className="overflow-x-auto custom-scrollbar">
          <table className="min-w-full border-separate border-spacing-0">
            <thead>
              <tr>
                <th className="sticky left-0 z-30 bg-slate-900 px-8 py-6 text-left text-[10px] font-black uppercase tracking-[0.2em] text-slate-400 border-r border-slate-800">
                  Financial Metrics
                </th>
                {headers.map((header, idx) => {
                  const isLatest = idx === headers.length - 1;
                  return (
                    <th key={header} className={`${isLatest ? 'bg-indigo-900' : 'bg-slate-800'} px-6 py-6 text-right text-[10px] font-black uppercase tracking-widest text-white border-b border-slate-700 min-w-[140px]`}>
                      {header}
                    </th>
                  );
                })}
              </tr>
            </thead>
            
            <tbody className="bg-white">
              {filteredData.map((row, idx) => {
                const isEven = idx % 2 === 0;
                const bgClass = isEven ? 'bg-white' : 'bg-slate-50';
                const rowId = `${tablePrefix}-row-${getSlug(row.metric)}`;

                return (
                  <tr key={idx} id={rowId} className={`group ${bgClass} hover:bg-indigo-50/80 transition-all duration-300 scroll-mt-40`}>
                    <td className={`sticky left-0 z-20 px-8 py-6 text-[13px] font-black border-b border-slate-100 
                      ${bgClass} group-hover:bg-indigo-50 border-r border-slate-100 shadow-[4px_0_10px_rgba(0,0,0,0.06)] text-slate-900`}>
                      {row.metric}
                    </td>

                    {headers.map((header, hIdx) => {
                      const currentVal = row.values[header];
                      const isLatest = hIdx === headers.length - 1;
                      
                      let trendInfo = null;
                      if (isLatest && hIdx > 0) {
                          const prevVal = parseFloat(row.values[headers[hIdx - 1]]);
                          const curValFloat = parseFloat(currentVal);
                          if (!isNaN(curValFloat) && !isNaN(prevVal) && prevVal !== 0) {
                              const diff = ((curValFloat - prevVal) / Math.abs(prevVal)) * 100;
                              trendInfo = {
                                  color: diff >= 0 ? "text-emerald-600" : "text-rose-600",
                                  icon: diff >= 0 ? TrendingUp : TrendingDown,
                                  percent: Math.abs(diff).toFixed(0)
                              };
                          }
                      }

                      return (
                        <td key={header} className={`px-6 py-6 text-[13px] text-right border-b border-slate-50 tabular-nums whitespace-nowrap ${isLatest ? 'bg-indigo-50/30' : ''}`}>
                          <div className="flex flex-col items-end justify-center min-h-[44px]">
                              <span className="text-slate-900 font-black">
                                  {currentVal != null ? currentVal.toLocaleString('en-IN') : "—"}
                              </span>
                              <div className="h-4 flex items-center">
                                {trendInfo && (
                                    <span className={`flex items-center text-[10px] font-black ${trendInfo.color}`}>
                                        <trendInfo.icon size={10} className="mr-0.5" />
                                        {trendInfo.percent}%
                                    </span>
                                )}
                              </div>
                          </div>
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
    </div>
  );
};

export default FinancialTable;