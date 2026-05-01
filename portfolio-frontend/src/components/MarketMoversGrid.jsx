import React, { useState } from "react";
import { TrendingUp, TrendingDown, Zap, ChevronRight } from "lucide-react";
import { useNavigate } from "react-router-dom";

const PREVIEW_COUNT = 6;
const getLogo = (sym) =>
  `https://assets-netstorage.groww.in/stock-assets/logos2/${sym.replace(".NS", "").toUpperCase()}.webp`;

const TABS = [
  { key: "gainers1D",      label: "Surging", Icon: TrendingUp,  color: "#059669", bg: "#ecfdf5" },
  { key: "losers1D",       label: "Sliding",  Icon: TrendingDown, color: "#e11d48", bg: "#fff1f2" },
  { key: "volumeShockers", label: "Buzzing",  Icon: Zap,         color: "#7c3aed", bg: "#f5f3ff" },
];

/* Sparkline */
function Sparkline({ data, positive, width = 72, height = 28 }) {
  if (!data || data.length < 2) {
    return <div style={{ width, height, borderRadius: 4, background: "#f1f5f9", flexShrink: 0 }} />;
  }
  const PAD = 2, W = width - PAD * 2, H = height - PAD * 2;
  const min = Math.min(...data), max = Math.max(...data);
  const range = max - min || 1;
  const last7 = data.slice(-7);
  const avg = last7.reduce((a, b) => a + b, 0) / last7.length;
  const baseY = PAD + H - ((avg - min) / range) * H;
  const points = data.map((v, i) => {
    const x = PAD + (i / (data.length - 1)) * W;
    const y = PAD + H - ((v - min) / range) * H;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  }).join(" ");

  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} fill="none"
      style={{ display: "block", flexShrink: 0, overflow: "visible" }}>
      <line x1={PAD} y1={baseY} x2={width - PAD} y2={baseY}
        stroke="#94a3b8" strokeWidth="1.2" strokeDasharray="4 3" opacity="0.8" />
      <polyline points={points} stroke={positive ? "#10b981" : "#f43f5e"}
        strokeWidth="1.5" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* Logo  */
function LogoAvatar({ symbol }) {
  const [failed, setFailed] = useState(false);
  const ticker = symbol.replace(".NS", "").toUpperCase();
  if (failed) return (
    <div style={{ width: 36, height: 36, borderRadius: 10, background: "#4f46e5", color: "#fff", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 9, fontWeight: 800, flexShrink: 0 }}>
      {ticker.slice(0, 3)}
    </div>
  );
  return (
    <div style={{ width: 36, height: 36, borderRadius: 10, background: "#f8fafc", border: "0.5px solid #e2e8f0", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden", flexShrink: 0 }}>
      <img src={getLogo(symbol)} alt={ticker} style={{ width: 28, height: 28, objectFit: "contain" }} onError={() => setFailed(true)} />
    </div>
  );
}

/* ─── Stock Row ────────────────────────────────────────────────────────────── */
function StockRow({ stock, activeKey, onSelect }) {
  const isUp = stock.changePercent >= 0;
  const isVol = activeKey === "volumeShockers";
  const chgColor = isUp ? "#059669" : "#e11d48";

  return (
    <div onClick={() => onSelect(stock.symbol)} className="mmg-row">
      <LogoAvatar symbol={stock.symbol} />
      <div className="mmg-name">
        <div className="mmg-sym">{stock.symbol.replace(".NS", "")}</div>
        <div className="mmg-company">{stock.companyName}</div>
      </div>
      <div className="mmg-spark">
        <Sparkline data={stock.sparkline} positive={isUp} width={68} height={26} />
      </div>
      <div className="mmg-price">
        <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a", fontVariantNumeric: "tabular-nums" }}>
          ₹{stock.price.toLocaleString("en-IN")}
        </div>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 3, marginTop: 2 }}>
          {isUp ? <TrendingUp size={9} color={chgColor} /> : <TrendingDown size={9} color={chgColor} />}
          <span style={{ fontSize: 11, fontWeight: 700, color: chgColor }}>
            {Math.abs(stock.changePercent).toFixed(2)}%
          </span>
        </div>
      </div>
      <div className="mmg-vol">
        {isVol ? (
          <>
            <div style={{ fontSize: 11, fontWeight: 700, color: "#7c3aed" }}>{stock.handover?.toFixed(2)}%</div>
            <div style={{ fontSize: 9, color: "#94a3b8" }}>turnover</div>
          </>
        ) : (
          <>
            <div style={{ fontSize: 11, color: "#64748b" }}>{((stock.volume || 0) / 1e5).toFixed(1)}L</div>
            <div style={{ fontSize: 9, color: "#94a3b8" }}>vol</div>
          </>
        )}
      </div>
    </div>
  );
}

