import React, { useEffect, useState } from 'react';
import api from '../api/axios';
import PositionsList from '../components/PositionsList';
import StockDetailView from '../components/StockDetailView'; // Ensure this exists
import { TrendingUp, TrendingDown, Activity, Lightbulb, AlertCircle } from 'lucide-react';

export default function Dashboard({ userId }) {
  const [data, setData] = useState({ summary: null, analysis: null, suggestions: [] });
  const [loading, setLoading] = useState(true);
  // Minimal Change: Added state for stock selection
  const [viewingStock, setViewingStock] = useState(null);

  const fetchData = async () => {
    if (!userId || userId === "undefined" || userId === "") return;
    setLoading(true);
    try {
      const [sum, ana, sug] = await Promise.all([
        api.get(`/portfolio/summary/${userId}`),
        api.get(`/portfolio/analysis?userId=${userId}`),
        api.get(`/portfolio/suggestions?userId=${userId}`)
      ]);
      setData({ summary: sum.data, analysis: ana.data, suggestions: sug.data || [] });
    } catch (err) {
      console.error("Fetch error:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (userId && userId !== "undefined") {
      fetchData();
    }
  }, [userId]);

  // Minimal Change: Render Detail View if a stock is selected
  if (viewingStock) {
    return <StockDetailView symbol={viewingStock} onBack={() => setViewingStock(null)} />;
  }

  if (loading) return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600"></div>
    </div>
  );

  return (
    <div className="p-8 max-w-7xl mx-auto space-y-8 animate-in fade-in duration-700">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <StatCard title="Invested" value={`₹${(data.summary?.totalInvested || 0).toLocaleString()}`} icon={<Activity size={20} className="text-blue-500"/>} />
        <StatCard title="Current" value={`₹${(data.summary?.currentValue || 0).toLocaleString()}`} icon={<TrendingUp size={20} className="text-indigo-500"/>} />
        <StatCard title="Total P&L" value={`₹${(data.summary?.totalPnl || 0).toLocaleString()}`} isLoss={(data.summary?.totalPnl || 0) < 0} icon={(data.summary?.totalPnl || 0) >= 0 ? <TrendingUp size={20} className="text-green-500"/> : <TrendingDown size={20} className="text-red-500"/>} />
        <div className="bg-indigo-600 p-6 rounded-2xl text-white shadow-lg shadow-indigo-200">
          <p className="text-xs opacity-80 uppercase font-bold tracking-wider">Portfolio Score</p>
          <p className="text-4xl font-black">{data.analysis?.score || 0}</p>
          <p className="text-sm font-medium">{data.analysis?.ratingBand || 'Neutral'} Health</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div className="lg:col-span-2 bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
          <h3 className="text-xl font-bold mb-6 flex items-center gap-2">
            <Lightbulb className="text-yellow-500" /> AI Recommendations
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {(data.suggestions || []).length > 0 ? (
              data.suggestions.map((s, i) => (
                <div key={i} className="p-4 bg-slate-50 rounded-xl border border-slate-100 border-l-4 border-l-indigo-500">
                  <p className="font-black text-indigo-700">{s.symbol}</p>
                  <p className="text-sm text-slate-600 my-2 leading-relaxed">{s.rationale}</p>
                  <div className="text-[10px] font-bold uppercase text-slate-400">Suggested: {s.suggestedAllocationPercent}%</div>
                </div>
              ))
            ) : (
              <p className="text-slate-400 text-sm italic">No suggestions available yet.</p>
            )}
          </div>
        </div>

        <div className="bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
          <h3 className="text-xl font-bold mb-6 flex items-center gap-2">
            <AlertCircle className="text-red-500" /> Health Warnings
          </h3>
          <div className="space-y-3">
            {(data.analysis?.warnings || []).length > 0 ? (
              data.analysis.warnings.map((w, i) => (
                <div key={i} className="p-3 bg-red-50 text-red-700 rounded-xl text-xs font-medium border border-red-100">
                  • {w}
                </div>
              ))
            ) : (
              <p className="text-slate-400 text-xs italic">Your portfolio looks healthy!</p>
            )}
          </div>
        </div>
      </div>

      {/* Minimal Change: Passed onSelectStock handler */}
      <PositionsList 
        holdings={data.summary?.holdings || []} 
        onRefresh={fetchData} 
        onSelectStock={(symbol) => setViewingStock(symbol)}
      />
    </div>
  );
}

function StatCard({ title, value, icon, isLoss }) {
  return (
    <div className="bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
      <div className="flex justify-between items-start">
        <div>
          <p className="text-xs text-slate-400 uppercase font-bold tracking-widest mb-1">{title}</p>
          <p className={`text-2xl font-black ${isLoss ? 'text-red-600' : 'text-slate-800'}`}>{value}</p>
        </div>
        <div className="p-2 bg-slate-50 rounded-xl">{icon}</div>
      </div>
    </div>
  );
}