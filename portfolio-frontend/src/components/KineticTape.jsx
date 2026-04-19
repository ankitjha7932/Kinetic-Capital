import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { TrendingUp, TrendingDown, Loader2 } from "lucide-react";
import api from "../api/axios";

const KineticTape = () => {
  const [stocks, setStocks] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchTicker = async () => {
      try {
        const res = await api.get("/portfolio/ticker");
        if (res.data?.length > 0) setStocks(res.data);
      } catch (err) {
        console.error("Ticker fetch error:", err);
      } finally {
        setLoading(false);
      }
    };
    fetchTicker();
    const interval = setInterval(fetchTicker, 60000 * 15);
    return () => clearInterval(interval);
  }, []);

  if (loading && stocks.length === 0) {
    return (
      <div className="kt-root">
        <Loader2 size={12} className="kt-loader" />
        <style>{styles}</style>
      </div>
    );
  }

  if (stocks.length === 0) return null;

  const displayItems = [...stocks, ...stocks, ...stocks];

  return (
    <div className="kt-root">
      <div className="kt-track">
        {displayItems.map((stock, idx) => {
          const up = stock.changePercent >= 0;
          return (
            <Link key={`${stock.symbol}-${idx}`} to={`/stock/${stock.symbol}`} className="kt-item">
              <span className="kt-sym">{stock.symbol}</span>
              <span className="kt-price">
                ₹{stock.price.toLocaleString("en-IN", { minimumFractionDigits: 2 })}
              </span>
              <span className={`kt-chg ${up ? "kt-up" : "kt-dn"}`}>
                {up ? <TrendingUp size={9} /> : <TrendingDown size={9} />}
                {Math.abs(stock.changePercent).toFixed(2)}%
              </span>
              <span className="kt-sep">·</span>
            </Link>
          );
        })}
      </div>
      <style>{styles}</style>
    </div>
  );
};

const styles = `
.kt-root {
  width: 100%;
  height: 36px;
  background: #0a0a0f;
  border-bottom: 1px solid rgba(99,102,241,0.15);
  display: flex;
  align-items: center;
  overflow: hidden;
  position: relative;
  z-index: 100;
}
.kt-root::before, .kt-root::after {
  content: '';
  position: absolute;
  top: 0; bottom: 0;
  width: 80px;
  z-index: 2;
  pointer-events: none;
}
.kt-root::before { left: 0; background: linear-gradient(to right, #0a0a0f, transparent); }
.kt-root::after  { right: 0; background: linear-gradient(to left, #0a0a0f, transparent); }
.kt-loader { margin: auto; color: #334155; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.kt-track {
  display: flex;
  width: fit-content;
  animation: kscroll 55s linear infinite;
}
.kt-track:hover { animation-play-state: paused; }
@keyframes kscroll {
  0%   { transform: translateX(0); }
  100% { transform: translateX(-33.3333%); }
}
.kt-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 20px;
  text-decoration: none;
  transition: background 0.15s;
  cursor: pointer;
  border-right: 1px solid rgba(255,255,255,0.04);
}
.kt-item:hover { background: rgba(99,102,241,0.08); }
.kt-sym {
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.12em;
  color: #475569;
  text-transform: uppercase;
  transition: color 0.15s;
}
.kt-item:hover .kt-sym { color: #818cf8; }
.kt-price {
  font-size: 11px;
  font-weight: 700;
  color: #e2e8f0;
  font-variant-numeric: tabular-nums;
}
.kt-chg {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 10px;
  font-weight: 800;
  padding: 1px 6px;
  border-radius: 4px;
}
.kt-up { color: #34d399; background: rgba(52,211,153,0.1); }
.kt-dn { color: #f87171; background: rgba(248,113,113,0.1); }
.kt-sep { color: rgba(255,255,255,0.06); font-size: 14px; margin-left: 4px; }
`;

export default KineticTape;