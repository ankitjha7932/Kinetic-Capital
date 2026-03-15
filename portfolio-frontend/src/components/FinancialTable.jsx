import React from 'react';

const FinancialTable = ({ data, title }) => {
  if (!data || data.length === 0) return (
    <div className="p-10 text-center text-slate-400 italic bg-white rounded-3xl border border-slate-100">
      No historical data available for {title}.
    </div>
  );

  // Extract and sort headers (Dates) logic
  const headers = Object.keys(data[0].values || {}).sort((a, b) => {
    const parseDate = (s) => {
      if (s === 'TTM') return new Date(2099, 1, 1);
      return new Date(s);
    };
    return parseDate(a) - parseDate(b);
  });

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-12">
      {/* Header Section mimicking Screener.in */}
      <div className="px-6 py-5 border-b border-slate-100 bg-white">
        <h3 className="text-xl font-semibold text-slate-800 tracking-tight">{title}</h3>
        <p className="text-[13px] text-slate-500 mt-1">
          Consolidated Figures in Rs. Crores / <span className="text-indigo-600 cursor-pointer hover:underline font-medium">View Standalone</span>
        </p>
      </div>

      {/* Table Container with Custom Scrollbar */}
      <div className="overflow-x-auto scrollbar-thin scrollbar-thumb-slate-200">
        <table className="min-w-full border-separate border-spacing-0">
          <thead>
            <tr className="bg-slate-50/50">
              <th scope="col" className="sticky left-0 z-20 bg-slate-50 py-3 pl-6 pr-4 text-left text-[11px] font-bold uppercase tracking-wider text-slate-400 border-b border-slate-200 whitespace-nowrap">
                Attributes
              </th>
              {headers.map((header) => (
                <th key={header} className="px-4 py-3 text-right text-[11px] font-bold uppercase tracking-wider text-slate-500 border-b border-slate-200 whitespace-nowrap">
                  {header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="bg-white">
            {data.map((row, idx) => (
              <tr key={idx} className="hover:bg-slate-50/80 transition-colors group">
                {/* Sticky Row Label */}
                <td className="sticky left-0 z-10 bg-white group-hover:bg-slate-50 py-3 pl-6 pr-4 text-[13px] font-medium text-slate-700 border-b border-slate-100 whitespace-nowrap border-r border-slate-50 shadow-[2px_0_5px_-2px_rgba(0,0,0,0.05)]">
                  <span className="flex items-center gap-1.5 cursor-pointer hover:text-indigo-600 transition-colors">
                    {row.metric} 
                    <span className="text-indigo-400 text-[10px] font-bold group-hover:scale-125 transition-transform">+</span>
                  </span>
                </td>
                {/* Numerical Data */}
                {headers.map((header) => (
                  <td key={header} className="px-4 py-3 text-[13px] text-right text-slate-600 border-b border-slate-100 tabular-nums whitespace-nowrap">
                    {row.values[header] !== undefined && row.values[header] !== null 
                      ? row.values[header].toLocaleString('en-IN') 
                      : "—"}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default FinancialTable;