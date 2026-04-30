// src/pages/IndexDetailView.jsx
import React, { useEffect, useState, useCallback, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  ComposedChart, Area, Line, XAxis, YAxis,
  CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import { ArrowLeft, TrendingUp, TrendingDown, ChevronLeft, ChevronRight, Loader2 } from "lucide-react";
import api from "../api/axios";

/* ─── Data constants ─────────────────────────────────────────── */
const RANGES = ["1D", "1W", "1M", "3M", "6M", "1Y", "3Y", "5Y", "MAX"];
const PAGE_SIZE = 8;

export const TOP_6_INDICES = [
  "NIFTY 50", "BSE SENSEX", "NIFTY BANK",
  "NIFTY MIDCAP SELECT", "NIFTY FINANCIAL SER", "NIFTY PHARMA",
];

export const ALL_INDICES = [
  "NIFTY 50", "NIFTY BANK", "NIFTY FINANCIAL SER", "BSE SENSEX",
  "NIFTY MIDCAP SELECT", "INDIA VIX", "NIFTY TOTAL MARKET",
  "NIFTY NEXT 50", "NIFTY 100", "NIFTY MIDCAP 100", "BSE 100",
  "NIFTY 500", "NIFTY AUTO", "NIFTY SMALLCAP 100", "NIFTY FMCG",
  "NIFTY METAL", "NIFTY PHARMA", "NIFTY PSU BANK", "NIFTY IT",
  "BSE SMALLCAP", "NIFTY SMALLCAP 250", "NIFTY MIDCAP 150",
  "NIFTY COMMODITIES", "BSE IPO",
];

/* ─── Slug helpers ───────────────────────────────────────────── */
export const indexToSlug = (name) =>
  name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");

export const slugToIndex = (slug) =>
  ALL_INDICES.find((i) => indexToSlug(i) === slug.toLowerCase()) ||
  slug.toUpperCase().replace(/-/g, " ");

/* ─── Helpers ────────────────────────────────────────────────── */
const toTitleCase = (str) =>
  str.toLowerCase().replace(/\b\w/g, (c) => c.toUpperCase());

const fmtINR = (val, dec = 2) => {
  if (val == null || val === 0) return "—";
  return Number(val).toLocaleString("en-IN", {
    minimumFractionDigits: dec,
    maximumFractionDigits: dec,
  });
};

const fmtMarketCap = (raw) => {
  if (!raw || raw === "N/A") return "N/A";
  const cleaned = raw.toString().replace(/,/g, "").replace(/Cr/i, "").trim();
  const num = parseFloat(cleaned);
  if (isNaN(num)) return raw;
  return `₹${num.toLocaleString("en-IN", { maximumFractionDigits: 0 })} Cr`;
};

/* ─── Index color palette ────────────────────────────────────── */
const INDEX_ACCENT = {
  "NIFTY 50":            "#4f46e5",
  "BSE SENSEX":          "#0ea5e9",
  "NIFTY BANK":          "#7c3aed",
  "NIFTY FINANCIAL SER": "#0891b2",
  "NIFTY MIDCAP SELECT": "#f59e0b",
  "NIFTY IT":            "#06b6d4",
  "NIFTY PHARMA":        "#10b981",
  "NIFTY AUTO":          "#f97316",
  "NIFTY FMCG":          "#84cc16",
  "NIFTY METAL":         "#64748b",
  "NIFTY PSU BANK":      "#ec4899",
};
const getAccent = (name) => INDEX_ACCENT[name] || "#4f46e5";

/* ─── Index avatar ───────────────────────────────────────────── */
const IndexAvatar = ({ name, size = 40, accent }) => {
  const initials = name
    .split(" ")
    .filter((w) => !["NIFTY", "BSE", "INDIA"].includes(w))
    .slice(0, 2)
    .map((w) => w[0])
    .join("") || name.slice(0, 2);

  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28,
      background: `linear-gradient(135deg, ${accent}dd, ${accent}88)`,
      display: "flex", alignItems: "center", justifyContent: "center",
      fontSize: size * 0.30, fontWeight: 900, color: "#fff",
      letterSpacing: "-0.5px", flexShrink: 0,
      boxShadow: `0 4px 14px ${accent}44`,
    }}>
      {initials}
    </div>
  );
};

