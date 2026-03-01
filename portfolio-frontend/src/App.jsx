import React, { useState } from 'react';
import Auth from './pages/Auth';
import Dashboard from './pages/Dashboard';
import HoldingModal from './components/HoldingModal';
import { PlusCircle, LogOut } from 'lucide-react';

/**
 * Main Application Component for Kinetic Capital
 * Manages Authentication state and the global layout.
 */
export default function App() {
  // Initialize state from localStorage to persist session on page refresh
  const [token, setToken] = useState(localStorage.getItem('token'));
  const [userId, setUserId] = useState(localStorage.getItem('userId')); 
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  /**
   * Called by Auth.jsx upon successful login/registration.
   * Updates state and local storage with fresh credentials.
   */
  const handleLoginSuccess = (newToken, newUserId) => {
    localStorage.setItem('token', newToken);
    localStorage.setItem('userId', newUserId);
    setToken(newToken);
    setUserId(newUserId);
  };

  /**
   * Clears session data and returns user to the Login screen.
   */
  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    setToken(null);
    setUserId(null);
  };

  // 1. Conditional Rendering: If not logged in, show Auth screen
  if (!token) {
    return <Auth onLoginSuccess={handleLoginSuccess} />;
  }

  // 2. Main Dashboard View
  return (
    <div className="min-h-screen bg-slate-50 font-sans">
      {/* --- TOP NAVIGATION BAR --- */}
      <nav className="bg-white border-b px-8 py-4 flex justify-between items-center sticky top-0 z-40 shadow-sm">
        <div className="flex items-center gap-2">
          {/* Kinetic Capital Logo */}
          <div className="w-9 h-9 bg-indigo-600 rounded-xl flex items-center justify-center text-white font-black shadow-lg shadow-indigo-200">
            K
          </div>
          <h1 className="text-xl font-bold text-slate-800 tracking-tight">
            Kinetic Capital
          </h1>
        </div>
        
        <div className="flex items-center gap-4">
          <button 
            onClick={() => setIsModalOpen(true)}
            className="flex items-center gap-2 bg-indigo-600 text-white px-5 py-2.5 rounded-xl font-bold hover:bg-indigo-700 transition-all active:scale-95 shadow-md shadow-indigo-100"
          >
            <PlusCircle size={18} /> 
            <span>New Holding</span>
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

      {/* --- MAIN DASHBOARD CONTENT --- */}
      <main className="animate-in fade-in duration-700">
        {/* The 'key' forces the Dashboard to reload whenever refreshKey changes */}
        <Dashboard userId={userId} key={refreshKey} />
      </main>

      {/* --- ADD HOLDING MODAL --- */}
      <HoldingModal 
        userId={userId} 
        isOpen={isModalOpen} 
        onClose={() => setIsModalOpen(false)} 
        onRefresh={() => setRefreshKey(prev => prev + 1)} 
      />
    </div>
  );
}