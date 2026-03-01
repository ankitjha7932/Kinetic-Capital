import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { X, Search, BarChart2, TrendingUp, Info, ArrowLeft, Loader2 } from 'lucide-react';

export default function HoldingModal({ userId, isOpen, onClose, onRefresh }) {
    const [query, setQuery] = useState("");
    const [searchResults, setSearchResults] = useState([]);
    const [selectedStock, setSelectedStock] = useState(null);
    const [analysis, setAnalysis] = useState(null);
    const [isFetchingPrice, setIsFetchingPrice] = useState(false);

    const [formData, setFormData] = useState({
        quantity: '',
        avgBuyPrice: '',
        purchaseDate: new Date().toISOString().split('T')[0]
    });

    // 1. Search Logic (Debounced)
    useEffect(() => {
        const delay = setTimeout(async () => {
            if (query.length > 1) {
                try {
                    const res = await api.get(`/stocks/search?query=${query}`);
                    setSearchResults(res.data);
                } catch (err) { console.error("Search failed", err); }
            } else { setSearchResults([]); }
        }, 300);
        return () => clearTimeout(delay);
    }, [query]);

    // 2. Selection Logic with Real-Time Price Fetching
    const handleSelectStock = async (stock) => {
        setSelectedStock(stock);
        setIsFetchingPrice(true);
        try {
            // Fetch live price to ensure comparison is based on latest data
            const priceRes = await api.get(`/portfolio/price/${stock.symbol}`);
            
            // Auto-fill the price field for the user
            setFormData(prev => ({
                ...prev,
                avgBuyPrice: priceRes.data.price
            }));

            // Fetch AI analysis
            const analysisRes = await api.get(`/stocks/analyze/${stock.symbol}`);
            setAnalysis(analysisRes.data);
        } catch (err) { 
            console.error("Data fetch failed", err); 
        } finally {
            setIsFetchingPrice(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            const payload = {
                userId: userId,
                symbol: selectedStock.symbol,
                quantity: parseFloat(formData.quantity),
                avgBuyPrice: parseFloat(formData.avgBuyPrice),
                purchaseDate: formData.purchaseDate,
                tags: ""
            };

            await api.post('/holdings', payload);
            onRefresh(); 
            onClose();
            
            // Reset state
            setSelectedStock(null);
            setFormData({ quantity: '', avgBuyPrice: '', purchaseDate: new Date().toISOString().split('T')[0] });
        } catch (err) {
            alert("Failed to add stock: " + (err.response?.data || err.message));
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4 z-50">
            <div className="bg-white w-full max-w-lg rounded-[2.5rem] shadow-2xl overflow-hidden flex flex-col max-h-[85vh]">
                
                {/* Header */}
                <div className="p-6 border-b flex justify-between items-center">
                    <div className="flex items-center gap-3">
                        {selectedStock && (
                            <button onClick={() => setSelectedStock(null)} className="p-2 hover:bg-slate-100 rounded-full transition">
                                <ArrowLeft size={20} className="text-slate-600" />
                            </button>
                        )}
                        <h2 className="text-xl font-black text-slate-800 tracking-tight">
                            {selectedStock ? 'Review Position' : 'Search Markets'}
                        </h2>
                    </div>
                    <button onClick={onClose} className="p-2 hover:bg-slate-100 rounded-full transition"><X size={20} /></button>
                </div>

                <div className="p-8 overflow-y-auto flex-1">
                    {!selectedStock ? (
                        /* --- STAGE 1: SEARCH --- */
                        <div className="space-y-6">
                            <div className="relative">
                                <Search className="absolute left-4 top-4 text-slate-400" size={20} />
                                <input 
                                    className="w-full pl-12 pr-4 py-4 bg-slate-50 rounded-2xl border-none outline-none focus:ring-2 focus:ring-indigo-500 font-medium"
                                    placeholder="Search 2,000+ Indian Stocks..."
                                    autoFocus
                                    onChange={e => setQuery(e.target.value)}
                                />
                            </div>
                            <div className="space-y-2">
                                {searchResults.map(stock => (
                                    <button 
                                        key={stock.symbol}
                                        onClick={() => handleSelectStock(stock)}
                                        className="w-full flex justify-between items-center p-4 hover:bg-indigo-50 rounded-2xl transition-all group border border-transparent hover:border-indigo-100"
                                    >
                                        <div className="text-left">
                                            <p className="font-bold text-slate-800 group-hover:text-indigo-600">{stock.symbol}</p>
                                            <p className="text-xs text-slate-400 font-medium">{stock.name}</p>
                                        </div>
                                        <BarChart2 size={18} className="text-slate-200 group-hover:text-indigo-400" />
                                    </button>
                                ))}
                            </div>
                        </div>
                    ) : (
                        /* --- STAGE 2: ADD & ANALYZE --- */
                        <form onSubmit={handleSubmit} className="space-y-6 animate-in slide-in-from-bottom-4 duration-300">
                            <div className="flex justify-between items-start">
                                <div>
                                    <h3 className="text-3xl font-black text-slate-800 tracking-tighter">{selectedStock.symbol}</h3>
                                    <p className="text-slate-500 font-medium">{selectedStock.name}</p>
                                </div>
                                <div className="bg-emerald-50 px-4 py-2 rounded-xl text-emerald-600 border border-emerald-100 font-black text-sm uppercase">
                                    {analysis?.sentiment || 'Bullish'}
                                </div>
                            </div>

                            {/* Live Price Loading State */}
                            {isFetchingPrice && (
                                <div className="flex items-center gap-2 text-indigo-500 font-bold text-sm">
                                    <Loader2 className="animate-spin" size={16} />
                                    Syncing market price...
                                </div>
                            )}

                            <div className="bg-indigo-50 p-6 rounded-[2rem] border border-indigo-100 flex gap-4">
                                <Info size={20} className="text-indigo-600 shrink-0 mt-1" />
                                <p className="text-sm text-indigo-900 leading-relaxed font-semibold italic">
                                    "{analysis?.summary || 'Analyzing current market conditions...'}"
                                </p>
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-1">
                                    <label className="text-[10px] font-black text-slate-400 uppercase ml-1">Quantity</label>
                                    <input 
                                        type="number" required
                                        value={formData.quantity}
                                        onChange={e => setFormData({...formData, quantity: e.target.value})}
                                        className="w-full p-4 bg-slate-50 rounded-2xl outline-none focus:ring-2 focus:ring-indigo-500 font-bold"
                                        placeholder="0"
                                    />
                                </div>
                                <div className="space-y-1">
                                    <label className="text-[10px] font-black text-slate-400 uppercase ml-1">Avg Price</label>
                                    <input 
                                        type="number" step="0.01" required
                                        value={formData.avgBuyPrice}
                                        onChange={e => setFormData({...formData, avgBuyPrice: e.target.value})}
                                        className="w-full p-4 bg-slate-50 rounded-2xl outline-none focus:ring-2 focus:ring-indigo-500 font-bold"
                                        placeholder="₹0.00"
                                    />
                                </div>
                            </div>

                            {/* Action Buttons */}
                            <div className="flex gap-3 pt-2">
                                <button 
                                    type="button" 
                                    onClick={onClose}
                                    className="flex-1 bg-slate-100 text-slate-600 py-5 rounded-3xl font-black text-lg hover:bg-slate-200 transition-all"
                                >
                                    Cancel
                                </button>
                                <button 
                                    type="submit" 
                                    className="flex-[2] bg-indigo-600 text-white py-5 rounded-3xl font-black text-lg shadow-xl shadow-indigo-100 hover:bg-indigo-700 active:scale-95 transition-all"
                                >
                                    Add to Portfolio
                                </button>
                            </div>
                        </form>
                    )}
                </div>
            </div>
        </div>
    );
}