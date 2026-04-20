import React, { useState } from "react";
import { TrendingUp, ChevronRight, ChevronDown } from "lucide-react";

const INDICES = [
  "NIFTY 100", "NIFTY 500", "MIDCAP 100", "SMALLCAP 100", "NIFTY TOTAL MARKET",
];
const PREVIEW_COUNT = 6;
const getLogo = (sym) =>
  `https://assets-netstorage.groww.in/stock-assets/logos2/${sym.replace(".NS","").toUpperCase()}.webp`;

/* ── FIXED SPARKLINE ─────────────────────────────────────────────────────── */
/* Key fixes:
   1. viewBox is calculated from ACTUAL data range, not hard-coded 0–100
   2. Padding added inside SVG so line never clips at edges
   3. Dashed baseline is prominent (thick + visible colour)
   4. preserveAspectRatio="none" fills the allocated box cleanly            */
function Sparkline({ data, positive, width = 64, height = 28 }) {
  if (!data || data.length < 2) {
    return <div style={{ width, height, borderRadius: 4, background: "#f1f5f9" }} />;
  }

  const PAD   = 2;                            // inner padding so stroke isn't clipped
  const W     = width  - PAD * 2;
  const H     = height - PAD * 2;

  const min   = Math.min(...data);
  const max   = Math.max(...data);
  const range = max - min || 1;

  // baseline = 7-day average
  const last7  = data.slice(-7);
  const avg    = last7.reduce((a, b) => a + b, 0) / last7.length;
  const baseY  = PAD + H - ((avg - min) / range) * H;

  const points = data
    .map((v, i) => {
      const x = PAD + (i / (data.length - 1)) * W;
      const y = PAD + H - ((v - min) / range) * H;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(" ");

  const lineColor = positive ? "#10b981" : "#f43f5e";

  return (
    <svg
      width={width}
      height={height}
      viewBox={`0 0 ${width} ${height}`}
      fill="none"
      style={{ display: "block", flexShrink: 0, overflow: "visible" }}
    >
      {/* Dashed baseline — thicker + clearly visible */}
      <line
        x1={PAD}      y1={baseY}
        x2={width - PAD} y2={baseY}
        stroke="#cbd5e1"
        strokeWidth="1.2"
        strokeDasharray="4 3"
      />
      {/* Price line */}
      <polyline
        points={points}
        stroke={lineColor}
        strokeWidth="1.5"
        fill="none"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/* ── LOGO ────────────────────────────────────────────────────────────────── */
function Logo({ symbol, size = 32 }) {
  const [err, setErr] = useState(false);
  const t = symbol.replace(".NS","").toUpperCase();
  if (err) return (
    <div style={{
      width: size, height: size, borderRadius: 8,
      background: "#4f46e5", color: "#fff",
      display: "flex", alignItems: "center", justifyContent: "center",
      fontSize: 9, fontWeight: 700, flexShrink: 0,
    }}>
      {t.slice(0, 3)}
    </div>
  );
  return (
    <div style={{
      width: size, height: size, borderRadius: 8,
      background: "#f8f8f8", border: "0.5px solid #e5e7eb",
      display: "flex", alignItems: "center", justifyContent: "center",
      overflow: "hidden", flexShrink: 0,
    }}>
      <img
        src={getLogo(symbol)} alt={t}
        style={{ width: size * 0.75, height: size * 0.75, objectFit: "contain" }}
        onError={() => setErr(true)}
      />
    </div>
  );
}

/* ── LIST ROW ────────────────────────────────────────────────────────────── */
function StockRow({ stock, returnField, onSelect, rank }) {
  const val      = stock[returnField];
  const positive = val >= 0;
  const color    = positive ? "#059669" : "#e11d48";

  return (
    <div
      onClick={() => onSelect(stock.symbol)}
      className="rl-row"
    >
      <span className="rl-rank">{rank}</span>
      <Logo symbol={stock.symbol} size={30} />
      <div className="rl-name">
        <div className="rl-sym">{stock.symbol}</div>
        <div className="rl-company">{stock.companyName}</div>
      </div>
      {/* Hide sparkline on very small screens */}
      <div className="rl-spark">
        <Sparkline data={stock.sparkline} positive={positive} width={60} height={26} />
      </div>
      <div className="rl-price">
        <div style={{ fontSize: 12, fontWeight: 700, color: "#0f172a" }}>
          ₹{stock.price.toLocaleString("en-IN")}
        </div>
        <div style={{ fontSize: 10, fontWeight: 700, color }}>
          {positive ? "+" : ""}{val.toFixed(2)}%
        </div>
      </div>
    </div>
  );
}

/* ── MAIN COMPONENT ──────────────────────────────────────────────────────── */
export default function ReturnLeaders({ data, selectedIndex, onIndexChange, onSelectStock }) {
  const [period, setPeriod]   = useState("1W");
  const [expanded, setExpanded] = useState(false);
  if (!data) return null;

  const list  = period === "1W" ? (data.topReturnsWeekly || []) : (data.topReturnsMonthly || []);
  const sorted = [...list].sort((a, b) =>
    period === "1W" ? b.return1W - a.return1W : b.return1M - a.return1M
  );
  const field   = period === "1W" ? "return1W" : "return1M";
  const shown   = expanded ? sorted : sorted.slice(0, PREVIEW_COUNT);

  return (
    <>
      <style>{`
        /* ─── HEADER ──────────────────────────────── */
        .rl-header {
          display: flex;
          align-items: flex-start;
          justify-content: space-between;
          margin-bottom: 14px;
          gap: 10px;
          flex-wrap: wrap;
        }
        .rl-title { font-size: 15px; font-weight: 700; color: #0f172a; letter-spacing: -0.4px; }
        .rl-sub   { font-size: 11px; color: #94a3b8; margin-top: 2px; }

        .rl-controls {
          display: flex;
          align-items: center;
          gap: 8px;
          flex-wrap: wrap;
        }
        .rl-toggle {
          display: flex;
          background: #f1f5f9;
          border-radius: 8px;
          padding: 3px;
          gap: 2px;
        }
        .rl-toggle-btn {
          padding: 3px 12px;
          border-radius: 6px;
          font-size: 11px;
          font-weight: 700;
          border: none;
          cursor: pointer;
          transition: all 0.15s;
        }
        .rl-select {
          font-size: 11px;
          font-weight: 700;
          color: #64748b;
          background: #f1f5f9;
          border: none;
          border-radius: 8px;
          padding: 5px 8px;
          cursor: pointer;
          outline: none;
          appearance: none;
          max-width: 140px;
        }

        /* ─── CARD GRID ───────────────────────────── */
        /* Default: 3 columns */
        .rl-cards {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 10px;
          margin-bottom: 16px;
        }
        /* ≤ 480px: 2 columns */
        @media (max-width: 480px) {
          .rl-cards { grid-template-columns: repeat(2, 1fr); gap: 8px; }
        }

        .rl-card {
          background: #fff;
          border: 0.5px solid #e2e8f0;
          border-radius: 14px;
          padding: 12px 10px;
          cursor: pointer;
          transition: all 0.18s ease;
          display: flex;
          flex-direction: column;
          gap: 7px;
          box-sizing: border-box;
          min-width: 0;
        }
        .rl-card:hover {
          background: #f0f4ff;
          border-color: #c7d2fe;
          box-shadow: 0 6px 16px rgba(79,70,229,0.1);
          transform: translateY(-3px);
        }
        .rl-card-top {
          display: flex;
          align-items: center;
          justify-content: space-between;
        }
        .rl-badge {
          font-size: 9px; font-weight: 800;
          color: #c7d2fe; background: #eef2ff;
          border-radius: 5px; padding: 1px 6px;
        }
        .rl-card-sym {
          font-size: 10px; font-weight: 800; color: #0f172a; margin-top: 2px;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .rl-card-name {
          font-size: 9px; color: #94a3b8; line-height: 1.3;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .rl-card-spark {
          display: flex;
          justify-content: center;
          align-items: center;
          min-height: 28px;
        }
        .rl-card-price {
          font-size: 12px; font-weight: 800; color: #0f172a;
          font-variant-numeric: tabular-nums;
          white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .rl-card-ret {
          font-size: 10px; font-weight: 700; margin-top: 1px;
        }

        /* ─── LIST ROWS ───────────────────────────── */
        .rl-list {
          background: #fff;
          border: 0.5px solid #e2e8f0;
          border-radius: 14px;
          padding: 2px 12px;
          box-sizing: border-box;
        }
        .rl-row {
          display: flex;
          align-items: center;
          gap: 10px;
          padding: 9px 0;
          border-bottom: 0.5px solid #f1f5f9;
          cursor: pointer;
          transition: all 0.12s;
        }
        .rl-row:last-child { border-bottom: none; }
        .rl-row:hover { transform: translateX(2px); background: #f9fafb; }

        .rl-rank  { font-size: 10px; font-weight: 700; color: #94a3b8; min-width: 14px; text-align: right; flex-shrink: 0; }
        .rl-name  { flex: 1; min-width: 0; }
        .rl-sym   { font-size: 12px; font-weight: 700; color: #0f172a; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .rl-company { font-size: 10px; color: #64748b; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .rl-spark { flex-shrink: 0; }
        .rl-price { text-align: right; min-width: 78px; flex-shrink: 0; }

        /* Hide sparkline column on very small screens */
        @media (max-width: 380px) {
          .rl-spark { display: none; }
        }

        .rl-more-btn {
          width: 100%; padding: 10px 0; background: none; border: none;
          border-top: 0.5px solid #f1f5f9; cursor: pointer;
          display: flex; align-items: center; justify-content: center; gap: 5px;
          font-size: 12px; font-weight: 700; color: #4f46e5;
        }
        .rl-more-btn:hover { color: #3730a3; }
      `}</style>

      {/* HEADER */}
      <div className="rl-header">
        <div>
          <div className="rl-title">Return leaders</div>
          <div className="rl-sub">Top performers by period return</div>
        </div>
        <div className="rl-controls">
          <div className="rl-toggle">
            {["1W","1M"].map(p => (
              <button
                key={p}
                className="rl-toggle-btn"
                onClick={() => { setPeriod(p); setExpanded(false); }}
                style={{
                  background: period === p ? "#fff" : "transparent",
                  color: period === p ? "#4f46e5" : "#64748b",
                  boxShadow: period === p ? "0 1px 3px rgba(0,0,0,0.08)" : "none",
                }}
              >{p}</button>
            ))}
          </div>
          <select
            className="rl-select"
            value={selectedIndex}
            onChange={e => { onIndexChange(e.target.value); setExpanded(false); }}
          >
            {INDICES.map(i => <option key={i} value={i}>{i}</option>)}
          </select>
        </div>
      </div>

      {/* TOP CARDS GRID — always 3 cols (2 on mobile) */}
      <div className="rl-cards">
        {sorted.slice(0, PREVIEW_COUNT).map((stock, i) => {
          const val = stock[field];
          const pos = val >= 0;
          return (
            <div
              key={stock.symbol}
              className="rl-card"
              onClick={() => onSelectStock(stock.symbol)}
            >
              <div className="rl-card-top">
                <Logo symbol={stock.symbol} size={30} />
                <span className="rl-badge">#{i + 1}</span>
              </div>
              <div>
                <div className="rl-card-sym">{stock.symbol}</div>
                <div className="rl-card-name">{stock.companyName}</div>
              </div>
              <div className="rl-card-spark">
                <Sparkline data={stock.sparkline} positive={pos} width={58} height={26} />
              </div>
              <div>
                <div className="rl-card-price">₹{stock.price.toLocaleString("en-IN")}</div>
                <div className="rl-card-ret" style={{ color: pos ? "#059669" : "#e11d48" }}>
                  {pos ? "+" : ""}{val.toFixed(2)}%
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* LIST */}
      <div className="rl-list">
        {shown.map((stock, i) => (
          <StockRow
            key={stock.symbol}
            stock={stock}
            returnField={field}
            onSelect={onSelectStock}
            rank={i + 1}
          />
        ))}
        {sorted.length > PREVIEW_COUNT && (
          <button className="rl-more-btn" onClick={() => setExpanded(!expanded)}>
            {expanded
              ? <><ChevronDown size={13}/> Show less</>
              : <><ChevronRight size={13}/> See all {sorted.length} stocks</>
            }
          </button>
        )}
      </div>
    </>
  );
}