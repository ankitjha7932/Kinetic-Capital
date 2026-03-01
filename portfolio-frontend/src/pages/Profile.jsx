import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { User, Shield, Target, Save } from 'lucide-react';

export default function Profile({ userId }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    // In a real app, you'd have a GET /api/auth/me or similar
    // For now, let's fetch summary or assume we have user data
    const fetchUser = async () => {
      try {
        const res = await api.get(`/portfolio/summary/${userId}`);
        setUser({
          riskProfile: "Moderate", // Defaulting as example
          investmentHorizon: 5,
          preferredSectors: "IT, Finance"
        });
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
      }
    };
    fetchUser();
  }, [userId]);

  const handleSave = async () => {
    setSaving(true);
    // Logic to call your backend update route would go here
    setTimeout(() => {
      setSaving(false);
      alert("Profile updated locally!");
    }, 800);
  };

  if (loading) return <div className="p-8 text-center">Loading Profile...</div>;

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
        <div className="bg-indigo-600 p-8 text-white">
          <div className="flex items-center gap-4">
            <div className="w-20 h-20 bg-white/20 rounded-2xl flex items-center justify-center backdrop-blur-md">
              <User size={40} />
            </div>
            <div>
              <h2 className="text-3xl font-bold">Investor Profile</h2>
              <p className="opacity-80 text-sm">Manage your investment DNA</p>
            </div>
          </div>
        </div>

        <div className="p-8 grid grid-cols-1 md:grid-cols-2 gap-8">
          {/* Risk Profile */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-bold text-slate-600 uppercase tracking-wider">
              <Shield size={16} /> Risk Appetite
            </label>
            <select 
              value={user.riskProfile}
              onChange={(e) => setUser({...user, riskProfile: e.target.value})}
              className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-indigo-500 transition-all"
            >
              <option value="Low">Low (Conservative)</option>
              <option value="Moderate">Moderate (Balanced)</option>
              <option value="High">High (Aggressive)</option>
            </select>
          </div>

          {/* Investment Horizon */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-bold text-slate-600 uppercase tracking-wider">
              <Target size={16} /> Horizon (Years)
            </label>
            <input 
              type="number" 
              value={user.investmentHorizon}
              onChange={(e) => setUser({...user, investmentHorizon: e.target.value})}
              className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-indigo-500 transition-all"
            />
          </div>

          <div className="md:col-span-2 space-y-2">
            <label className="text-sm font-bold text-slate-600 uppercase tracking-wider">Preferred Sectors</label>
            <input 
              type="text" 
              value={user.preferredSectors}
              placeholder="e.g. IT, Energy, FMCG"
              onChange={(e) => setUser({...user, preferredSectors: e.target.value})}
              className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-indigo-500 transition-all"
            />
          </div>
        </div>

        <div className="p-8 bg-slate-50 border-t flex justify-end">
          <button 
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 bg-indigo-600 text-white px-8 py-3 rounded-xl font-bold hover:bg-indigo-700 shadow-lg shadow-indigo-100 transition-all active:scale-95 disabled:opacity-50"
          >
            <Save size={18} />
            {saving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      </div>
    </div>
  );
}