/* ─── Company logo ───────────────────────────────────────────── */
const CompanyLogo = ({ symbol, size = 34 }) => {
  const [srcIdx, setSrcIdx] = useState(0);
  const ticker = symbol.replace(/\.(NS|BO)$/i, "").toUpperCase();

  const sources = [
    `https://assets-netstorage.groww.in/stock-assets/logos2/${ticker}.webp`,
    `https://assets.tickertape.in/logos/${ticker.toLowerCase()}.png`,
  ];

  const handleError = () => {
    if (srcIdx < sources.length - 1) {
      setSrcIdx(srcIdx + 1);
    } else {
      setSrcIdx(sources.length);
    }
  };

  if (srcIdx >= sources.length) {
    return (
      <div style={{
        width: size, height: size, borderRadius: size * 0.28,
        background: "linear-gradient(135deg,#4f46e5,#7c3aed)",
        display: "flex", alignItems: "center", justifyContent: "center",
        fontSize: size * 0.33, fontWeight: 800, color: "#fff", flexShrink: 0,
      }}>
        {ticker.slice(0, 2)}
      </div>
    );
  }

  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28,
      background: "#f8fafc", border: "0.5px solid #e2e8f0",
      display: "flex", alignItems: "center", justifyContent: "center",
      overflow: "hidden", flexShrink: 0,
    }}>
      <img
        src={sources[srcIdx]}
        alt={ticker}
        onError={handleError}
        style={{ width: size * 0.78, height: size * 0.78, objectFit: "contain" }}
      />
    </div>
  );
};

/* ─── Range bar ──────────────────────────────────────────────── */
const RangeBar = ({ low, high, current, label1, label2, accentColor }) => {
  const span = (high - low) || 1;
  const pct  = Math.min(100, Math.max(0, ((current - low) / span) * 100));
  return (
    <div style={{ flex: "1 1 240px", minWidth: 0 }}>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
        <div>
          <div style={{ fontSize: 9, fontWeight: 700, color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.09em", marginBottom: 3 }}>
            {label1}
          </div>
          <div style={{ fontSize: 15, fontWeight: 900, color: "#0f172a", letterSpacing: "-0.3px" }}>
            {fmtINR(low)}
          </div>
        </div>
        <div style={{ textAlign: "right" }}>
          <div style={{ fontSize: 9, fontWeight: 700, color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.09em", marginBottom: 3 }}>
            {label2}
          </div>
          <div style={{ fontSize: 15, fontWeight: 900, color: "#0f172a", letterSpacing: "-0.3px" }}>
            {fmtINR(high)}
          </div>
        </div>
      </div>
      <div style={{ position: "relative", height: 5, background: "#e2e8f0", borderRadius: 99 }}>
        <div style={{
          position: "absolute", left: 0, top: 0, bottom: 0,
          width: `${pct}%`, borderRadius: 99,
          background: `linear-gradient(90deg, ${accentColor}44 0%, ${accentColor} 100%)`,
          transition: "width 0.7s cubic-bezier(.4,0,.2,1)",
        }} />
        <div style={{
          position: "absolute", top: "50%",
          left: `${pct}%`, transform: "translate(-50%,-50%)",
          width: 13, height: 13, borderRadius: "50%",
          background: "#fff", border: `2.5px solid ${accentColor}`,
          boxShadow: `0 0 0 3px ${accentColor}20`,
          transition: "left 0.7s cubic-bezier(.4,0,.2,1)",
        }} />
      </div>
    </div>
  );
};

/* ─── Stat tile ──────────────────────────────────────────────── */
const StatTile = ({ label, value }) => (
  <div style={{
    flex: "1 1 120px", borderRadius: 14,
    background: "#f8fafc", border: "1px solid #e2e8f0",
    padding: "14px 16px",
  }}>
    <div style={{ fontSize: 9, fontWeight: 700, color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.09em", marginBottom: 6 }}>
      {label}
    </div>
    <div style={{ fontSize: 17, fontWeight: 900, color: "#0f172a", letterSpacing: "-0.4px" }}>
      {value}
    </div>
  </div>
);

/* ─── Chart tooltip ──────────────────────────────────────────── */
const ChartTooltip = ({ active, payload, range }) => {
  if (!active || !payload?.length) return null;
  const d = payload[0]?.payload;
  if (!d) return null;
  return (
    <div style={{
      background: "rgba(15,23,42,0.96)", border: "1px solid rgba(99,102,241,0.25)",
      borderRadius: 12, padding: "10px 14px", fontSize: 11,
      boxShadow: "0 8px 32px rgba(0,0,0,0.35)",
    }}>
      <div style={{ color: "#818cf8", fontWeight: 700, marginBottom: 6, fontSize: 9, textTransform: "uppercase", letterSpacing: "0.12em" }}>
        {range === "1D"
          ? new Date(d.date).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true })
          : new Date(d.date).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "2-digit" })}
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 16 }}>
        <span style={{ color: "#64748b", fontWeight: 600 }}>Price</span>
        <span style={{ color: "#f1f5f9", fontWeight: 800 }}>{fmtINR(d.price)}</span>
      </div>
    </div>
  );
};

