import React, { useState } from "react";
import { TrendingUp, TrendingDown, Zap, ChevronRight, ChevronDown } from "lucide-react";

const PREVIEW_COUNT = 6;

const getGrowwLogo = (symbol) =>
  `https://assets-netstorage.groww.in/stock-assets/logos2/${symbol
    .replace(".NS", "")
    .toUpperCase()}.webp`;

const TABS = [
  { key: "gainers1D", label: "Surging", icon: TrendingUp, color: "#059669", bg: "#ecfdf5" },
  { key: "losers1D", label: "Sliding", icon: TrendingDown, color: "#e11d48", bg: "#fff1f2" },
  { key: "volumeShockers", label: "Buzzing", icon: Zap, color: "#7c3aed", bg: "#f5f3ff" },
];

function Sparkline({ data, positive }) {
  if (!data || data.length < 2) return null;

  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;

  const w = 72;
  const h = 28;

  // 👉 average of last 7 values
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
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} fill="none" style={{ flexShrink: 0 }}>
      
      {/* 🔹 Baseline (avg 7 days) */}
      <line
        x1="0"
        x2={w}
        y1={avgY}
        y2={avgY}
        stroke="#94a3b8"
        strokeWidth="1"
        strokeDasharray="3 3"
        opacity="0.7"
      />

      {/* 🔹 Actual price line */}
      <polyline
        points={pts}
        stroke={color}
        strokeWidth="1.5"
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
        width: 38, height: 38, borderRadius: 12,
        background: "#4f46e5", color: "#fff",
        display: "flex", alignItems: "center", justifyContent: "center",
        fontSize: 10, fontWeight: 800, flexShrink: 0,
        letterSpacing: "-0.3px",
      }}>
        {ticker.slice(0, 3)}
      </div>
    );
  }
  return (
    <div style={{
      width: 38, height: 38, borderRadius: 12,
      background: "#f8fafc", border: "0.5px solid #e2e8f0",
      display: "flex", alignItems: "center", justifyContent: "center",
      overflow: "hidden", flexShrink: 0,
    }}>
      <img
        src={getGrowwLogo(symbol)}
        alt={ticker}
        style={{ width: 30, height: 30, objectFit: "contain" }}
        onError={() => setFailed(true)}
      />
    </div>
  );
}

