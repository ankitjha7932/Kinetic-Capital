import React, { useState, useEffect, useCallback } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
    ArrowLeft, TrendingUp, TrendingDown, Zap,
    Search, X, Grid, List, ChevronLeft, ChevronRight,
    AlertCircle, RefreshCw
} from "lucide-react";
import api from "../api/axios";

/* ─── Constants ─────────────────────────────────────────────────────────── */
const INDICES = [
    "NIFTY 100", "NIFTY 500", "MIDCAP 100", "SMALLCAP 100", "NIFTY TOTAL MARKET",
];

const PULSE_TABS = [
    { key: "gainers1D", label: "Surging", Icon: TrendingUp, accent: "#059669", bg: "#ecfdf5", pill: "#d1fae5" },
    { key: "losers1D", label: "Sliding", Icon: TrendingDown, accent: "#e11d48", bg: "#fff1f2", pill: "#ffe4e6" },
    { key: "volumeShockers", label: "Buzzing", Icon: Zap, accent: "#7c3aed", bg: "#f5f3ff", pill: "#ede9fe" },
];

const SORT_OPTIONS = [
    { key: "default", label: "Default" },
    { key: "price_asc", label: "Price ↑" },
    { key: "price_desc", label: "Price ↓" },
    { key: "change_asc", label: "Change ↑" },
    { key: "change_desc", label: "Change ↓" },
    { key: "volume_desc", label: "Volume ↓" },
];

const getLogo = (sym) =>
    `https://assets-netstorage.groww.in/stock-assets/logos2/${(sym || "")
        .replace(".NS", "")
        .toUpperCase()}.webp`;

const RL_PER_PAGE = 24;
const MP_PER_PAGE = 30;

/* ─── Helpers ────────────────────────────────────────────────────────────── */
function fmt(n) { return n?.toLocaleString("en-IN") ?? "—"; }

/* ─── Sparkline ──────────────────────────────────────────────────────────── */
function Sparkline({ data, positive, w = 72, h = 28 }) {
    if (!data || data.length < 2)
        return <div style={{ width: w, height: h, borderRadius: 4, background: "#f1f5f9", flexShrink: 0 }} />;
    const PAD = 2, W = w - PAD * 2, H = h - PAD * 2;
    const min = Math.min(...data), max = Math.max(...data), range = max - min || 1;
    const last7 = data.slice(-7), avg = last7.reduce((a, b) => a + b, 0) / last7.length;
    const baseY = PAD + H - ((avg - min) / range) * H;
    const pts = data.map((v, i) =>
        `${(PAD + (i / (data.length - 1)) * W).toFixed(2)},${(PAD + H - ((v - min) / range) * H).toFixed(2)}`
    ).join(" ");
    return (
        <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} fill="none" style={{ display: "block", flexShrink: 0, overflow: "visible" }}>
            <line x1={PAD} y1={baseY} x2={w - PAD} y2={baseY} stroke="#cbd5e1" strokeWidth="1" strokeDasharray="3 3" />
            <polyline points={pts} stroke={positive ? "#10b981" : "#f43f5e"} strokeWidth="1.5" fill="none" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    );
}

