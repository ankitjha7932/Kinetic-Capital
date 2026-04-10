import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { X, Search, BarChart2, ArrowLeft, Loader2, Hash, IndianRupee, Rocket, TrendingUp } from 'lucide-react';

export default function HoldingModal({ userId, isOpen, onClose, onRefresh }) {
    const [query, setQuery] = useState("");
    const [searchResults, setSearchResults] = useState([]);
    const [selectedStock, setSelectedStock] = useState(null);
    const [analysis, setAnalysis] = useState(null);
    const [livePrice, setLivePrice] = useState(0); 
    const [isFetchingData, setIsFetchingData] = useState(false);
    const [isSearching, setIsSearching] = useState(false);

    const [formData, setFormData] = useState({
        quantity: '',
        avgBuyPrice: '', 
        purchaseDate: new Date().toISOString().split('T')[0]
    });

    const handleClose = () => {
        onClose();
        setQuery("");
        setSearchResults([]);
        setSelectedStock(null);
        setAnalysis(null);
        setLivePrice(0);
        setFormData({ quantity: '', avgBuyPrice: '', purchaseDate: new Date().toISOString().split('T')[0] });
    };

    useEffect(() => {
        if (!isOpen) {
            setQuery("");
            setSearchResults([]);
            setSelectedStock(null);
        }
    }, [isOpen]);

    useEffect(() => {
        const delay = setTimeout(async () => {
            if (query.length > 1) {
                setIsSearching(true);
                try {
                    const res = await api.get(`/stocks/search?query=${query}`);
                    const data = res.data?.success ? res.data.data : (Array.isArray(res.data) ? res.data : []);
                    setSearchResults(data || []);
                } catch (err) { 
                    setSearchResults([]);
                } finally {
                    setIsSearching(false);
                }
            } else { setSearchResults([]); }
        }, 300);
        return () => clearTimeout(delay);
    }, [query]);

    const handleSelectStock = async (stock) => {
        setSelectedStock(stock);
        setIsFetchingData(true);
        try {
            const priceRes = await api.get(`/portfolio/price/${stock.symbol}`);
            const marketPrice = priceRes.data.price || 0;
            setLivePrice(marketPrice);
            setFormData(prev => ({ 
                ...prev, 
                avgBuyPrice: parseFloat(marketPrice).toFixed(2)
            }));
            const analysisRes = await api.get(`/stocks/analyze/${stock.symbol}`);
            setAnalysis(analysisRes.data.success === false ? null : (analysisRes.data.data || analysisRes.data));
        } catch (err) { 
            console.error("Data fetch failed", err); 
        } finally {
            setIsFetchingData(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            const payload = {
                userId,
                symbol: selectedStock.symbol,
                quantity: parseFloat(formData.quantity),
                avgBuyPrice: parseFloat(formData.avgBuyPrice),
                purchaseDate: formData.purchaseDate,
                tags: "Equity"
            };
            await api.post('/holdings', payload);
            onRefresh();
            handleClose();
        } catch (err) {
            alert("Error: " + (err.response?.data || err.message));
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-md flex items-center justify-center p-4 z-50 transition-all">
            <div className="bg-white w-full max-w-lg rounded-[2rem] shadow-2xl flex flex-col max-h-[90vh] border border-slate-100">
                
                {/* Simplified Header */}
                <div className="px-8 py-6 flex justify-between items-center bg-white rounded-t-[2rem] border-b border-slate-50 shrink-0">
                    <div className="flex items-center gap-4">
                        {selectedStock && (
                            <button onClick={() => setSelectedStock(null)} className="p-2 bg-slate-50 hover:bg-slate-100 rounded-xl transition-all">
                                <ArrowLeft size={18} className="text-slate-600" />
                            </button>
                        )}
                        <h2 className="text-xl font-bold text-slate-800 tracking-tight">
                            {selectedStock ? 'Review Details' : 'Add to Portfolio'}
                        </h2>
                    </div>
                    <button onClick={handleClose} className="p-2 text-slate-400 hover:text-rose-500 transition-colors">
                        <X size={22} />
                    </button>
                </div>

                <div className="px-8 py-6 overflow-y-auto flex-1 custom-scrollbar">
                    {!selectedStock ? (
                        /* --- CLEAN SEARCH VIEW --- */
                        <div className="space-y-6 animate-in slide-in-from-top-2 duration-300">
                            <div className="relative group">
                                <Search className="absolute left-5 top-5 text-slate-400 group-focus-within:text-indigo-600 transition-colors" size={18} />
                                <input
                                    className="w-full pl-12 pr-12 py-4 bg-slate-50 rounded-2xl border-2 border-transparent focus:border-indigo-500/20 focus:bg-white outline-none font-semibold text-slate-800 transition-all placeholder:text-slate-400"
                                    placeholder="Search by name or ticker..."
                                    autoFocus
                                    value={query}
                                    onChange={e => setQuery(e.target.value)}
                                />
                                {isSearching && (
                                    <div className="absolute inset-y-0 right-5 flex items-center">
                                        <Loader2 className="animate-spin text-indigo-500" size={18} />
                                    </div>
                                )}
                            </div>
                            
                            <div className="space-y-2">
                                {searchResults.map((stock) => (
                                    <button
                                        key={stock.symbol}
                                        onClick={() => handleSelectStock(stock)}
                                        className="w-full flex justify-between items-center p-4 bg-white hover:bg-slate-50 rounded-2xl transition-all group border border-slate-100 hover:border-indigo-200"
                                    >
                                        <div className="flex items-center gap-4">
                                            <div className="w-10 h-10 bg-indigo-50 rounded-xl flex items-center justify-center font-bold text-indigo-600 uppercase">
                                                {stock.symbol.charAt(0)}
                                            </div>
                                            <div className="text-left">
                                                <span className="block font-bold text-slate-900 group-hover:text-indigo-600 transition-colors">{stock.symbol}</span>
                                                <span className="block text-[11px] text-slate-400 font-medium truncate max-w-[180px]">{stock.name}</span>
                                            </div>
                                        </div>
                                        <BarChart2 size={18} className="text-slate-200 group-hover:text-indigo-400" />
                                    </button>
                                ))}
                            </div>
                        </div>
                    ) : (
                        /* --- CLEAN INPUT VIEW --- */
                        <form onSubmit={handleSubmit} className="space-y-6 animate-in zoom-in-95 duration-300">
                            {/* Stock Identity Card */}
                            <div className="bg-slate-900 p-8 rounded-3xl relative overflow-hidden shadow-lg">
                                <div className="absolute top-[-10%] right-[-10%] w-48 h-48 bg-indigo-500/10 rounded-full blur-3xl" />
                                <div className="relative z-10">
                                    <div className="flex justify-between items-start mb-6">
                                        <div>
                                            <h3 className="text-3xl font-bold text-white tracking-tight">{selectedStock.symbol}</h3>
                                            <p className="text-slate-400 text-xs font-medium mt-1">{selectedStock.name}</p>
                                        </div>
                                        <div className="px-3 py-1 bg-white/10 border border-white/10 rounded-lg backdrop-blur-md">
                                            <span className="text-[10px] font-bold text-indigo-300 uppercase tracking-wider">{analysis?.sentiment || 'Neutral'}</span>
                                        </div>
                                    </div>
                                    
                                    <div className="space-y-0.5">
                                        <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest">Live Market Price</p>
                                        {isFetchingData ? (
                                            <div className="flex items-center gap-2 text-indigo-400 italic font-medium">
                                                <Loader2 className="animate-spin" size={14} /> Fetching...
                                            </div>
                                        ) : (
                                            <div className="flex items-baseline gap-1.5">
                                                <span className="text-indigo-400 font-bold text-lg">₹</span>
                                                <span className="text-white text-4xl font-bold tracking-tight">
                                                    {Number(livePrice).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                                                </span>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            </div>

                            {/* AI Summary Card */}
                            <div className="bg-emerald-50/60 p-4 rounded-2xl border border-emerald-100 flex gap-3 items-start">
                                <Rocket size={18} className="text-emerald-600 shrink-0 mt-0.5" />
                                <p className="text-[13px] text-emerald-900 font-medium leading-relaxed italic">
                                    "{analysis?.summary || 'Analyzing current market position...'}"
                                </p>
                            </div>

                            {/* Simplified Inputs */}
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <label className="text-[11px] font-bold text-slate-500 uppercase tracking-wider ml-1 flex items-center gap-1.5">
                                        <Hash size={14} className="text-slate-300" /> Quantity
                                    </label>
                                    <input 
                                        type="number" required
                                        value={formData.quantity}
                                        onChange={e => setFormData({...formData, quantity: e.target.value})}
                                        className="w-full p-4 bg-slate-50 border-2 border-slate-100 rounded-2xl outline-none focus:border-indigo-500/30 focus:bg-white text-slate-900 font-bold text-xl transition-all"
                                        placeholder="0"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-[11px] font-bold text-slate-500 uppercase tracking-wider ml-1 flex items-center gap-1.5">
                                        <IndianRupee size={14} className="text-slate-300" /> Avg. Price
                                    </label>
                                    <input 
                                        type="number" step="0.01" required
                                        value={formData.avgBuyPrice}
                                        onChange={e => setFormData({...formData, avgBuyPrice: e.target.value})}
                                        className="w-full p-4 bg-slate-50 border-2 border-slate-100 rounded-2xl outline-none focus:border-indigo-500/30 focus:bg-white text-slate-900 font-bold text-xl transition-all"
                                        placeholder="0.00"
                                    />
                                </div>
                            </div>

                            {/* Action Buttons */}
                            <div className="flex gap-4 pt-4">
                                <button type="button" onClick={handleClose} className="flex-1 bg-slate-50 text-slate-500 py-4 rounded-2xl font-bold text-sm hover:bg-slate-100 transition-all">
                                    Discard
                                </button>
                                <button type="submit" className="flex-[2] bg-indigo-600 text-white py-4 rounded-2xl font-bold text-sm shadow-lg shadow-indigo-100 hover:bg-indigo-700 active:scale-[0.98] transition-all">
                                    Add Asset
                                </button>
                            </div>
                        </form>
                    )}
                </div>
            </div>
        </div>
    );
}