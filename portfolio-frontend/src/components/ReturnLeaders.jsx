import React, { useState } from "react";
import { TrendingUp, ChevronRight, ChevronDown } from "lucide-react";

const INDICES = [
  "NIFTY 100",
  "NIFTY 500",
  "MIDCAP 100",
  "SMALLCAP 100",
  "NIFTY TOTAL MARKET",
];

// Updated to 6 for the 3x2 symmetrical layout
const PREVIEW_COUNT = 6;

const getGrowwLogo = (symbol) =>
  `https://assets-netstorage.groww.in/stock-assets/logos2/${symbol
    .replace(".NS", "")
    .toUpperCase()}.webp`;

function Sparkline({ data, positive }) {
  if (!data || data.length < 2) return null;

  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;

  const w = 64;
  const h = 28;

  const last7 = data.slice(-7);
  const avg = last7.reduce((a, b) => a + b, 0) / last7.length;
  const avgY = h - ((avg - min) / range) * h;

  const pts = data
    .map((v, i) => {
      const x = (i / (data.length - 1)) * w;
      const y = h - ((v - min) / range) * h;
      return `${x},${y}`;
    })
    .join(" ");

  const color = positive ? "#10b981" : "#f43f5e";

  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} fill="none">
      <line
        x1="0"
        x2={w}
        y1={avgY}
        y2={avgY}
        stroke="#94a3b8"
        strokeWidth="1"
        strokeDasharray="3 4"
        opacity="0.3" // More subtle baseline
      />
      <polyline
        points={pts}
        stroke={color}
        strokeWidth="1.2" // Finner trend line
        fill="none"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function LogoAvatar({ symbol }) {
  const [failed, setFailed] = useState(false);
  const ticker = symbol.replace(".NS", "").toUpperCase();
  if (failed) {
    return (
      <div style={{
        width: 32, height: 32, borderRadius: 8,
        background: "#4f46e5", color: "#fff",
        display: "flex", alignItems: "center", justifyContent: "center",
        fontSize: 10, fontWeight: 700, flexShrink: 0,
        letterSpacing: "-0.5px",
      }}>
        {ticker.slice(0, 3)}
      </div>
    );
  }
  return (
    <div style={{
      width: 32, height: 32, borderRadius: 8,
      background: "#f8f8f8", border: "0.5px solid #e5e7eb",
      display: "flex", alignItems: "center", justifyContent: "center",
      overflow: "hidden", flexShrink: 0,
    }}>
      <img
        src={getGrowwLogo(symbol)}
        alt={ticker}
        style={{ width: 24, height: 24, objectFit: "contain" }}
        onError={() => setFailed(true)}
      />
    </div>
  );
}

function ReturnBadge({ value, period }) {
  const positive = value >= 0;
  return (
    <div style={{
      display: "inline-flex", alignItems: "center", gap: 3,
      background: positive ? "#ecfdf5" : "#fff1f2",
      color: positive ? "#059669" : "#e11d48",
      borderRadius: 6, padding: "2px 6px", fontSize: 10, fontWeight: 700,
    }}>
      <TrendingUp size={9} style={{ transform: positive ? "none" : "scaleY(-1)" }} />
      {positive ? "+" : ""}{value.toFixed(2)}% {period}
    </div>
  );
}

