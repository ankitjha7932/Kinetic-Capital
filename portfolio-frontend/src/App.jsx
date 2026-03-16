import React, { useState } from 'react';
import { BrowserRouter, Routes, Route, Link, Navigate } from 'react-router-dom'; 
import Auth from './pages/Auth';
import Dashboard from './pages/Dashboard';
import StockDetail from './components/StockDetailView';
import HoldingModal from './components/HoldingModal';
import Profile from './pages/Profile';
import { PlusCircle, LogOut, User } from 'lucide-react';

/**
 * Kinetic Capital - Main Application Controller
 */
export default function App() {
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [userId, setUserId] = useState(localStorage.getItem('userId')); 
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleLoginSuccess = (newToken, newUserId) => {
    localStorage.setItem('token', newToken);
    localStorage.setItem('userId', newUserId);
    setToken(newToken);
    setUserId(newUserId);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    setToken(null);
    setUserId(null);
  };

  return (
    <BrowserRouter>
      {/* CRITICAL FIX: Putting the ternary operator inside BrowserRouter 
          ensures Auth.jsx can use the useNavigate() hook without crashing.
      */}
      {!token ? (
        <Auth onLoginSuccess={handleLoginSuccess} />
      ) : (
        <div className="min-h-screen bg-slate-50 font-sans">
          {/* --- NAVIGATION BAR --- */}
          <nav className="bg-white border-b px-8 py-4 flex justify-between items-center sticky top-0 z-40 shadow-sm">
            <Link to="/" className="flex items-center gap-2 transition-opacity hover:opacity-80">
              <div className="w-9 h-9 bg-indigo-600 rounded-xl flex items-center justify-center text-white font-black shadow-lg shadow-indigo-200">
                K
              </div>
              <h1 className="text-xl font-bold text-slate-800 tracking-tight">Kinetic Capital</h1>
            </Link>
            
            <div className="flex items-center gap-3">
              {/* Profile Shortcut */}
              <Link 
                to="/profile" 
                className="p-2.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-xl transition-all" 
                title="Profile"
              >
                <User size={20} />
              </Link>

              <button 
                onClick={() => setIsModalOpen(true)}
                className="flex items-center gap-2 bg-indigo-600 text-white px-5 py-2.5 rounded-xl font-bold hover:bg-indigo-700 transition-all active:scale-95 shadow-md shadow-indigo-100"
              >
                <PlusCircle size={18} /> 
                <span className="hidden sm:inline">New Holding</span>
              </button>
              
              <button 
                onClick={handleLogout} 
                className="p-2.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all" 
                title="Logout"
              >
                <LogOut size={20} />
              </button>
            </div>
          </nav>

          {/* --- MAIN PAGE CONTENT --- */}
          <main className="animate-in fade-in duration-700">
            <Routes>
              <Route path="/" element={<Dashboard userId={userId} key={refreshKey} />} />
              <Route path="/stock/:symbol" element={<StockDetail userId={userId} />} />
              <Route path="/profile" element={<Profile userId={userId} />} />
              {/* Catch-all: If route doesn't exist, redirect to home */}
              <Route path="*" element={<Navigate to="/" />} />
            </Routes>
          </main>

          {/* --- GLOBAL MODAL --- */}
          <HoldingModal 
            userId={userId} 
            isOpen={isModalOpen} 
            onClose={() => setIsModalOpen(false)} 
            onRefresh={() => setRefreshKey(prev => prev + 1)} 
          />
        </div>
      )}
    </BrowserRouter>
  );
}