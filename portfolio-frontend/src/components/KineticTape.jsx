import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom"; // 👈 Added Link for navigation
import { TrendingUp, TrendingDown, Loader2 } from "lucide-react";
import api from "../api/axios";

const KineticTape = () => {
  const [stocks, setStocks] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchTicker = async () => {
      try {
        const res = await api.get("/portfolio/ticker");
        if (res.data && res.data.length > 0) {
          setStocks(res.data);
        }
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
      <div className="w-full bg-slate-950 h-10 flex items-center justify-center border-b border-white/5">
        <Loader2 size={14} className="animate-spin text-slate-700" />
      </div>
    );
  }

  if (stocks.length === 0) return null;

  const displayItems = [...stocks, ...stocks, ...stocks];

  return (
    <div className="w-full bg-slate-950 text-white overflow-hidden h-10 flex items-center border-b border-white/5 relative z-[100] select-none shadow-2xl">
      <div className="flex animate-kinetic-scroll whitespace-nowrap">
        {displayItems.map((stock, idx) => (
          /* 🚀 Wrapped in Link for Navigation */
          <Link
            key={`${stock.symbol}-${idx}`}
            to={`/stock/${stock.symbol}`}
            className="inline-flex items-center px-10 gap-4 border-r border-white/10 group hover:bg-white/10 transition-all cursor-pointer no-underline"
          >
            <span className="text-[10px] font-black tracking-[0.2em] uppercase text-slate-500 group-hover:text-indigo-400 transition-colors">
              {stock.symbol}
            </span>

            <span className="text-xs font-bold tabular-nums text-white">
              ₹
              {stock.price.toLocaleString("en-IN", {
                minimumFractionDigits: 2,
              })}
            </span>

            <span
              className={`flex items-center gap-1 text-[10px] font-black px-1.5 py-0.5 rounded ${
                stock.changePercent >= 0
                  ? "text-emerald-400 bg-emerald-400/10"
                  : "text-rose-400 bg-rose-400/10"
              }`}
            >
              {stock.changePercent >= 0 ? (
                <TrendingUp size={12} />
              ) : (
                <TrendingDown size={12} />
              )}
              {Math.abs(stock.changePercent).toFixed(2)}%
            </span>
          </Link>
        ))}
      </div>

      <style
        dangerouslySetInnerHTML={{
          __html: `
        @keyframes kineticScroll {
          0% { transform: translateX(0); }
          100% { transform: translateX(-33.3333%); }
        }
        .animate-kinetic-scroll {
          display: flex;
          width: fit-content;
          animation: kineticScroll 60s linear infinite;
        }
        .animate-kinetic-scroll:hover {
          animation-play-state: paused;
        }
      `,
        }}
      />
    </div>
  );
};

export default KineticTape;
