import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { Search, Loader2, ArrowUpRight } from "lucide-react";
import api from "../api/axios";

export default function GlobalSearch() {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const searchRef = useRef(null);

  useEffect(() => {
    const handler = (e) => {
      if (!searchRef.current?.contains(e.target)) setIsOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  useEffect(() => {
    const delay = setTimeout(async () => {
      if (query.length > 1) {
        setLoading(true);
        try {
          const res = await api.get(`/stocks/search?query=${query}`);
          setResults(res.data);
          setIsOpen(true);
        } catch (err) {
          console.error("Search failed", err);
        }
        setLoading(false);
      } else {
        setIsOpen(false);
      }
    }, 300);
    return () => clearTimeout(delay);
  }, [query]);

  return (
    <div className="relative w-full max-w-md hidden md:block" ref={searchRef}>
      <div className="relative group">
        <Search
          className="absolute left-4 top-3 text-slate-400 group-focus-within:text-indigo-500 transition-colors"
          size={18}
        />
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search symbols (e.g. RELIANCE)..."
          className="w-full pl-11 pr-4 py-2.5 bg-slate-100 rounded-2xl border-none outline-none focus:ring-2 focus:ring-indigo-500/20 text-sm font-bold transition-all"
        />
        {loading && (
          <Loader2
            className="absolute right-4 top-3 animate-spin text-indigo-500"
            size={18}
          />
        )}
      </div>

      {isOpen && results.length > 0 && (
        <div className="absolute top-full left-0 right-0 mt-2 bg-white rounded-2xl shadow-2xl border border-slate-100 overflow-hidden z-[100] animate-in fade-in slide-in-from-top-2">
          {results.map((stock) => (
            <button
              key={stock.symbol}
              onClick={() => {
                setQuery("");
                setIsOpen(false);
                navigate(`/stock/${stock.symbol}`);
              }}
              className="w-full flex justify-between items-center px-5 py-4 hover:bg-indigo-50 transition-colors border-b border-slate-50 last:border-none group"
            >
              <div className="text-left">
                <span className="block font-black text-slate-800 group-hover:text-indigo-600 transition-colors">
                  {stock.symbol}
                </span>
                <span className="block text-[10px] text-slate-400 font-bold uppercase truncate max-w-[200px]">
                  {stock.name}
                </span>
              </div>
              <ArrowUpRight
                size={16}
                className="text-slate-300 group-hover:text-indigo-500 transition-all"
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
