// src/pages/Dashboard.jsx
// Only change from your original:
//   1. Import IndexCards
//   2. Add the "Market Indices" section-card above .market-grid

import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import api from "../api/axios";
import PositionsList from "../components/PositionsList";
import MarketMoversGrid from "../components/MarketMoversGrid";
import ReturnLeaders from "../components/ReturnLeaders";
import IndexCards from "../components/IndexCards";                    // ← NEW
import { Activity, Target, ArrowUpRight, ArrowDownRight, Loader2 } from "lucide-react";

const DEFAULT_INDEX = "NIFTY 100";

export default function Dashboard({ userId }) {
  const [data, setData] = useState({ summary: null, analysis: null });
  const [marketData, setMarketData] = useState(null);
  const [selectedIndex, setSelectedIndex] = useState(DEFAULT_INDEX);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  const fetchMarket = async (index) => {
    try {
      const res = await api.get(`Portfolio/index-movers?index=${encodeURIComponent(index)}`);
      setMarketData(res.data.data || res.data);
    } catch (_) { }
  };

  const fetchPortfolio = async () => {
    if (!userId) return;
    try {
      const [sum, ana] = await Promise.all([
        api.get(`/portfolio/summary/${userId}`),
        api.get(`/portfolio/analysis?userId=${userId}`),
      ]);
      setData({ summary: sum.data, analysis: ana.data });
    } catch (_) { }
  };

  useEffect(() => {
    (async () => {
      setLoading(true);
      await Promise.all([fetchPortfolio(), fetchMarket(DEFAULT_INDEX)]);
      setLoading(false);
    })();
  }, [userId]);

  const handleIndexChange = (index) => {
    setSelectedIndex(index);
    fetchMarket(index);
  };

  if (loading) return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "60vh" }}>
      <div style={{ textAlign: "center" }}>
        <div style={{
          width: 36, height: 36, border: "2px solid #e0e7ff",
          borderTopColor: "#4f46e5", borderRadius: "50%",
          animation: "spin 0.8s linear infinite", margin: "0 auto 12px",
        }} />
        <div style={{
          fontSize: 10, fontWeight: 700, color: "#94a3b8",
          textTransform: "uppercase", letterSpacing: "0.12em",
        }}>Loading</div>
        <style>{`@keyframes spin{to{transform:rotate(360deg);}}`}</style>
      </div>
    </div>
  );

  const s = data.summary;
  const pnlUp = (s?.totalPnl ?? 0) >= 0;
  const pnlPct = s?.totalInvested > 0 ? (s.totalPnl / s.totalInvested) * 100 : 0;

  return (
    <>
      <style>{`
        /* ─── ROOT ─────────────────────────────────── */
        .dash-root {
          padding: clamp(12px, 4vw, 32px);
          max-width: 1400px;
          margin: 0 auto;
          background: #fcfcfd;
          min-height: 100vh;
          box-sizing: border-box;
        }

        /* ─── STAT CARDS ───────────────────────────── */
        .stat-grid {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 12px;
          margin-bottom: 20px;
        }
        @media (max-width: 600px) {
          .stat-grid { grid-template-columns: 1fr; }
        }

        .stat-card {
          background: #fff;
          border: 1px solid #f1f5f9;
          border-radius: 16px;
          padding: 16px 18px;
          display: flex;
          justify-content: space-between;
          align-items: center;
          box-shadow: 0 1px 3px rgba(0,0,0,0.02);
          transition: transform 0.2s, box-shadow 0.2s;
          box-sizing: border-box;
        }
        .stat-card:hover {
          transform: translateY(-2px);
          box-shadow: 0 8px 20px rgba(0,0,0,0.06);
        }
        .stat-label {
          font-size: 10px; font-weight: 700; color: #94a3b8;
          text-transform: uppercase; letter-spacing: 0.08em; margin-bottom: 5px;
        }
        .stat-value {
          font-size: clamp(16px, 2.4vw, 22px);
          font-weight: 800; letter-spacing: -0.5px; line-height: 1;
        }
        .stat-pct {
          font-size: 12px; font-weight: 700; opacity: 0.85; margin-top: 4px;
        }
        .stat-icon {
          width: 38px; height: 38px; border-radius: 11px; flex-shrink: 0;
          display: flex; align-items: center; justify-content: center;
          margin-left: 12px;
        }

        /* ─── MARKET 2-COLUMN ──────────────────────── */
        .market-grid {
          display: grid;
          grid-template-columns: repeat(2, 1fr);
          gap: 16px;
          margin-bottom: 20px;
        }
        @media (max-width: 860px) {
          .market-grid { grid-template-columns: 1fr; }
        }

        .section-card {
          background: #fff;
          border: 1px solid #f1f5f9;
          border-radius: 20px;
          padding: 18px 16px;
          box-shadow: 0 1px 4px rgba(0,0,0,0.03);
          box-sizing: border-box;
          overflow: hidden;
          min-width: 0;
        }

        /* ─── POSITIONS ────────────────────────────── */
        .positions-wrap {
          background: #fff;
          border: 1px solid #f1f5f9;
          border-radius: 20px;
          padding: 4px 8px;
          overflow: hidden;
          box-sizing: border-box;
        }
        @media (max-width: 600px) {
          .positions-wrap { padding: 4px 0; border-radius: 14px; }
        }

        /* ─── INDEX SECTION ────────────────────────── */
        .indices-section {
          background: #fff;
          border: 1px solid #f1f5f9;
          border-radius: 20px;
          padding: 14px 16px 6px;
          box-shadow: 0 1px 4px rgba(0,0,0,0.03);
          box-sizing: border-box;
          margin-bottom: 20px;
        }
        .indices-section-label {
          font-size: 9px;
          font-weight: 800;
          color: #94a3b8;
          text-transform: uppercase;
          letter-spacing: 0.12em;
          margin-bottom: 12px;
        }
      `}</style>

      <div className="dash-root">

        {/* ── STATS ─────────────────────────────────────────────── */}
        <div className="stat-grid">
          <div className="stat-card">
            <div style={{ minWidth: 0, flex: 1 }}>
              <div className="stat-label">Invested</div>
              <div className="stat-value" style={{ color: "#0f172a" }}>
                ₹{s?.totalInvested?.toLocaleString("en-IN") ?? "0"}
              </div>
            </div>
            <div className="stat-icon" style={{ background: "#f0f4ff", color: "#4f46e5" }}>
              <Target size={16} />
            </div>
          </div>

          <div className="stat-card">
            <div style={{ minWidth: 0, flex: 1 }}>
              <div className="stat-label">Current value</div>
              <div className="stat-value" style={{ color: "#0f172a" }}>
                ₹{s?.currentValue?.toLocaleString("en-IN") ?? "0"}
              </div>
            </div>
            <div className="stat-icon" style={{ background: "#f0f9ff", color: "#0ea5e9" }}>
              <Activity size={16} />
            </div>
          </div>

          <div className="stat-card">
            <div style={{ minWidth: 0, flex: 1 }}>
              <div className="stat-label">Total P&amp;L</div>
              <div className="stat-value" style={{ color: pnlUp ? "#10b981" : "#ef4444" }}>
                {pnlUp ? "+" : ""}₹{Math.abs(s?.totalPnl ?? 0).toLocaleString("en-IN")}
              </div>
              <div className="stat-pct" style={{ color: pnlUp ? "#10b981" : "#ef4444" }}>
                {pnlUp ? "+" : ""}{pnlPct.toFixed(2)}%
              </div>
            </div>
            <div className="stat-icon" style={{
              background: pnlUp ? "#ecfdf5" : "#fff1f2",
              color: pnlUp ? "#10b981" : "#ef4444",
            }}>
              {pnlUp ? <ArrowUpRight size={16} /> : <ArrowDownRight size={16} />}
            </div>
          </div>
        </div>

        {/* ── INDEX CARDS (NEW) ──────────────────────────────────── */}
        <div className="indices-section">
          <div className="indices-section-label">Market Indices</div>
          <IndexCards />
        </div>

        {/* ── MARKET WIDGETS ────────────────────────────────────── */}
        <div className="market-grid">
          <div className="section-card">
            <ReturnLeaders
              data={marketData}
              selectedIndex={selectedIndex}
              onIndexChange={handleIndexChange}
              onSelectStock={(sym) => navigate(`/stock/${sym}`)}
            />
          </div>
          <div className="section-card">
            <MarketMoversGrid
              data={marketData}
              onSelectStock={(sym) => navigate(`/stock/${sym}`)}
            />
          </div>
        </div>

        {/* ── POSITIONS ─────────────────────────────────────────── */}
        <div className="positions-wrap">
          <PositionsList
            positions={data.analysis?.positions || []}
            onRefresh={fetchPortfolio}
            onSelectStock={(sym) => navigate(`/stock/${sym}`)}
          />
        </div>
      </div>
    </>
  );
}