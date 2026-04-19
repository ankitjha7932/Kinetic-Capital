import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import api from "../api/axios";
import PositionsList from "../components/PositionsList";
import MarketMoversGrid from "../components/MarketMoversGrid";
import ReturnLeaders from "../components/ReturnLeaders";
import { Activity, TrendingUp, Target, Loader2, ArrowUpRight, ArrowDownRight } from "lucide-react";

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
    } catch (_) {}
  };

  const fetchPortfolio = async () => {
    if (!userId) return;
    try {
      const [sum, ana] = await Promise.all([
        api.get(`/portfolio/summary/${userId}`),
        api.get(`/portfolio/analysis?userId=${userId}`),
      ]);
      setData({ summary: sum.data, analysis: ana.data });
    } catch (_) {}
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

  if (loading)
    return (
      <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "60vh" }}>
        <Loader2 size={32} style={{ color: "#4f46e5", animation: "spin 1s linear infinite" }} />
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    );

  const s = data.summary;
  
  // Calculate P&L Percentage
  const pnlPct = s?.totalInvested > 0 
    ? (s.totalPnl / s.totalInvested) * 100 
    : 0;

  return (
    <div style={{
      padding: "clamp(16px, 4vw, 32px)",
      maxWidth: 1400,
      margin: "0 auto",
      backgroundColor: "#fcfcfd",
      minHeight: "100vh"
    }}>

      {/* ── 1. STATS ROW ── */}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
        gap: 16,
        marginBottom: 32
      }}>
        <StatCard
          label="Invested"
          value={`₹${s?.totalInvested?.toLocaleString("en-IN") ?? "0"}`}
          icon={<Target size={18} />}
        />
        <StatCard
          label="Current value"
          value={`₹${s?.currentValue?.toLocaleString("en-IN") ?? "0"}`}
          icon={<Activity size={18} />}
        />
        <StatCard
          label="Total P&L"
          value={`₹${s?.totalPnl?.toLocaleString("en-IN") ?? "0"}`}
          percentage={pnlPct}
          icon={s?.totalPnl >= 0 ? <ArrowUpRight size={18} /> : <ArrowDownRight size={18} />}
          accent={s?.totalPnl >= 0 ? "green" : "red"}
        />
      </div>

      {/* ── 2. MARKET SECTION ── */}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(400px, 1fr))",
        gap: 24,
        marginBottom: 32
      }}>
        <SectionCard>
          <ReturnLeaders
            data={marketData}
            selectedIndex={selectedIndex}
            onIndexChange={handleIndexChange}
            onSelectStock={(sym) => navigate(`/stock/${sym}`)}
          />
        </SectionCard>

        <SectionCard>
          <MarketMoversGrid
            data={marketData}
            onSelectStock={(sym) => navigate(`/stock/${sym}`)}
          />
        </SectionCard>
      </div>

      {/* ── 3. POSITIONS ── */}
      <div style={{ background: "#fff", border: "1px solid #f1f5f9", borderRadius: 24, padding: "8px" }}>
        <PositionsList
          positions={data.analysis?.positions || []}
          onRefresh={fetchPortfolio}
          onSelectStock={(symbol) => navigate(`/stock/${symbol}`)}
        />
      </div>
    </div>
  );
}

/* ── Sub-components ── */

function SectionCard({ children }) {
  return (
    <div style={{
      background: "#fff",
      border: "1px solid #f1f5f9",
      borderRadius: 24,
      padding: "20px",
      width: "100%",
      boxShadow: "0 1px 3px rgba(0,0,0,0.02)"
    }}>
      {children}
    </div>
  );
}

function StatCard({ label, value, icon, percentage, accent }) {
  const isPositive = accent === "green";
  const isNegative = accent === "red";
  
  const valueColor = isPositive ? "#10b981" : isNegative ? "#ef4444" : "#0f172a";
  const iconBg = isPositive ? "#ecfdf5" : isNegative ? "#fef2f2" : "#f5f7ff";
  const iconColor = isPositive ? "#10b981" : isNegative ? "#ef4444" : "#4f46e5";

  return (
    <div style={{
      background: "#fff",
      border: "1px solid #f1f5f9",
      borderRadius: 20,
      padding: "20px",
      display: "flex",
      justifyContent: "space-between",
      alignItems: "center",
      transition: "transform 0.2s ease, box-shadow 0.2s ease",
      cursor: "default"
    }}
    onMouseEnter={e => {
      e.currentTarget.style.transform = "translateY(-2px)";
      e.currentTarget.style.boxShadow = "0 10px 20px rgba(0,0,0,0.04)";
    }}
    onMouseLeave={e => {
      e.currentTarget.style.transform = "none";
      e.currentTarget.style.boxShadow = "none";
    }}
    >
      <div>
        <div style={{
          fontSize: 11,
          fontWeight: 700,
          color: "#94a3b8",
          textTransform: "uppercase",
          letterSpacing: "0.8px",
          marginBottom: 6
        }}>
          {label}
        </div>

        <div style={{ display: "flex", alignItems: "baseline", gap: 6 }}>
          <span style={{
            fontSize: "clamp(18px, 2.2vw, 24px)",
            fontWeight: 800,
            color: valueColor,
            letterSpacing: "-0.5px"
          }}>
            {value}
          </span>
          
          {percentage !== undefined && (
            <span style={{
              fontSize: 13,
              fontWeight: 700,
              color: valueColor,
              opacity: 0.85
            }}>
              ({percentage >= 0 ? "+" : ""}{percentage.toFixed(2)}%)
            </span>
          )}
        </div>
      </div>

      <div style={{
        width: 42,
        height: 42,
        borderRadius: 12,
        background: iconBg,
        color: iconColor,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        flexShrink: 0,
        transition: "transform 0.3s ease"
      }}>
        {icon}
      </div>
    </div>
  );
}