/* MAIN COMPONENT */
export default function MarketMoversGrid({ data, onSelectStock, selectedIndex }) {
  const [activeTab, setActiveTab] = useState("gainers1D");
  const navigate = useNavigate();

  if (!data) return null;

  const list = data[activeTab] || [];
  const displayed = list.slice(0, PREVIEW_COUNT);
  const cfg = TABS.find(t => t.key === activeTab);

  const handleSeeAll = () => {
    navigate(`/market?index=${encodeURIComponent(selectedIndex || data?.index || "NIFTY 100")}&tab=${activeTab}`);
  };

  return (
    <>
      <style>{`
        .mmg-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:12px; flex-wrap:wrap; gap:8px; }
        .mmg-title { font-size:15px; font-weight:700; color:#0f172a; letter-spacing:-0.4px; }
        .mmg-sub   { font-size:11px; color:#94a3b8; margin-top:2px; }
        .mmg-tabs  { display:flex; background:#f1f5f9; border-radius:10px; padding:3px; gap:2px; }
        .mmg-tab-btn { display:flex; align-items:center; gap:4px; padding:4px 10px; border-radius:8px; font-size:11px; font-weight:700; border:none; cursor:pointer; transition:all 0.15s; white-space:nowrap; }
        .mmg-badge-row { display:flex; align-items:center; gap:6px; margin-bottom:8px; flex-wrap:wrap; }
        .mmg-badge { display:inline-flex; align-items:center; gap:4px; border-radius:7px; padding:3px 10px; font-size:11px; font-weight:700; }

        .mmg-col-headers { display:flex; align-items:center; gap:10px; padding:0 0 7px; border-bottom:0.5px solid #e2e8f0; margin-bottom:2px; }
        .mmg-ch { font-size:9px; font-weight:700; color:#94a3b8; text-transform:uppercase; letter-spacing:0.5px; }
        .mmg-row { display:flex; align-items:center; gap:10px; padding:10px 0; border-bottom:0.5px solid #f1f5f9; cursor:pointer; transition:all 0.12s; min-width:0; }
        .mmg-row:last-child{ border-bottom:none; }
        .mmg-row:hover{ background:#fafbff; border-radius:8px; }
        .mmg-name  { flex:1; min-width:0; }
        .mmg-sym   { font-size:12px; font-weight:700; color:#0f172a; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
        .mmg-company{ font-size:10px; color:#94a3b8; margin-top:1px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
        .mmg-spark { flex-shrink:0; }
        .mmg-price { text-align:right; min-width:95px; flex-shrink:0; }
        .mmg-vol   { text-align:right; min-width:64px; flex-shrink:0; }
        @media(max-width:380px){ .mmg-spark{ display:none; } .mmg-vol{ display:none; } .mmg-price{ min-width:80px; } }

        .mmg-see-all-btn { margin-top:6px; width:100%; padding:11px 0; background:none; border:none; border-top:0.5px solid #f1f5f9; cursor:pointer; display:flex; align-items:center; justify-content:center; gap:6px; font-size:12px; font-weight:700; color:#4f46e5; transition:color 0.12s; }
        .mmg-see-all-btn:hover{ color:#3730a3; }
        .mmg-see-all-count { font-size:10px; font-weight:800; background:#eef2ff; color:#4f46e5; border-radius:5px; padding:1px 7px; }
      `}</style>

      {/* HEADER */}
      <div className="mmg-header">
        <div>
          <div className="mmg-title">Market pulse</div>
          <div className="mmg-sub">{data.index} · {data.totalStocks} stocks</div>
        </div>
        <div className="mmg-tabs">
          {TABS.map(tab => {
            const active = activeTab === tab.key;
            return (
              <button key={tab.key} className="mmg-tab-btn"
                onClick={() => setActiveTab(tab.key)}
                style={{
                  background: active ? "#fff" : "transparent",
                  color: active ? tab.color : "#64748b",
                  boxShadow: active ? "0 1px 3px rgba(0,0,0,0.08)" : "none",
                }}>
                <tab.Icon size={9} />{tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* ACTIVE BADGE */}
      <div className="mmg-badge-row">
        <div className="mmg-badge" style={{ background: cfg.bg, color: cfg.color }}>
          <cfg.Icon size={9} />{cfg.label} today
        </div>
        <span style={{ fontSize: 11, color: "#94a3b8" }}>{list.length} stocks</span>
      </div>

      {/* COLUMN HEADERS */}
      <div className="mmg-col-headers">
        <div style={{ width: 36, flexShrink: 0 }} />
        <div className="mmg-ch" style={{ flex: 1 }}>Company</div>
        <div className="mmg-ch mmg-spark" style={{ width: 68 }} />
        <div className="mmg-ch" style={{ textAlign: "right", minWidth: 95 }}>Price (1D)</div>
        <div className="mmg-ch mmg-vol" style={{ textAlign: "right", minWidth: 64 }}>
          {activeTab === "volumeShockers" ? "Turnover" : "Volume"}
        </div>
      </div>

      {/* ROWS */}
      {displayed.map(stock => (
        <StockRow key={stock.symbol} stock={stock} activeKey={activeTab}
          onSelect={onSelectStock} />
      ))}

      {/* SEE ALL */}
      {list.length > PREVIEW_COUNT && (
        <button className="mmg-see-all-btn" onClick={handleSeeAll}>
          <ChevronRight size={13} />
          See all
          <span className="mmg-see-all-count">{list.length}</span>
          {cfg.label.toLowerCase()} stocks
        </button>
      )}
    </>
  );
}