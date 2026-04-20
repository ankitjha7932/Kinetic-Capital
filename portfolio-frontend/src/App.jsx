import React, { useState } from "react";
import { BrowserRouter, Routes, Route, Link, Navigate } from "react-router-dom";
import { GoogleOAuthProvider } from "@react-oauth/google";
import Auth from "./pages/Auth";
import Dashboard from "./pages/Dashboard";
import StockDetail from "./components/StockDetailView";
import HoldingModal from "./components/HoldingModal";
import Profile from "./pages/Profile";
import ForgotPassword from "./pages/ForgotPassword";
import ResetPassword from "./pages/ResetPassword";
import StrategicTerminal from "./pages/StrategicTerminal";
import KineticTape from "./components/KineticTape";
import GlobalSearch from "./components/GlobalSearch";
import { PlusCircle, LogOut, User } from "lucide-react";

export default function App() {
  const [token, setToken] = useState(localStorage.getItem("token"));
  const [userId, setUserId] = useState(localStorage.getItem("userId"));
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  // Securely load the key from the .env file using Vite's syntax
  const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID;

  const handleLoginSuccess = (newToken, newUserId) => {
    localStorage.setItem("token", newToken);
    localStorage.setItem("userId", newUserId);
    setToken(newToken);
    setUserId(newUserId);
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("userId");
    setToken(null);
    setUserId(null);
  };

  return (
    <BrowserRouter>
      {!token ? (
        /* --- PUBLIC ROUTES --- */
        <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
          <Routes>
            <Route path="/forgot-password" element={<ForgotPassword />} />
            <Route path="/reset-password" element={<ResetPassword />} />
            <Route
              path="*"
              element={<Auth onLoginSuccess={handleLoginSuccess} />}
            />
          </Routes>
        </GoogleOAuthProvider>
      ) : (
        /* --- PROTECTED ROUTES --- */
        <div className="min-h-screen bg-slate-50 font-sans flex flex-col">
          <KineticTape />

          {/* UPDATED: Adjusted padding and gap for mobile screens so it doesn't squish */}
          <nav className="bg-white border-b px-3 md:px-8 py-3 md:py-4 flex justify-between items-center sticky top-0 z-40 shadow-sm gap-2 md:gap-8">
            <Link to="/" className="flex items-center gap-2 shrink-0">
              <div className="w-9 h-9 bg-indigo-600 rounded-xl flex items-center justify-center text-white font-black shadow-lg shadow-indigo-200">
                K
              </div>
              <h1 className="text-xl font-bold text-slate-800 tracking-tight hidden lg:block">
                Kinetic Capital
              </h1>
            </Link>

            {/* UPDATED: Removed "hidden md:block" so the search bar shows everywhere */}
            <div className="flex-1 max-w-md w-full px-2 md:px-0">
              <GlobalSearch />
            </div>

            {/* UPDATED: Reduced gap slightly on mobile so the icons fit nicely next to the search bar */}
            <div className="flex items-center gap-1 md:gap-3 shrink-0">
              <Link
                to="/profile"
                className="p-2 md:p-2.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-xl transition-all"
              >
                <User size={20} />
              </Link>

              <button
                onClick={() => setIsModalOpen(true)}
                className="flex items-center gap-2 bg-indigo-600 text-white px-3 md:px-5 py-2 md:py-2.5 rounded-xl font-bold hover:bg-indigo-700 transition-all shadow-md shadow-indigo-100"
              >
                <PlusCircle size={18} />
                <span className="hidden sm:inline">New Holding</span>
              </button>

              <button
                onClick={handleLogout}
                className="p-2 md:p-2.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all"
              >
                <LogOut size={20} />
              </button>
            </div>
          </nav>

          <main className="flex-1 animate-in fade-in duration-700">
            <Routes>
              <Route
                path="/"
                element={<Dashboard userId={userId} key={refreshKey} />}
              />
              <Route
                path="/stock/:symbol"
                element={<StockDetail userId={userId} />}
              />
              <Route path="/profile" element={<Profile userId={userId} />} />
              <Route path="/strategy/:symbol" element={<StrategicTerminal />} />
              <Route path="*" element={<Navigate to="/" />} />
            </Routes>
          </main>

          <HoldingModal
            userId={userId}
            isOpen={isModalOpen}
            onClose={() => setIsModalOpen(false)}
            onRefresh={() => setRefreshKey((prev) => prev + 1)}
          />
        </div>
      )}
    </BrowserRouter>
  );
}