/* ─── Logo Avatar ─────────────────────────────────────────────────────────── */
function Logo({ symbol, size = 36 }) {
    const [err, setErr] = useState(false);
    const t = (symbol || "").replace(".NS", "").toUpperCase();
    return err ? (
        <div style={{ width: size, height: size, borderRadius: size * 0.28, background: "linear-gradient(135deg,#4f46e5,#7c3aed)", color: "#fff", display: "flex", alignItems: "center", justifyContent: "center", fontSize: size * 0.26, fontWeight: 800, flexShrink: 0, letterSpacing: "-0.5px" }}>
            {t.slice(0, 3)}
        </div>
    ) : (
        <div style={{ width: size, height: size, borderRadius: size * 0.28, background: "#f8fafc", border: "1px solid #e2e8f0", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden", flexShrink: 0 }}>
            <img src={getLogo(symbol)} alt={t} style={{ width: size * 0.72, height: size * 0.72, objectFit: "contain" }} onError={() => setErr(true)} />
        </div>
    );
}

/* ─── Change Chip ─────────────────────────────────────────────────────────── */
function ChangeChip({ val }) {
    const pos = val >= 0;
    const color = pos ? "#059669" : "#e11d48";
    const bg = pos ? "#ecfdf5" : "#fff1f2";
    return (
        <span style={{ fontSize: 11, fontWeight: 700, color, background: bg, borderRadius: 5, padding: "1px 6px", display: "inline-block" }}>
            {pos ? "+" : ""}{Math.abs(val).toFixed(2)}%
        </span>
    );
}

/* ─── Stock Grid Card ─────────────────────────────────────────────────────── */
function GridCard({ stock, field, rank, onSelect }) {
    const val = stock[field] ?? stock.changePercent ?? 0;
    const pos = val >= 0;
    const color = pos ? "#059669" : "#e11d48";
    return (
        <div onClick={() => onSelect(stock.symbol)} className="ex-grid-card">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
                <Logo symbol={stock.symbol} size={30} />
                <span style={{ fontSize: 9, fontWeight: 800, color: "#a5b4fc", background: "#eef2ff", borderRadius: 4, padding: "2px 6px" }}>#{rank}</span>
            </div>
            <div style={{ fontSize: 11, fontWeight: 800, color: "#0f172a", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", marginBottom: 1 }}>
                {(stock.symbol || "").replace(".NS", "")}
            </div>
            <div style={{ fontSize: 9, color: "#94a3b8", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", marginBottom: 6 }}>
                {stock.companyName}
            </div>
            <div style={{ display: "flex", justifyContent: "center", marginBottom: 6 }}>
                <Sparkline data={stock.sparkline} positive={pos} w={54} h={24} />
            </div>
            <div style={{ fontSize: 13, fontWeight: 800, color: "#0f172a", marginBottom: 2 }}>
                ₹{fmt(stock.price)}
            </div>
            <ChangeChip val={val} />
        </div>
    );
}

/* ─── Stock List Row ──────────────────────────────────────────────────────── */
function ListRow({ stock, field, rank, onSelect, extra }) {
    const val = stock[field] ?? stock.changePercent ?? 0;
    const pos = val >= 0;
    return (
        <div onClick={() => onSelect(stock.symbol)} className="ex-list-row">
            <span className="ex-rank">{rank}</span>
            <Logo symbol={stock.symbol} size={34} />
            <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {(stock.symbol || "").replace(".NS", "")}
                </div>
                <div style={{ fontSize: 10, color: "#64748b", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {stock.companyName}
                </div>
            </div>
            <div className="ex-spark-col">
                <Sparkline data={stock.sparkline} positive={pos} w={68} h={26} />
            </div>
            <div style={{ textAlign: "right", minWidth: 105, flexShrink: 0 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a" }}>₹{fmt(stock.price)}</div>
                <ChangeChip val={val} />
            </div>
            {extra && (
                <div className="ex-extra-col" style={{ textAlign: "right", minWidth: 72 }}>
                    {extra}
                </div>
            )}
        </div>
    );
}

/* ─── Pagination ─────────────────────────────────────────────────────────── */
function Pagination({ page, total, onChange }) {
    if (total <= 1) return null;
    const pages = Array.from({ length: total }, (_, i) => i + 1)
        .filter(p => p === 1 || p === total || Math.abs(p - page) <= 2)
        .reduce((acc, p, i, arr) => { if (i > 0 && p - arr[i - 1] > 1) acc.push("…"); acc.push(p); return acc; }, []);

    return (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 4, padding: "16px 0" }}>
            <button className="ex-pg-btn" onClick={() => onChange(page - 1)} disabled={page === 1} style={{ padding: "6px 10px" }}>
                <ChevronLeft size={14} />
            </button>
            {pages.map((p, i) =>
                p === "…"
                    ? <span key={`e${i}`} style={{ color: "#94a3b8", fontSize: 12, padding: "0 2px" }}>…</span>
                    : <button key={p} className={`ex-pg-btn ${page === p ? "active" : ""}`} onClick={() => onChange(p)}>{p}</button>
            )}
            <button className="ex-pg-btn" onClick={() => onChange(page + 1)} disabled={page === total} style={{ padding: "6px 10px" }}>
                <ChevronRight size={14} />
            </button>
        </div>
    );
}

/* ─── Toolbar ─────────────────────────────────────────────────────────────── */
function Toolbar({ search, onSearch, sort, onSort, view, onView, count, sortOptions, extraLeft }) {
    return (
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 14, flexWrap: "wrap" }}>
            {extraLeft}
            <div style={{ position: "relative", flex: 1, minWidth: 160, maxWidth: 280 }}>
                <Search size={13} style={{ position: "absolute", left: 10, top: "50%", transform: "translateY(-50%)", color: "#94a3b8", pointerEvents: "none" }} />
                <input
                    value={search} onChange={e => onSearch(e.target.value)}
                    placeholder="Search stocks…"
                    style={{ width: "100%", padding: "7px 32px 7px 30px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 12, outline: "none", background: "#f8fafc", boxSizing: "border-box" }}
                />
                {search && <button onClick={() => onSearch("")} style={{ position: "absolute", right: 8, top: "50%", transform: "translateY(-50%)", background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 0, display: "flex" }}><X size={12} /></button>}
            </div>
            <select value={sort} onChange={e => onSort(e.target.value)} className="ex-select">
                {sortOptions.map(s => <option key={s.key} value={s.key}>{s.label}</option>)}
            </select>
            <div style={{ display: "flex", border: "1px solid #e2e8f0", borderRadius: 8, overflow: "hidden" }}>
                <button onClick={() => onView("grid")} className={`ex-view-btn ${view === "grid" ? "active" : ""}`} title="Grid"><Grid size={13} /></button>
                <button onClick={() => onView("list")} className={`ex-view-btn ${view === "list" ? "active" : ""}`} title="List"><List size={13} /></button>
            </div>
            <span style={{ fontSize: 11, fontWeight: 700, color: "#64748b", background: "#f1f5f9", borderRadius: 6, padding: "3px 9px" }}>{count} stocks</span>
        </div>
    );
}

/* ─── Column Header Row ──────────────────────────────────────────────────── */
function ColHeader({ right }) {
    return (
        <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "8px 16px", borderBottom: "1px solid #f1f5f9", background: "#fafafa" }}>
            <div style={{ width: 26, flexShrink: 0 }} />
            <div style={{ width: 34, flexShrink: 0 }} />
            <div className="ex-ch" style={{ flex: 1 }}>Company</div>
            <div className="ex-ch ex-spark-col" style={{ width: 68 }} />
            <div className="ex-ch" style={{ textAlign: "right", minWidth: 105 }}>{right || "Price / Change"}</div>
        </div>
    );
}

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN COMPONENT
══════════════════════════════════════════════════════════════════════════════ */
export default function MarketExplorer() {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();

    const initTab = searchParams.get("tab") || "gainers1D";
    const initIndex = searchParams.get("index") || "NIFTY 100";
    const initSec = ["gainers1D", "losers1D", "volumeShockers"].includes(initTab) ? "pulse" : "leaders";

    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [selIndex, setSelIndex] = useState(initIndex);
    const [activeSection, setActiveSection] = useState(initSec);

    /* Market Pulse state */
    const [mpTab, setMpTab] = useState(initTab);
    const [mpView, setMpView] = useState("list");
    const [mpSearch, setMpSearch] = useState("");
    const [mpSort, setMpSort] = useState("default");
    const [mpPage, setMpPage] = useState(1);

    /* Return Leaders state */
    const [rlPeriod, setRlPeriod] = useState("1W");
    const [rlView, setRlView] = useState("list");
    const [rlSearch, setRlSearch] = useState("");
    const [rlSort, setRlSort] = useState("default");
    const [rlPage, setRlPage] = useState(1);

    const fetchData = useCallback(async (index) => {
        setLoading(true); setError(null);
        try {
            const res = await api.get(`Portfolio/index-movers?index=${encodeURIComponent(index)}`);
            setData(res.data.data || res.data);
        } catch (e) {
            setError("Failed to load market data. Please try again.");
        } finally { setLoading(false); }
    }, []);

    useEffect(() => { fetchData(selIndex); }, [selIndex]);

    /* ── Derived: Market Pulse ── */
    const mpRaw = data?.[mpTab] || [];
    const mpFiltered = mpRaw
        .filter(s => !mpSearch || s.symbol.toLowerCase().includes(mpSearch.toLowerCase()) || (s.companyName || "").toLowerCase().includes(mpSearch.toLowerCase()))
        .sort((a, b) => {
            if (mpSort === "default") return 0;
            if (mpSort === "price_asc") return a.price - b.price;
            if (mpSort === "price_desc") return b.price - a.price;
            if (mpSort === "change_asc") return a.changePercent - b.changePercent;
            if (mpSort === "change_desc") return b.changePercent - a.changePercent;
            if (mpSort === "volume_desc") return (b.volume || 0) - (a.volume || 0);
            return 0;
        });
    const mpTotalPages = Math.ceil(mpFiltered.length / MP_PER_PAGE);
    const mpPaged = mpFiltered.slice((mpPage - 1) * MP_PER_PAGE, mpPage * MP_PER_PAGE);

    /* ── Derived: Return Leaders ── */
    const rlField = rlPeriod === "1W" ? "return1W" : "return1M";
    const rlRaw = (rlPeriod === "1W" ? data?.topReturnsWeekly : data?.topReturnsMonthly) || [];
    const rlFiltered = rlRaw
        .filter(s => !rlSearch || s.symbol.toLowerCase().includes(rlSearch.toLowerCase()) || (s.companyName || "").toLowerCase().includes(rlSearch.toLowerCase()))
        .sort((a, b) => {
            if (rlSort === "default") return b[rlField] - a[rlField];
            if (rlSort === "price_asc") return a.price - b.price;
            if (rlSort === "price_desc") return b.price - a.price;
            if (rlSort === "change_asc") return a[rlField] - b[rlField];
            if (rlSort === "change_desc") return b[rlField] - a[rlField];
            return 0;
        });
    const rlTotalPages = Math.ceil(rlFiltered.length / RL_PER_PAGE);
    const rlPaged = rlFiltered.slice((rlPage - 1) * RL_PER_PAGE, rlPage * RL_PER_PAGE);

    const mpCfg = PULSE_TABS.find(t => t.key === mpTab);
    const handleStock = sym => navigate(`/stock/${sym}`);

    return (
        <>
            <style>{`
            .ex-root { max-width:1400px; margin:0 auto; padding:clamp(12px,3vw,28px); min-height:100vh; background:#f8fafc; box-sizing:border-box; font-family:sans-serif; }

            /* Top bar */
            .ex-topbar { display:flex; align-items:center; gap:12px; margin-bottom:20px; flex-wrap:wrap; }
            .ex-back-btn { width:36px; height:36px; border-radius:50%; border:none; background:#fff; box-shadow:0 1px 4px rgba(0,0,0,.08); cursor:pointer; display:flex; align-items:center; justify-content:center; color:#64748b; transition:box-shadow .15s; flex-shrink:0; }
            .ex-back-btn:hover { box-shadow:0 3px 8px rgba(0,0,0,.12); }
            .ex-title { font-size:20px; font-weight:800; color:#0f172a; letter-spacing:-0.5px; }
            .ex-subtitle { font-size:11px; color:#94a3b8; font-weight:600; margin-top:1px; }
            .ex-select { padding:7px 10px; border:1px solid #e2e8f0; border-radius:8px; font-size:12px; font-weight:700; background:#fff; outline:none; cursor:pointer; color:#334155; }
            .ex-select:focus { border-color:#a5b4fc; }

            /* Section tabs */
            .ex-section-tabs { display:flex; gap:6px; margin-bottom:16px; background:#fff; border:1px solid #e2e8f0; border-radius:12px; padding:4px; width:fit-content; }
            .ex-section-tab { padding:7px 18px; border-radius:9px; font-size:12px; font-weight:700; border:none; cursor:pointer; transition:all .15s; background:transparent; color:#64748b; }
            .ex-section-tab.active { background:#4f46e5; color:#fff; box-shadow:0 2px 8px rgba(79,70,229,.25); }

            /* Pulse tab buttons */
            .ex-pulse-tabs { display:flex; gap:8px; margin-bottom:14px; flex-wrap:wrap; }
            .ex-pulse-tab { display:flex; align-items:center; gap:5px; padding:8px 14px; border-radius:10px; font-size:12px; font-weight:700; border:1px solid; cursor:pointer; transition:all .15s; }

            /* View toggle */
            .ex-view-btn { padding:7px 10px; border:none; background:transparent; cursor:pointer; color:#64748b; display:flex; align-items:center; transition:all .12s; }
            .ex-view-btn:hover { background:#f1f5f9; }
            .ex-view-btn.active { background:#eef2ff; color:#4f46e5; }

            /* Grid */
            .ex-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(138px,1fr)); gap:10px; margin-bottom:4px; }
            @media(max-width:480px){ .ex-grid{ grid-template-columns:repeat(2,1fr); gap:8px; } }

            .ex-grid-card { background:#fff; border:1px solid #e2e8f0; border-radius:14px; padding:12px 10px; cursor:pointer; transition:all .18s ease; box-sizing:border-box; }
            .ex-grid-card:hover { border-color:#c7d2fe; box-shadow:0 6px 20px rgba(79,70,229,.1); transform:translateY(-3px); }

            /* List */
            .ex-list-wrap { background:#fff; border:1px solid #e2e8f0; border-radius:14px; overflow:hidden; margin-bottom:4px; }
            .ex-list-row { display:flex; align-items:center; gap:10px; padding:10px 16px; border-bottom:1px solid #f8fafc; cursor:pointer; transition:background .1s; }
            .ex-list-row:last-child { border-bottom:none; }
            .ex-list-row:hover { background:#fafbff; }
            .ex-rank { font-size:10px; font-weight:700; color:#94a3b8; min-width:22px; text-align:right; flex-shrink:0; }
            .ex-spark-col { flex-shrink:0; }
            .ex-ch { font-size:9px; font-weight:700; color:#94a3b8; text-transform:uppercase; letter-spacing:.6px; }
            @media(max-width:480px){ .ex-spark-col{ display:none; } }

            /* Pagination */
            .ex-pg-btn { padding:5px 10px; border:1px solid #e2e8f0; border-radius:7px; font-size:12px; font-weight:700; cursor:pointer; background:#fff; color:#475569; transition:all .12s; min-width:32px; }
            .ex-pg-btn:hover:not(:disabled) { background:#f1f5f9; }
            .ex-pg-btn.active { background:#4f46e5; color:#fff; border-color:#4f46e5; box-shadow:0 2px 6px rgba(79,70,229,.3); }
            .ex-pg-btn:disabled { opacity:.35; cursor:default; }

            /* Period toggle */
            .ex-period-toggle { display:flex; background:#f1f5f9; border-radius:8px; padding:3px; gap:2px; }
            .ex-period-btn { padding:4px 14px; border-radius:6px; border:none; font-size:11px; font-weight:700; cursor:pointer; transition:all .15s; background:transparent; color:#64748b; }
            .ex-period-btn.active { background:#fff; color:#4f46e5; box-shadow:0 1px 3px rgba(0,0,0,.08); }

            /* Empty */
            .ex-empty { padding:56px 0; text-align:center; color:#94a3b8; font-size:13px; font-weight:600; }

            /* Loading */
            .ex-loading { display:flex; flex-direction:column; align-items:center; justify-content:center; padding:80px 0; }
            @keyframes spin { to { transform:rotate(360deg); } }
            .ex-spinner { width:32px; height:32px; border:2.5px solid #e0e7ff; border-top-color:#4f46e5; border-radius:50%; animation:spin .7s linear infinite; margin-bottom:12px; }
        `}</style>

            <div className="ex-root">
                {/* ── TOP BAR ── */}
                <div className="ex-topbar">
                    <button className="ex-back-btn" onClick={() => navigate(-1)}><ArrowLeft size={17} /></button>
                    <div style={{ flex: 1 }}>
                        <div className="ex-title">Market Explorer</div>
                        <div className="ex-subtitle">
                            {data?.index || selIndex} · {data?.totalStocks || "—"} stocks
                        </div>
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <select className="ex-select" value={selIndex}
                            onChange={e => { setSelIndex(e.target.value); setMpPage(1); setRlPage(1); }}>
                            {INDICES.map(i => <option key={i} value={i}>{i}</option>)}
                        </select>
                        {!loading && (
                            <button onClick={() => fetchData(selIndex)} style={{ width: 34, height: 34, borderRadius: 8, border: "1px solid #e2e8f0", background: "#fff", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", color: "#64748b" }}>
                                <RefreshCw size={14} />
                            </button>
                        )}
                    </div>
                </div>

                {/* ── SECTION TABS ── */}
                <div className="ex-section-tabs">
                    <button className={`ex-section-tab ${activeSection === "leaders" ? "active" : ""}`}
                        onClick={() => setActiveSection("leaders")}>Return Leaders</button>
                    <button className={`ex-section-tab ${activeSection === "pulse" ? "active" : ""}`}
                        onClick={() => setActiveSection("pulse")}>Market Pulse</button>
                </div>

                {/* ── LOADING ── */}
                {loading && (
                    <div className="ex-loading">
                        <div className="ex-spinner" />
                        <div style={{ fontSize: 11, fontWeight: 700, color: "#94a3b8", textTransform: "uppercase", letterSpacing: ".1em" }}>Loading</div>
                    </div>
                )}

                {/* ── ERROR ── */}
                {!loading && error && (
                    <div style={{ display: "flex", alignItems: "center", gap: 10, padding: 16, background: "#fff1f2", borderRadius: 10, border: "1px solid #fecdd3" }}>
                        <AlertCircle size={16} color="#e11d48" />
                        <span style={{ fontSize: 13, color: "#be123c", fontWeight: 600 }}>{error}</span>
                        <button onClick={() => fetchData(selIndex)} style={{ marginLeft: "auto", fontSize: 12, fontWeight: 700, color: "#e11d48", background: "none", border: "none", cursor: "pointer", textDecoration: "underline" }}>Retry</button>
                    </div>
                )}

                {!loading && !error && data && (
                    <>
                        {/* ════ RETURN LEADERS ════ */}
                        {activeSection === "leaders" && (
                            <div>
                                <Toolbar
                                    search={rlSearch} onSearch={v => { setRlSearch(v); setRlPage(1); }}
                                    sort={rlSort} onSort={v => { setRlSort(v); setRlPage(1); }}
                                    view={rlView} onView={setRlView}
                                    count={rlFiltered.length}
                                    sortOptions={SORT_OPTIONS.filter(s => s.key !== "volume_desc")}
                                    extraLeft={
                                        <div className="ex-period-toggle">
                                            {["1W", "1M"].map(p => (
                                                <button key={p} className={`ex-period-btn ${rlPeriod === p ? "active" : ""}`}
                                                    onClick={() => { setRlPeriod(p); setRlPage(1); }}>{p}</button>
                                            ))}
                                        </div>
                                    }
                                />

                                {rlFiltered.length === 0
                                    ? <div className="ex-empty">No stocks match your search</div>
                                    : rlView === "grid"
                                        ? (
                                            <div className="ex-grid">
                                                {rlPaged.map((s, i) => (
                                                    <GridCard key={s.symbol} stock={s} field={rlField}
                                                        rank={(rlPage - 1) * RL_PER_PAGE + i + 1}
                                                        onSelect={handleStock} />
                                                ))}
                                            </div>
                                        ) : (
                                            <div className="ex-list-wrap">
                                                <ColHeader right="Price / Return" />
                                                {rlPaged.map((s, i) => (
                                                    <ListRow key={s.symbol} stock={s} field={rlField}
                                                        rank={(rlPage - 1) * RL_PER_PAGE + i + 1}
                                                        onSelect={handleStock} />
                                                ))}
                                            </div>
                                        )
                                }
                                <Pagination page={rlPage} total={rlTotalPages} onChange={setRlPage} />
                            </div>
                        )}

                        {/* ════ MARKET PULSE ════ */}
                        {activeSection === "pulse" && (
                            <div>
                                {/* Pulse tab row */}
                                <div className="ex-pulse-tabs">
                                    {PULSE_TABS.map(tab => {
                                        const active = mpTab === tab.key;
                                        return (
                                            <button key={tab.key} className="ex-pulse-tab"
                                                style={{
                                                    background: active ? tab.bg : "#fff",
                                                    borderColor: active ? tab.accent : "#e2e8f0",
                                                    color: active ? tab.accent : "#64748b",
                                                }}
                                                onClick={() => { setMpTab(tab.key); setMpPage(1); setMpSearch(""); }}>
                                                <tab.Icon size={11} />
                                                {tab.label}
                                                {active && (
                                                    <span style={{ background: tab.pill, color: tab.accent, borderRadius: 4, fontSize: 9, fontWeight: 800, padding: "1px 5px" }}>
                                                        {mpRaw.length}
                                                    </span>
                                                )}
                                            </button>
                                        );
                                    })}
                                </div>

                                <Toolbar
                                    search={mpSearch} onSearch={v => { setMpSearch(v); setMpPage(1); }}
                                    sort={mpSort} onSort={v => { setMpSort(v); setMpPage(1); }}
                                    view={mpView} onView={setMpView}
                                    count={mpFiltered.length}
                                    sortOptions={SORT_OPTIONS}
                                />

                                {mpFiltered.length === 0
                                    ? <div className="ex-empty">No stocks match your search</div>
                                    : mpView === "grid"
                                        ? (
                                            <div className="ex-grid">
                                                {mpPaged.map((s, i) => (
                                                    <GridCard key={s.symbol} stock={s} field="changePercent"
                                                        rank={(mpPage - 1) * MP_PER_PAGE + i + 1}
                                                        onSelect={handleStock} />
                                                ))}
                                            </div>
                                        ) : (
                                            <div className="ex-list-wrap">
                                                <ColHeader right="Price (1D)" />
                                                {mpPaged.map((s, i) => (
                                                    <ListRow key={s.symbol} stock={s} field="changePercent"
                                                        rank={(mpPage - 1) * MP_PER_PAGE + i + 1}
                                                        onSelect={handleStock}
                                                        extra={
                                                            mpTab === "volumeShockers"
                                                                ? (<><div style={{ fontSize: 11, fontWeight: 700, color: "#7c3aed" }}>{s.handover?.toFixed(2)}%</div><div style={{ fontSize: 9, color: "#94a3b8" }}>turnover</div></>)
                                                                : (<><div style={{ fontSize: 11, color: "#64748b" }}>{((s.volume || 0) / 1e5).toFixed(1)}L</div><div style={{ fontSize: 9, color: "#94a3b8" }}>vol</div></>)
                                                        }
                                                    />
                                                ))}
                                            </div>
                                        )
                                }
                                <Pagination page={mpPage} total={mpTotalPages} onChange={setMpPage} />
                            </div>
                        )}
                    </>
                )}
            </div>
        </>
    );
}