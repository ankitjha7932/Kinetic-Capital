import React, { useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import api from '../api/axios';
import { Lock, CheckCircle2, ArrowRight, ShieldCheck, Eye, EyeOff } from 'lucide-react';

export default function ResetPassword() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  
  const [passwords, setPasswords] = useState({ new: '', confirm: '' });
  const [showPassword, setShowPassword] = useState(false);
  const [status, setStatus] = useState({ msg: '', loading: false, success: false });

  const handleReset = async (e) => {
    e.preventDefault();
    if (passwords.new !== passwords.confirm) {
      return setStatus({ msg: "Passwords do not match", loading: false, success: false });
    }

    setStatus({ msg: '', loading: true, success: false });
    try {
      await api.post('/auth/reset-password', { 
        token, 
        newPassword: passwords.new 
      });
      setStatus({ msg: "Password changed successfully!", loading: false, success: true });
    } catch (err) {
      setStatus({ 
        msg: err.response?.data || "This link has expired. Please request a new one.", 
        loading: false, 
        success: false 
      });
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans">
      <div className="w-full max-w-md bg-white p-8 rounded-[2rem] shadow-2xl shadow-slate-200 animate-in fade-in zoom-in duration-500">
        
        <div className="text-center mb-10">
          <div className="w-16 h-16 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-3xl mx-auto mb-6 shadow-xl shadow-indigo-200">
            K
          </div>
          <h2 className="text-3xl font-black text-slate-800 tracking-tight">New Password</h2>
          <p className="text-slate-500 mt-2 text-sm">Secure your Kinetic Capital account.</p>
        </div>

        {status.success ? (
          <div className="text-center py-6 animate-in slide-in-from-bottom-4 duration-500">
            <div className="w-20 h-20 bg-emerald-50 text-emerald-500 rounded-full flex items-center justify-center mx-auto mb-6">
              <ShieldCheck size={40} />
            </div>
            <h3 className="text-xl font-bold text-slate-800 mb-2">All Set!</h3>
            <p className="text-slate-500 text-sm mb-8">Your password has been updated. You can now log in with your new credentials.</p>
            
            <Link 
              to="/auth" 
              className="w-full bg-slate-900 text-white p-4 rounded-2xl font-black hover:bg-black transition-all flex items-center justify-center gap-3 shadow-lg"
            >
              Go to Login
              <ArrowRight size={18} />
            </Link>
          </div>
        ) : (
          <form onSubmit={handleReset} className="space-y-5">
            <div className="relative">
              <Lock className="absolute left-4 top-4.5 text-slate-400" size={20} />
              <input 
                type={showPassword ? "text" : "password"}
                placeholder="New Password" 
                className="w-full p-4 pl-12 pr-12 bg-slate-50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all font-medium"
                onChange={(e) => setPasswords({...passwords, new: e.target.value})}
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

            <div className="relative">
              <Lock className="absolute left-4 top-4.5 text-slate-400" size={20} />
              <input 
                type={showPassword ? "text" : "password"}
                placeholder="Confirm Password" 
                className="w-full p-4 pl-12 pr-12 bg-slate-50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all font-medium"
                onChange={(e) => setPasswords({...passwords, confirm: e.target.value})}
                required
              />
            </div>

            <button 
              disabled={status.loading}
              className="w-full bg-indigo-600 text-white p-4 rounded-2xl font-black hover:bg-indigo-700 transition-all flex items-center justify-center gap-3 disabled:opacity-50 shadow-lg shadow-indigo-100"
            >
              {status.loading ? "Updating..." : "Update Password"}
              <CheckCircle2 size={18} />
            </button>
            
            {status.msg && (
              <p className="text-center text-xs font-bold text-red-500 uppercase mt-4">
                {status.msg}
              </p>
            )}
          </form>
        )}
      </div>
    </div>
  );
}