import React, { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import api from "../api/axios";
import { INDUSTRIES } from "../constants/industries"; 
import {
  ArrowLeft, User, ShieldCheck, Target, 
  TrendingUp, Save, CheckCircle2, 
  Layers, Search, X, Activity, UserCircle
} from "lucide-react";

export default function Profile({ userId }) {
  const navigate = useNavigate();
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  
  // New state to prevent the "typing in header" effect
  const [displayName, setDisplayName] = useState("");

  const [user, setUser] = useState({
    fullName: "", 
    email: "",
    riskProfile: "Moderate",
    investmentHorizon: 5,
    sectors: [],
  });

  const suggestions = useMemo(() => {
    if (!query.trim()) return [];
    return INDUSTRIES.filter(
      (s) => s.toLowerCase().includes(query.toLowerCase()) && !user.sectors.includes(s)
    ).slice(0, 6);
  }, [query, user.sectors]);

  useEffect(() => {
    const fetchLiveProfile = async () => {
      setLoading(true);
      try {
        const res = await api.get(`/user/profile/${userId}`);
        const data = res.data;

        const profileData = {
          fullName: data.fullName || "",
          email: data.email || "",
          riskProfile: data.riskProfile || "Moderate",
          investmentHorizon: data.investmentHorizon || 5,
          sectors: data.preferredSectors ? data.preferredSectors.split(",").filter(s => s) : [],
        };

        setUser(profileData);
        setDisplayName(data.fullName || "Private Investor"); // Set header name only on load
      } catch (err) {
        console.error("Critical: Could not sync persona", err);
      } finally {
        setLoading(false);
      }
    };

    if (userId) fetchLiveProfile();
  }, [userId]);

  const addSector = (sector) => {
    setUser((prev) => ({ ...prev, sectors: [...prev.sectors, sector] }));
    setQuery("");
  };

  const removeSector = (sector) => {
    setUser((prev) => ({ ...prev, sectors: prev.sectors.filter((s) => s !== sector) }));
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await api.put(`/user/profile/${userId}`, {
        FullName: user.fullName,
        RiskProfile: user.riskProfile,
        InvestmentHorizon: parseInt(user.investmentHorizon),
        PreferredSectors: user.sectors.join(","),
      });
      
      setDisplayName(user.fullName); // Update header name only after successful DB save
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      console.error("Update failed", err);
    } finally {
      setSaving(false);
    }
  };

  if (loading) return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50">
      <Activity size={24} className="text-indigo-600 animate-spin" />
    </div>
  );

  return (
    <div className="min-h-screen bg-slate-50 p-4 md:p-8 font-sans">
      <div className="max-w-4xl mx-auto">
        
        <button onClick={() => navigate("/")} className="flex items-center gap-2 text-slate-400 hover:text-indigo-600 font-bold mb-6 group transition-all text-sm">
          <ArrowLeft size={16} className="group-hover:-translate-x-1 transition-transform" />
          Return to Dashboard
        </button>

        <div className="bg-white rounded-[2.5rem] shadow-xl shadow-slate-200 border border-white overflow-hidden">
          
          {/* COMPACT HEADER */}
          <div className="bg-slate-900 p-8 text-white relative overflow-hidden">
            <div className="relative z-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-indigo-500/20 backdrop-blur-md border border-white/10 flex items-center justify-center shadow-lg">
                <User size={28} className="text-indigo-400" />
              </div>
              <div>
                <h1 className="text-2xl font-black tracking-tight">{displayName}</h1>
                <p className="text-indigo-300 font-bold uppercase text-[9px] tracking-[0.2em] mt-0.5">
                  {user.email || "Verified Strategy Account"}
                </p>
              </div>
            </div>
            <div className="absolute top-0 right-0 w-48 h-48 bg-indigo-500/10 rounded-full -translate-y-1/2 translate-x-1/2 blur-3xl" />
          </div>

          <div className="p-8 space-y-10">
            
            {/* NAME FIELD SECTION */}
            <div className="pb-6 border-b border-slate-100">
               <FieldWrapper label="Personal Identity" icon={<UserCircle size={14} />}>
                <input
                  type="text"
                  placeholder="Enter your full name"
                  value={user.fullName}
                  onChange={(e) => setUser({ ...user, fullName: e.target.value })}
                  className="w-full bg-slate-50 border-none rounded-xl px-5 py-4 font-bold text-lg text-slate-800 focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-300 transition-all shadow-sm"
                />
              </FieldWrapper>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">
              <FieldWrapper label="Risk Profile" icon={<ShieldCheck size={14} />}>
                <select
                  value={user.riskProfile}
                  onChange={(e) => setUser({ ...user, riskProfile: e.target.value })}
                  className="w-full bg-slate-50 border-none rounded-xl px-4 py-3.5 font-bold text-slate-800 focus:ring-2 focus:ring-indigo-500 appearance-none cursor-pointer text-sm"
                >
                  <option>Conservative</option>
                  <option>Moderate</option>
                  <option>Aggressive</option>
                </select>
              </FieldWrapper>

              <FieldWrapper label="Horizon (Years)" icon={<TrendingUp size={14} />}>
                <input
                  type="number"
                  value={user.investmentHorizon}
                  onChange={(e) => setUser({ ...user, investmentHorizon: e.target.value })}
                  className="w-full bg-slate-50 border-none rounded-xl px-4 py-3.5 font-bold text-slate-800 focus:ring-2 focus:ring-indigo-500 text-sm"
                />
              </FieldWrapper>
            </div>

            <div className="pt-6 border-t border-slate-100">
              <div className="flex items-center gap-2 mb-4">
                <Layers className="text-indigo-500" size={14} />
                <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Preferred Sectors</label>
              </div>

              <div className="relative mb-4">
                <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" size={16} />
                <input 
                  type="text"
                  placeholder="Search industries..."
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  className="w-full bg-slate-50 border-none rounded-xl pl-12 pr-4 py-3.5 font-bold text-slate-700 focus:ring-2 focus:ring-indigo-500 text-sm shadow-inner"
                />
                {suggestions.length > 0 && (
                  <div className="absolute z-50 w-full mt-2 bg-white rounded-2xl shadow-2xl border border-slate-100 overflow-hidden">
                    {suggestions.map(s => (
                      <div key={s} onClick={() => addSector(s)} className="px-5 py-3 hover:bg-indigo-600 hover:text-white cursor-pointer font-bold text-xs text-slate-600 transition-colors">{s}</div>
                    ))}
                  </div>
                )}
              </div>

              <div className="flex flex-wrap gap-2">
                {user.sectors.map((sector) => (
                  <span key={sector} className="flex items-center gap-2 bg-indigo-50 text-indigo-600 pl-3 pr-1 py-1.5 rounded-lg text-[10px] font-black tracking-wide border border-indigo-100">
                    {sector.toUpperCase()}
                    <button onClick={() => removeSector(sector)} className="p-1 hover:text-indigo-800 transition-colors"><X size={12} /></button>
                  </span>
                ))}
              </div>
            </div>
          </div>

          <div className="p-8 bg-slate-50 border-t flex justify-end items-center gap-4">
            {saved && <span className="text-emerald-600 font-bold text-[11px] uppercase flex items-center gap-1.5"><CheckCircle2 size={14} /> Persona Synced</span>}
            <button onClick={handleSave} disabled={saving} className="flex items-center gap-2 px-8 py-3.5 bg-indigo-600 text-white font-black rounded-xl shadow-lg hover:bg-indigo-700 transition-all active:scale-95 disabled:opacity-50 text-xs uppercase tracking-widest">
              <Save size={16} /> {saving ? "Syncing..." : "Commit Changes"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

const FieldWrapper = ({ label, icon, children }) => (
  <div className="space-y-2">
    <div className="flex items-center gap-2 ml-0.5">
      <div className="text-indigo-500">{icon}</div>
      <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">{label}</label>
    </div>
    {children}
  </div>
);