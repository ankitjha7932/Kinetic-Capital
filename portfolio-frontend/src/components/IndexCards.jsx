// src/components/IndexCards.jsx
// Shows the top 6 indices as clickable cards with live price + sparkline.

import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { TrendingUp, TrendingDown, Loader2 } from "lucide-react";
import api from "../api/axios";
import { indexToSlug, TOP_6_INDICES } from "../pages/IndexDetailView";

/* ── Minimal SVG sparkline (no extra dependency) ── */
const Spark = ({ data = [], color }) => {
    if (!data || data.length < 2) return null;
    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;
    const W = 56, H = 26;
    const pts = data
        .map((v, i) => `${(i / (data.length - 1)) * W},${H - ((v - min) / range) * H}`)
        .join(" ");
    return (
        <svg width={W} height={H} viewBox={`0 0 ${W} ${H}`} style={{ overflow: "visible", flexShrink: 0 }}>
            <polyline
                points={pts}
                fill="none"
                stroke={color}
                strokeWidth="1.6"
                strokeLinejoin="round"
                strokeLinecap="round"
            />
        </svg>
    );
};

/* ── Short display labels for narrow cards ── */
const SHORT_LABEL = {
    "NIFTY 50": "NIFTY 50",
    "BSE SENSEX": "SENSEX",
    "NIFTY BANK": "BANKNIFTY",
    "NIFTY MIDCAP SELECT": "MIDCAP SEL",
    "NIFTY FINANCIAL SER": "FIN SER",
    "BSE BANKEX": "BANKEX",
};

export default function IndexCards() {
    const navigate = useNavigate();
    const [indicesData, setIndicesData] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        (async () => {
            try {
                const results = await Promise.allSettled(
                    TOP_6_INDICES.map((name) =>
                        api
                            .get(`/index/chart?name=${encodeURIComponent(name)}&range=1d`)
                            .then((r) => ({ name, ...(r.data || {}) }))
                            .catch(() => ({ name, success: false }))
                    )
                );
                setIndicesData(
                    results.map((r) => (r.status === "fulfilled" ? r.value : { name: "", success: false }))
                );
            } catch (_) { }
            finally { setLoading(false); }
        })();
    }, []);

    if (loading) {
        return (
            <div style={{ padding: "12px 0", display: "flex", justifyContent: "center" }}>
                <Loader2 size={18} style={{ animation: "spin 0.8s linear infinite", color: "#4f46e5" }} />
                <style>{`@keyframes spin{to{transform:rotate(360deg);}}`}</style>
            </div>
        );
    }

    return (
        <>
            <style>{`
        .idx-cards-grid {
          display: grid;
          grid-template-columns: repeat(6, 1fr);
          gap: 10px;
          margin-bottom: 20px;
        }
        @media (max-width: 1100px) { .idx-cards-grid { grid-template-columns: repeat(3, 1fr); } }
        @media (max-width: 640px)  { .idx-cards-grid { grid-template-columns: repeat(2, 1fr); } }

        .idx-card-item {
          background: #fff;
          border: 1px solid #f1f5f9;
          border-radius: 14px;
          padding: 12px 13px;
          cursor: pointer;
          transition: transform 0.15s, box-shadow 0.15s, border-color 0.15s;
          box-sizing: border-box;
          overflow: hidden;
          display: flex;
          flex-direction: column;
          gap: 6px;
          min-width: 0;
        }
        .idx-card-item:hover {
          transform: translateY(-2px);
          box-shadow: 0 6px 18px rgba(0,0,0,0.07);
          border-color: #e0e7ff;
        }
        .idx-card-item:active { transform: translateY(0); }
        @keyframes spin{to{transform:rotate(360deg);}}
      `}</style>

            <div className="idx-cards-grid">
                {indicesData.map(({ name, success, chartData, stats }) => {
                    const prices = (chartData || []).map((p) => p.price).filter(Boolean);
                    const isUp = (stats?.dayChangePct ?? 0) >= 0;
                    const color = isUp ? "#10b981" : "#ef4444";
                    const label = SHORT_LABEL[name] || name;

                    return (
                        <div
                            key={name}
                            className="idx-card-item"
                            onClick={() => navigate(`/index/${indexToSlug(name)}`)}
                        >
                            {/* Label */}
                            <div style={{
                                fontSize: 9, fontWeight: 800, color: "#64748b",
                                textTransform: "uppercase", letterSpacing: "0.06em",
                                lineHeight: 1.2, whiteSpace: "nowrap", overflow: "hidden",
                                textOverflow: "ellipsis",
                            }}>
                                {label}
                            </div>

                            {/* Price */}
                            {success && stats ? (
                                <>
                                    <div style={{
                                        fontSize: "clamp(13px,1.4vw,17px)", fontWeight: 900,
                                        color: "#0f172a", letterSpacing: "-0.4px", lineHeight: 1,
                                    }}>
                                        {stats.currentPrice?.toLocaleString("en-IN", { minimumFractionDigits: 2 })}
                                    </div>

                                    {/* Change + sparkline */}
                                    <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", gap: 4 }}>
                                        <div style={{
                                            display: "flex", alignItems: "center", gap: 3,
                                            color, fontSize: 10, fontWeight: 700, flexShrink: 0,
                                        }}>
                                            {isUp ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
                                            {isUp ? "+" : ""}{stats.dayChangePct?.toFixed(2)}%
                                        </div>
                                        <Spark data={prices.slice(-20)} color={color} />
                                    </div>
                                </>
                            ) : (
                                <div style={{ fontSize: 10, color: "#94a3b8" }}>—</div>
                            )}
                        </div>
                    );
                })}
            </div>
        </>
    );
}