function StockRow({ stock, activeKey, onSelect }) {
  const isUp = stock.changePercent >= 0;
  const isVolume = activeKey === "volumeShockers";
  const changeColor = isUp ? "#059669" : "#e11d48";

  return (
    <div
      onClick={() => onSelect(stock.symbol)}
      style={{
        display: "flex", alignItems: "center", gap: 12,
        padding: "11px 0",
        borderBottom: "0.5px solid #f1f5f9",
        cursor: "pointer",
        transition: "background 0.12s",
        borderRadius: 4,
      }}
      onMouseEnter={e => e.currentTarget.style.background = "#fafbff"}
      onMouseLeave={e => e.currentTarget.style.background = "transparent"}
    >
      <LogoAvatar symbol={stock.symbol} />

      {/* Name column */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a", letterSpacing: "-0.3px" }}>
          {stock.symbol}
        </div>
        <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 1, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
          {stock.companyName}
        </div>
      </div>

      {/* Sparkline */}
      <Sparkline data={stock.sparkline} positive={isUp} />

      {/* Price + change */}
      <div style={{ textAlign: "right", minWidth: 110 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a" }}>
          ₹{stock.price.toLocaleString("en-IN")}
        </div>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 3, marginTop: 2 }}>
          {isUp
            ? <TrendingUp size={10} color={changeColor} />
            : <TrendingDown size={10} color={changeColor} />
          }
          <span style={{ fontSize: 11, fontWeight: 700, color: changeColor }}>
            {Math.abs(stock.changePercent).toFixed(2)}%
          </span>
        </div>
      </div>

      {/* Volume / Handover column */}
      <div style={{ textAlign: "right", minWidth: 80, display: "flex", flexDirection: "column", alignItems: "flex-end" }}>
        {isVolume ? (
          <>
            <div style={{ fontSize: 12, fontWeight: 700, color: "#7c3aed" }}>
              {stock.handover.toFixed(2)}%
            </div>
            <div style={{ fontSize: 10, color: "#94a3b8" }}>turnover</div>
          </>
        ) : (
          <>
            <div style={{ fontSize: 12, color: "#64748b" }}>
              {(stock.volume / 1e5).toFixed(1)}L
            </div>
            <div style={{ fontSize: 10, color: "#94a3b8" }}>vol</div>
          </>
        )}
      </div>
    </div>
  );
}

export default function MarketMoversGrid({ data, onSelectStock }) {
  const [activeTab, setActiveTab] = useState("gainers1D");
  const [expanded, setExpanded] = useState(false);

  if (!data) return null;

  const list = data[activeTab] || [];
  const displayed = expanded ? list : list.slice(0, PREVIEW_COUNT);
  const activeConfig = TABS.find(t => t.key === activeTab);

  return (
    <div style={{ width: "100%" }}>
      {/* Header row */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 16, flexWrap: "wrap", gap: 8 }}>
        <div>
          <div style={{ fontSize: 16, fontWeight: 700, color: "#0f172a", letterSpacing: "-0.5px" }}>
            Market pulse
          </div>
          <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
            {data.index} · {data.totalStocks} stocks
          </div>
        </div>

        {/* Tab pills */}
        <div style={{ display: "flex", background: "#f1f5f9", borderRadius: 12, padding: 3, gap: 2 }}>
          {TABS.map(tab => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.key;
            return (
              <button
                key={tab.key}
                onClick={() => { setActiveTab(tab.key); setExpanded(false); }}
                style={{
                  display: "flex", alignItems: "center", gap: 5,
                  padding: "5px 12px", borderRadius: 9, fontSize: 11, fontWeight: 700,
                  border: "none", cursor: "pointer", transition: "all 0.15s",
                  background: isActive ? "#fff" : "transparent",
                  color: isActive ? tab.color : "#64748b",
                  boxShadow: isActive ? "0 1px 3px rgba(0,0,0,0.08)" : "none",
                }}
              >
                <Icon size={10} />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Active tab badge */}
      <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8, paddingLeft: 2 }}>
        <div style={{
          display: "inline-flex", alignItems: "center", gap: 4,
          background: activeConfig.bg, color: activeConfig.color,
          borderRadius: 8, padding: "3px 10px", fontSize: 11, fontWeight: 700,
        }}>
          <activeConfig.icon size={10} />
          {activeConfig.label} today
        </div>
        <span style={{ fontSize: 11, color: "#94a3b8" }}>{list.length} stocks</span>
      </div>

      {/* Table header */}
      <div style={{
        display: "flex", alignItems: "center", gap: 12,
        padding: "0 0 8px 0", borderBottom: "0.5px solid #e2e8f0",
        marginBottom: 2,
      }}>
        <div style={{ width: 38, flexShrink: 0 }} />
        <div style={{ flex: 1, fontSize: 10, fontWeight: 700, color: "#94a3b8", letterSpacing: "0.5px", textTransform: "uppercase" }}>
          Company
        </div>
        <div style={{ width: 72, flexShrink: 0 }} />
        <div style={{ textAlign: "right", minWidth: 110, fontSize: 10, fontWeight: 700, color: "#94a3b8", letterSpacing: "0.5px", textTransform: "uppercase" }}>
          Price (1D)
        </div>
        <div style={{ textAlign: "right", minWidth: 80, fontSize: 10, fontWeight: 700, color: "#94a3b8", letterSpacing: "0.5px", textTransform: "uppercase" }}>
          {activeTab === "volumeShockers" ? "Turnover" : "Volume"}
        </div>
      </div>

      {/* Rows */}
      <div>
        {displayed.map(stock => (
          <StockRow
            key={stock.symbol}
            stock={stock}
            activeKey={activeTab}
            onSelect={onSelectStock}
          />
        ))}
      </div>

      {/* See more */}
      {list.length > PREVIEW_COUNT && (
        <button
          onClick={() => setExpanded(!expanded)}
          style={{
            marginTop: 8, width: "100%", padding: "10px 0",
            background: "none", border: "none",
            borderTop: "0.5px solid #f1f5f9",
            cursor: "pointer", display: "flex", alignItems: "center",
            justifyContent: "center", gap: 5,
            fontSize: 12, fontWeight: 700, color: "#4f46e5",
          }}
        >
          {expanded
            ? <><ChevronDown size={14} /> Show less</>
            : <><ChevronRight size={14} /> See all {list.length} stocks</>
          }
        </button>
      )}
    </div>
  );
}