/* ══════════════════════════════════════════════════════════════ */
export default function IndexDetailView() {
  const { slug }    = useParams();
  const navigate    = useNavigate();
  const indexName   = slugToIndex(slug || "nifty-50");
  const accent      = getAccent(indexName);

  const [chartData,    setChartData]    = useState([]);
  const [chartLoading, setChartLoading] = useState(true);
  const [range,        setRange]        = useState("1D");

  // statsSnapshot holds the live-quote values fetched on first load.
  // It is ONLY updated when the index changes, never when the range changes.
  // This keeps Today's High/Low, 52W High/Low, Open, Prev Close stable
  // across timeframe switches (they come from the live quote, not chart history).
  const [statsSnapshot, setStatsSnapshot] = useState(null);
  const statsLoadedForIndex = useRef(null); // tracks which index the snapshot belongs to

  const [constituents,      setConstituents]      = useState([]);
  const [constitPage,       setConstitPage]       = useState(1);
  const [constitTotal,      setConstitTotal]      = useState(0);
  const [constitTotalPages, setConstitTotalPages] = useState(1);
  const [constitLoading,    setConstitLoading]    = useState(true);

  const fetchChart = useCallback(async (r, isInitialLoad = false) => {
    setChartLoading(true);
    try {
      const res = await api.get(
        `/index/chart?name=${encodeURIComponent(indexName)}&range=${r.toLowerCase()}`
      );
      if (res.data?.success) {
        setChartData(res.data.chartData || []);

        // Only capture stats on the initial load for this index.
        // Subsequent range changes update only the chart, never the stats panel.
        if (isInitialLoad && res.data.stats) {
          setStatsSnapshot(res.data.stats);
          statsLoadedForIndex.current = indexName;
        }
      }
    } catch (_) {}
    finally { setChartLoading(false); }
  }, [indexName]);

  const fetchConstituents = useCallback(async (p) => {
    setConstitLoading(true);
    try {
      const res = await api.get(
        `/index/constituents?name=${encodeURIComponent(indexName)}&page=${p}&pageSize=${PAGE_SIZE}`
      );
      if (res.data?.success) {
        setConstituents(res.data.data || []);
        setConstitTotal(res.data.totalCount || 0);
        setConstitTotalPages(res.data.totalPages || 1);
      }
    } catch (_) {}
    finally { setConstitLoading(false); }
  }, [indexName]);

  // When the index changes: reset everything and do an initial load (captures stats).
  useEffect(() => {
    setRange("1D");
    setStatsSnapshot(null);
    statsLoadedForIndex.current = null;
    fetchChart("1D", /* isInitialLoad */ true);
  }, [indexName]); // intentionally NOT including fetchChart to avoid double-fire

  // When range changes (but index hasn't): fetch chart only, don't touch stats.
  useEffect(() => {
    if (range === "1D" && statsLoadedForIndex.current !== indexName) return; // handled above
    if (statsLoadedForIndex.current === indexName) {
      fetchChart(range, /* isInitialLoad */ false);
    }
  }, [range]);

  useEffect(() => {
    setConstitPage(1);
    fetchConstituents(1);
  }, [indexName]);

  const handlePageChange = (p) => { setConstitPage(p); fetchConstituents(p); };

  const stats        = statsSnapshot;
  const isUp         = (stats?.dayChangePct ?? 0) >= 0;
  const priceColor   = isUp ? "#10b981" : "#ef4444";
  const currentPrice = stats?.currentPrice ?? 0;
  const displayName  = toTitleCase(indexName);

  const formatXTick = (d) =>
    range === "1D"
      ? new Date(d).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true })
      : new Date(d).toLocaleDateString("en-IN", { day: "2-digit", month: "short" });

  return (
    <>
      <style>{`
        .idv { padding: clamp(12px,3.5vw,28px); max-width: 1200px; margin: 0 auto; background: #fcfcfd; min-height: 100vh; box-sizing: border-box; }
        .idv-card { background: #fff; border: 1px solid #f1f5f9; border-radius: 20px; padding: 20px 22px; box-shadow: 0 1px 6px rgba(0,0,0,0.04); box-sizing: border-box; margin-bottom: 14px; }
        .rng-bar { display: flex; gap: 2px; padding: 4px 6px; background: #f8fafc; border-radius: 12px; border: 1px solid #e2e8f0; width: fit-content; margin-bottom: 18px; overflow-x: auto; }
        .rng-btn { padding: 6px 12px; border-radius: 8px; font-size: 10px; font-weight: 800; text-transform: uppercase; border: none; cursor: pointer; transition: all 0.15s; background: transparent; color: #64748b; letter-spacing: 0.05em; white-space: nowrap; }
        .rng-btn.on { color: #fff; box-shadow: 0 2px 10px rgba(79,70,229,0.35); }
        .rng-btn:hover:not(.on) { background: #f1f5f9; color: #334155; }
        .ct-table { width: 100%; border-collapse: collapse; }
        .ct-table th { font-size: 9px; font-weight: 700; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.09em; padding: 10px 14px; text-align: left; border-bottom: 1px solid #f1f5f9; white-space: nowrap; }
        .ct-table td { padding: 11px 14px; border-bottom: 1px solid #f8fafc; vertical-align: middle; }
        .ct-table tr:last-child td { border-bottom: none; }
        .ct-table tbody tr { cursor: pointer; transition: background 0.1s; }
        .ct-table tbody tr:hover td { background: #f8fafc; }
        .ct-sym { font-size: 12px; font-weight: 800; color: #4f46e5; }
        .ct-cname { font-size: 10px; color: #64748b; font-weight: 500; margin-top: 2px; max-width: 260px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .ct-sec { display: inline-flex; align-items: center; background: #f1f5f9; border-radius: 20px; padding: 3px 10px; font-size: 10px; font-weight: 600; color: #475569; white-space: nowrap; }
        .pg-btn { width: 30px; height: 30px; border-radius: 8px; border: 1px solid #e2e8f0; background: #fff; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.15s; color: #64748b; }
        .pg-btn:hover:not(:disabled) { border-color: #4f46e5; color: #4f46e5; }
        .pg-btn:disabled { opacity: 0.3; cursor: not-allowed; }
        .ai-row { display: flex; align-items: center; justify-content: space-between; padding: 9px 12px; border-radius: 10px; cursor: pointer; transition: background 0.12s; gap: 8px; }
        .ai-row:hover { background: #f8fafc; }
        @media (max-width: 640px) {
          .ct-table th:nth-child(4), .ct-table td:nth-child(4) { display: none; }
          .perf-flex { flex-direction: column; }
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        @keyframes fadeUp { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
        .au { animation: fadeUp 0.35s ease both; }
      `}</style>

      <div className="idv">

        {/* ══ HEADER ══════════════════════════════════════════════ */}
        <div className="idv-card au" style={{ animationDelay: "0ms" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 14, minWidth: 0 }}>
              <button
                onClick={() => navigate("/")}
                style={{
                  width: 36, height: 36, borderRadius: "50%",
                  border: "1px solid #e2e8f0", background: "#fff",
                  cursor: "pointer", display: "flex", alignItems: "center",
                  justifyContent: "center", flexShrink: 0,
                }}
              >
                <ArrowLeft size={16} color="#64748b" />
              </button>
              <IndexAvatar name={indexName} size={44} accent={accent} />
              <div style={{ minWidth: 0 }}>
                <h1 style={{
                  fontSize: "clamp(16px,2.5vw,24px)", fontWeight: 900,
                  color: "#0f172a", letterSpacing: "-0.5px", margin: 0, lineHeight: 1.1,
                }}>
                  {displayName}
                </h1>
                <div style={{ fontSize: 10, color: "#94a3b8", fontWeight: 600, marginTop: 3, textTransform: "uppercase", letterSpacing: "0.1em" }}>
                  NSE / BSE Index
                </div>
              </div>
            </div>

            {stats && (
              <div style={{ textAlign: "right", flexShrink: 0 }}>
                <div style={{
                  fontSize: "clamp(20px,2.8vw,32px)", fontWeight: 900,
                  color: priceColor, letterSpacing: "-0.8px", lineHeight: 1,
                }}>
                  {fmtINR(stats.currentPrice)}
                </div>
                <div style={{
                  display: "flex", alignItems: "center", justifyContent: "flex-end",
                  gap: 4, marginTop: 5, color: priceColor, fontSize: 12, fontWeight: 700,
                }}>
                  {isUp ? <TrendingUp size={12} /> : <TrendingDown size={12} />}
                  {isUp ? "+" : ""}{fmtINR(stats.dayChange)}
                  <span style={{ opacity: 0.75 }}>({isUp ? "+" : ""}{fmtINR(stats.dayChangePct)}%)</span>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* ══ PERFORMANCE ════════════════════════════════════════ */}
        {stats && (
          <div className="idv-card au" style={{ animationDelay: "50ms" }}>
            <div style={{ fontSize: 10, fontWeight: 800, color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.1em", marginBottom: 20 }}>
              Performance
            </div>
            <div className="perf-flex" style={{ display: "flex", gap: 32, flexWrap: "wrap" }}>
              <RangeBar
                low={stats.todayLow}  high={stats.todayHigh}
                current={currentPrice} label1="Today's Low" label2="Today's High"
                accentColor={priceColor}
              />
              <RangeBar
                low={stats.week52Low} high={stats.week52High}
                current={currentPrice} label1="52W Low" label2="52W High"
                accentColor={accent}
              />
            </div>
            <div style={{ display: "flex", gap: 10, marginTop: 18, flexWrap: "wrap" }}>
              <StatTile label="Open"        value={fmtINR(stats.open)} />
              <StatTile label="Prev. Close" value={fmtINR(stats.prevClose)} />
            </div>
          </div>
        )}

        {/* ══ CHART ════════════════════════════════════════════════ */}
        <div className="idv-card au" style={{ animationDelay: "100ms" }}>
          <div className="rng-bar">
            {RANGES.map((r) => (
              <button
                key={r}
                className={`rng-btn${range === r ? " on" : ""}`}
                style={range === r ? { background: accent } : {}}
                onClick={() => setRange(r)}
              >
                {r}
              </button>
            ))}
          </div>

          <div style={{ height: "clamp(180px,28vw,270px)" }}>
            {chartLoading ? (
              <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <Loader2 size={26} style={{ animation: "spin 0.8s linear infinite", color: accent }} />
              </div>
            ) : chartData.length < 2 ? (
              <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center", flexDirection: "column", gap: 8 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: "#94a3b8" }}>No data for this range</div>
                <div style={{ fontSize: 10, color: "#cbd5e1" }}>Try a longer timeframe</div>
              </div>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={chartData} margin={{ left: 8, right: 8, top: 8, bottom: 0 }}>
                  <defs>
                    <linearGradient id="idxGrad" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%"  stopColor={priceColor} stopOpacity={0.18} />
                      <stop offset="95%" stopColor={priceColor} stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                  <XAxis
                    dataKey="date" tickFormatter={formatXTick} minTickGap={48}
                    tick={{ fontSize: 9, fontWeight: 600, fill: "#94a3b8" }}
                    axisLine={false} tickLine={false}
                  />
                  <YAxis
                    domain={["auto", "auto"]}
                    tick={{ fontSize: 10, fontWeight: 700, fill: "#94a3b8" }}
                    axisLine={false} tickLine={false} width={72}
                    tickFormatter={(v) => v.toLocaleString("en-IN", { maximumFractionDigits: 0 })}
                  />
                  <Tooltip
                    content={<ChartTooltip range={range} />}
                    cursor={{ stroke: "#c7d2fe", strokeWidth: 1.5, strokeDasharray: "4 4" }}
                  />
                  <Area type="monotone" dataKey="price" fill="url(#idxGrad)" stroke="none" connectNulls />
                  <Line type="monotone" dataKey="price" stroke={priceColor} strokeWidth={2.2} dot={false} connectNulls />
                </ComposedChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>

        {/* ══ CONSTITUENTS ═════════════════════════════════════════ */}
        {constitTotal > 0 && (
          <div className="idv-card au" style={{ animationDelay: "150ms" }}>
            <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 16 }}>
              <div style={{ fontSize: 16, fontWeight: 900, color: "#0f172a", letterSpacing: "-0.3px" }}>
                {displayName} Companies
              </div>
              <div style={{
                fontSize: 10, fontWeight: 700, color: "#94a3b8",
                background: "#f1f5f9", borderRadius: 20, padding: "2px 8px",
              }}>
                {constitTotal}
              </div>
            </div>

            {constitLoading ? (
              <div style={{ padding: 40, display: "flex", justifyContent: "center" }}>
                <Loader2 size={22} style={{ animation: "spin 0.8s linear infinite", color: accent }} />
              </div>
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table className="ct-table">
                  <thead>
                    <tr>
                      <th style={{ width: 32, paddingLeft: 10 }}>#</th>
                      <th>Company</th>
                      <th>Market Cap</th>
                      <th>Sector</th>
                    </tr>
                  </thead>
                  <tbody>
                    {constituents.map((c, i) => (
                      <tr key={c.symbol} onClick={() => navigate(`/stock/${c.symbol}`)}>
                        <td style={{ color: "#cbd5e1", fontSize: 11, fontWeight: 700, paddingLeft: 10 }}>
                          {(constitPage - 1) * PAGE_SIZE + i + 1}
                        </td>
                        <td>
                          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                            <CompanyLogo symbol={c.symbol} size={34} />
                            <div style={{ minWidth: 0 }}>
                              <div className="ct-sym">{c.symbol}</div>
                              <div className="ct-cname">{c.name}</div>
                            </div>
                          </div>
                        </td>
                        <td>
                          <span style={{
                            fontSize: 13, fontWeight: 800, color: "#334155",
                            fontVariantNumeric: "tabular-nums",
                          }}>
                            {fmtMarketCap(c.marketCap)}
                          </span>
                        </td>
                        <td>
                          <span className="ct-sec">{c.industry}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Pagination */}
            <div style={{
              display: "flex", alignItems: "center", justifyContent: "space-between",
              marginTop: 16, paddingTop: 14, borderTop: "1px solid #f1f5f9",
            }}>
              <div style={{ fontSize: 10, color: "#94a3b8", fontWeight: 600 }}>
                Page {constitPage} of {constitTotalPages} · {constitTotal} stocks
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <button className="pg-btn" disabled={constitPage <= 1}
                  onClick={() => handlePageChange(constitPage - 1)}>
                  <ChevronLeft size={14} />
                </button>
                {Array.from({ length: Math.min(constitTotalPages, 5) }, (_, i) => {
                  let p;
                  if (constitTotalPages <= 5)                    p = i + 1;
                  else if (constitPage <= 3)                     p = i + 1;
                  else if (constitPage >= constitTotalPages - 2) p = constitTotalPages - 4 + i;
                  else                                           p = constitPage - 2 + i;
                  return (
                    <button key={p} onClick={() => handlePageChange(p)} style={{
                      width: 30, height: 30, borderRadius: 8,
                      border: p === constitPage ? "none" : "1px solid #e2e8f0",
                      background: p === constitPage ? accent : "#fff",
                      color: p === constitPage ? "#fff" : "#64748b",
                      fontSize: 11, fontWeight: 700, cursor: "pointer",
                    }}>
                      {p}
                    </button>
                  );
                })}
                <button className="pg-btn" disabled={constitPage >= constitTotalPages}
                  onClick={() => handlePageChange(constitPage + 1)}>
                  <ChevronRight size={14} />
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ══ ALL INDICES ══════════════════════════════════════════ */}
        <div className="idv-card au" style={{ animationDelay: "200ms" }}>
          <div style={{ fontSize: 13, fontWeight: 800, color: "#0f172a", marginBottom: 12, letterSpacing: "-0.2px" }}>
            All Indices
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(190px,1fr))", gap: 2 }}>
            {ALL_INDICES.map((idx) => {
              const active    = idx === indexName;
              const idxAccent = getAccent(idx);
              return (
                <div
                  key={idx}
                  className="ai-row"
                  style={active ? { background: "#eef2ff" } : {}}
                  onClick={() => navigate(`/index/${indexToSlug(idx)}`)}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 10, minWidth: 0 }}>
                    <IndexAvatar name={idx} size={28} accent={idxAccent} />
                    <span style={{
                      fontSize: 12, fontWeight: 700,
                      color: active ? "#4f46e5" : "#1e293b",
                      whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis",
                    }}>
                      {toTitleCase(idx)}
                    </span>
                  </div>
                  <ChevronRight size={13} color={active ? "#4f46e5" : "#cbd5e1"} style={{ flexShrink: 0 }} />
                </div>
              );
            })}
          </div>
        </div>

      </div>
    </>
  );
}