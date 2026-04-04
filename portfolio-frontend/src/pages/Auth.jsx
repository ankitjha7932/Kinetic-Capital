import React, { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import api from "../api/axios";
import {
  Mail,
  Lock,
  ArrowRight,
  UserPlus,
  LogIn,
  Eye,
  EyeOff,
} from "lucide-react";
import { GoogleLogin } from "@react-oauth/google";

export default function Auth({ onLoginSuccess }) {
  const navigate = useNavigate();
  const [isLogin, setIsLogin] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const [formData, setFormData] = useState({
    email: "",
    password: "",
    riskProfile: "Moderate",
    investmentHorizon: 5,
  });

  // --- GOOGLE SIGN IN ---
  const handleGoogleSuccess = async (credentialResponse) => {
    setIsLoading(true);
    try {
      const res = await api.post("/auth/google-login", {
        token: credentialResponse.credential,
      });
      onLoginSuccess(res.data.token, res.data.userId);
      navigate("/");
    } catch (err) {
      alert("Google Sign-In failed. Please try again.");
    } finally {
      setIsLoading(false);
    }
  };

  // --- EMAIL/PASSWORD HANDLER ---
  const handleEmailSubmit = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      if (isLogin) {
        const res = await api.post("/auth/login", {
          email: formData.email,
          password: formData.password,
        });
        onLoginSuccess(res.data.token, res.data.userId);
        navigate("/");
      } else {
        const res = await api.post("/auth/verify-otp-register", {
          ...formData,
          otp: "bypass", // Adjust based on your backend rules for bypassing OTP
          preferredSectors: [],
        });
        onLoginSuccess(res.data.token, res.data.userId);
        navigate("/");
      }
    } catch (err) {
      alert(
        err.response?.data?.message ||
          err.response?.data ||
          "Authentication failed",
      );
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans relative overflow-hidden">
      {/* Soft Background Accents */}
      <div className="absolute top-[-10%] left-[-10%] w-[30rem] h-[30rem] bg-indigo-200 rounded-full mix-blend-multiply filter blur-[100px] opacity-40"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-[30rem] h-[30rem] bg-emerald-200 rounded-full mix-blend-multiply filter blur-[100px] opacity-40"></div>

      <div className="w-full max-w-md bg-white/90 backdrop-blur-xl p-10 rounded-[2.5rem] shadow-2xl shadow-indigo-100 border border-white/50 z-10 animate-in fade-in zoom-in duration-500">
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-3xl mx-auto mb-6 shadow-xl shadow-indigo-200">
            K
          </div>
          <h2 className="text-3xl font-black text-slate-800 tracking-tight">
            {isLogin ? "Welcome Back" : "Create Account"}
          </h2>
          <p className="text-slate-500 mt-2 text-sm font-medium">
            Access your Kinetic Capital portfolio
          </p>
        </div>

        <div className="mb-6 flex justify-center">
          <GoogleLogin
            onSuccess={handleGoogleSuccess}
            onError={() => console.log("Login Failed")}
            theme="outline"
            size="large"
            width="100%"
            text={isLogin ? "signin_with" : "signup_with"}
            shape="pill"
          />
        </div>

        <div className="relative mb-6">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-slate-200"></div>
          </div>
          <div className="relative flex justify-center text-xs uppercase">
            <span className="px-4 bg-white/90 text-slate-400 font-bold tracking-widest">
              Or continue with email
            </span>
          </div>
        </div>

        <form onSubmit={handleEmailSubmit} className="space-y-4">
          <div className="relative group">
            <Mail
              className="absolute left-4 top-4.5 text-slate-400 group-focus-within:text-indigo-500 transition-colors"
              size={20}
            />
            <input
              type="email"
              placeholder="Email Address"
              className="w-full p-4 pl-12 bg-slate-50/50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 focus:bg-white outline-none transition-all font-medium text-slate-700"
              value={formData.email}
              onChange={(e) =>
                setFormData({ ...formData, email: e.target.value })
              }
              required
            />
          </div>

          <div className="relative group">
            <Lock
              className="absolute left-4 top-4.5 text-slate-400 group-focus-within:text-indigo-500 transition-colors"
              size={20}
            />
            <input
              type={showPassword ? "text" : "password"}
              placeholder="Password"
              className="w-full p-4 pl-12 pr-12 bg-slate-50/50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 focus:bg-white outline-none transition-all font-medium text-slate-700"
              value={formData.password}
              onChange={(e) =>
                setFormData({ ...formData, password: e.target.value })
              }
              required
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-4 top-4.5 text-slate-400 hover:text-indigo-600 transition-colors"
            >
              {showPassword ? <EyeOff size={20} /> : <Eye size={20} />}
            </button>
          </div>

          {!isLogin && (
            <div className="grid grid-cols-2 gap-4 pt-2 animate-in slide-in-from-top-4 duration-500">
              <div className="space-y-1.5">
                <label className="text-[10px] font-black text-slate-400 uppercase tracking-wider ml-1">
                  Risk Profile
                </label>
                <select
                  className="w-full p-3.5 bg-slate-50/50 border border-slate-200 rounded-2xl outline-none font-bold text-slate-700 focus:ring-2 focus:ring-indigo-500"
                  value={formData.riskProfile}
                  onChange={(e) =>
                    setFormData({ ...formData, riskProfile: e.target.value })
                  }
                >
                  <option value="Low">Low</option>
                  <option value="Moderate">Moderate</option>
                  <option value="High">High</option>
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[10px] font-black text-slate-400 uppercase tracking-wider ml-1">
                  Horizon (Yrs)
                </label>
                <input
                  type="number"
                  className="w-full p-3.5 bg-slate-50/50 border border-slate-200 rounded-2xl outline-none font-bold text-slate-700 focus:ring-2 focus:ring-indigo-500"
                  value={formData.investmentHorizon}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      investmentHorizon: e.target.value,
                    })
                  }
                />
              </div>
            </div>
          )}

          {isLogin && (
            <div className="flex justify-end px-2 pt-1">
              <Link
                to="/forgot-password"
                className="text-xs font-bold text-indigo-600 hover:text-indigo-800 transition-colors"
              >
                Forgot Password?
              </Link>
            </div>
          )}

          <button
            disabled={isLoading}
            className="w-full bg-indigo-600 text-white p-4 rounded-2xl font-black hover:bg-indigo-700 transition-all flex items-center justify-center gap-2 disabled:opacity-50 shadow-lg shadow-indigo-200 mt-4 active:scale-[0.98]"
          >
            {isLoading
              ? "Processing..."
              : isLogin
                ? "Sign In"
                : "Create Account"}
            {!isLoading && <ArrowRight size={18} />}
          </button>
        </form>

        <div className="mt-8 text-center pt-6">
          <button
            onClick={() => {
              setIsLogin(!isLogin);
              setShowPassword(false);
            }}
            className="text-slate-500 text-sm font-medium flex items-center justify-center gap-2 mx-auto hover:text-slate-800 transition-colors"
          >
            {isLogin ? (
              <>
                New to Kinetic?{" "}
                <span className="text-indigo-600 font-bold flex items-center gap-1">
                  <UserPlus size={16} /> Create an account
                </span>
              </>
            ) : (
              <>
                Already invested?{" "}
                <span className="text-indigo-600 font-bold flex items-center gap-1">
                  <LogIn size={16} /> Sign in
                </span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