function StockRow({ stock, returnField, period, onSelect, rank }) {
  const val = stock[returnField];
  const positive = val >= 0;
  return (
    <div
      onClick={() => onSelect(stock.symbol)}
      style={{
        display: "flex", alignItems: "center", gap: 12,
        padding: "10px 0",
        borderBottom: "0.5px solid #f1f5f9",
        cursor: "pointer",
        transition: "all 0.1s ease-in-out",
        transform: "none",
      }}
      onMouseEnter={e => {
        e.currentTarget.style.background = "#f9fafb";
        e.currentTarget.style.transform = "translateX(2px)";
      }}
      onMouseLeave={e => {
        e.currentTarget.style.background = "transparent";
        e.currentTarget.style.transform = "none";
      }}
    >
      <span style={{ fontSize: 11, fontWeight: 700, color: "#94a3b8", minWidth: 16, textAlign: "right" }}>
        {rank}
      </span>
      <LogoAvatar symbol={stock.symbol} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: "#0f172a", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
          {stock.symbol}
        </div>
        <div style={{ fontSize: 10, color: "#64748b", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
          {stock.companyName}
        </div>
      </div>
      <div style={{ marginLeft: "auto" }}>
        <Sparkline data={stock.sparkline} positive={positive} />
      </div>
      <div style={{ textAlign: "right", minWidth: 80 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a" }}>
          ₹{stock.price.toLocaleString("en-IN")}
        </div>
        <div style={{ fontSize: 10, fontWeight: 700, color: positive ? "#059669" : "#e11d48" }}>
          {positive ? "+" : ""}{val.toFixed(2)}%
        </div>
      </div>
    </div>
  );
}

export default function ReturnLeaders({ data, selectedIndex, onIndexChange, onSelectStock }) {
  const [period, setPeriod] = useState("1W");
  const [expanded, setExpanded] = useState(false);

  if (!data) return null;

  const list = period === "1W" ? (data.topReturnsWeekly || []) : (data.topReturnsMonthly || []);
  const sorted = [...list].sort((a, b) =>
    period === "1W" ? b.return1W - a.return1W : b.return1M - a.return1M
  );
  const returnField = period === "1W" ? "return1W" : "return1M";
  const displayed = expanded ? sorted : sorted.slice(0, PREVIEW_COUNT);

  return (
    <div style={{ width: "100%" }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 16, flexWrap: "wrap", gap: 10 }}>
        <div>
          <div style={{ fontSize: 16, fontWeight: 700, color: "#0f172a", letterSpacing: "-0.5px" }}>
            Return leaders
          </div>
          <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
            Top performers by period return
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <div style={{ display: "flex", background: "#f1f5f9", borderRadius: 8, padding: 3, gap: 2 }}>
            {["1W", "1M"].map(p => (
              <button
                key={p}
                onClick={() => { setPeriod(p); setExpanded(false); }}
                style={{
                  padding: "4px 12px", borderRadius: 6, fontSize: 11, fontWeight: 700,
                  border: "none", cursor: "pointer", transition: "all 0.15s",
                  background: period === p ? "#fff" : "transparent",
                  color: period === p ? "#4f46e5" : "#64748b",
                  boxShadow: period === p ? "0 1px 3px rgba(0,0,0,0.08)" : "none",
                }}
              >{p}</button>
            ))}
          </div>

          <select
            value={selectedIndex}
            onChange={e => { onIndexChange(e.target.value); setExpanded(false); }}
            style={{
              fontSize: 11, fontWeight: 700, color: "#64748b",
              background: "#f1f5f9", border: "none", borderRadius: 8,
              padding: "6px 8px", cursor: "pointer", outline: "none",
              appearance: "none", paddingRight: 20,
            }}
          >
            {INDICES.map(idx => (
              <option key={idx} value={idx}>{idx}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Symmetrical 3x2 Grid */}
      <div style={{ 
        display: "grid", 
        gridTemplateColumns: "repeat(3, 1fr)", // Forces exactly 3 columns
        gap: 12, 
        marginBottom: 20 
      }}>
        {sorted.slice(0, PREVIEW_COUNT).map((stock, i) => {
          const val = stock[returnField];
          const positive = val >= 0;
          return (
            <div
              key={stock.symbol}
              onClick={() => onSelectStock(stock.symbol)}
              style={{
                background: "#fff", border: "0.5px solid #e2e8f0",
                borderRadius: 16, padding: "14px",
                cursor: "pointer", transition: "all 0.2s ease-in-out",
                display: "flex", flexDirection: "column", gap: 8,
                transform: "none",
              }}
              onMouseEnter={e => {
                e.currentTarget.style.background = "#f0f4ff"; // Light blue background
                e.currentTarget.style.borderColor = "#c7d2fe";
                e.currentTarget.style.boxShadow = "0 8px 16px rgba(0,0,0,0.08)";
                e.currentTarget.style.transform = "scale(1.02) translateY(-4px)"; // Scale and Lift
                const badge = e.currentTarget.querySelector('.rank-badge');
                if(badge) badge.style.transform = "scale(1.1) rotate(-5deg)";
              }}
              onMouseLeave={e => {
                e.currentTarget.style.background = "#fff";
                e.currentTarget.style.borderColor = "#e2e8f0";
                e.currentTarget.style.boxShadow = "none";
                e.currentTarget.style.transform = "none";
                const badge = e.currentTarget.querySelector('.rank-badge');
                if(badge) badge.style.transform = "none";
              }}
            >
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <LogoAvatar symbol={stock.symbol} />
                <span 
                  className="rank-badge"
                  style={{ 
                    fontSize: 10, fontWeight: 800, color: "#c7d2fe", 
                    background: "#eef2ff", borderRadius: 6, padding: "2px 6px",
                    transition: "transform 0.2s ease-out" 
                  }}
                >
                  #{i + 1}
                </span>
              </div>
              <div>
                <div style={{ fontSize: 11, fontWeight: 800, color: "#0f172a" }}>
                  {stock.symbol}
                </div>
                <div style={{ fontSize: 10, color: "#94a3b8", lineHeight: 1.3, marginTop: 1, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {stock.companyName}
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'center'}}>
                <Sparkline data={stock.sparkline} positive={positive} />
              </div>
              <div>
                <div style={{ fontSize: 13, fontWeight: 800, color: "#0f172a" }}>
                  ₹{stock.price.toLocaleString("en-IN")}
                </div>
                <div style={{ fontSize: 11, fontWeight: 700, color: positive ? "#059669" : "#e11d48", marginTop: 2 }}>
                  {positive ? "+" : ""}{val.toFixed(2)}%
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* List section */}
      <div style={{ background: "#fff", border: "0.5px solid #e2e8f0", borderRadius: 16, padding: "4px 16px" }}>
        {displayed.map((stock, i) => (
          <StockRow
            key={stock.symbol}
            stock={stock}
            returnField={returnField}
            period={period}
            onSelect={onSelectStock}
            rank={i + 1}
          />
        ))}

        {sorted.length > PREVIEW_COUNT && (
          <button
            onClick={() => setExpanded(!expanded)}
            style={{
              width: "100%", padding: "12px 0", background: "none",
              border: "none", borderTop: "0.5px solid #f1f5f9",
              cursor: "pointer", display: "flex", alignItems: "center",
              justifyContent: "center", gap: 5,
              fontSize: 12, fontWeight: 700, color: "#4f46e5",
            }}
          >
            {expanded ? (
              <><ChevronDown size={14} /> Show less</>
            ) : (
              <><ChevronRight size={14} /> See all {sorted.length} stocks</>
            )}
          </button>
        )}
      </div>
    </div>
  );
}