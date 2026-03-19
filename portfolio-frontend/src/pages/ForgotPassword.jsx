import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axios';
import { Mail, ArrowLeft, Send, CheckCircle2 } from 'lucide-react';

export default function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [status, setStatus] = useState({ msg: '', loading: false, success: false });

  const handleSubmit = async (e) => {
    e.preventDefault();
    setStatus({ msg: '', loading: true, success: false });
    try {
      const res = await api.post('/auth/forgot-password', { email });
      setStatus({ msg: res.data.message, loading: false, success: true });
    } catch (err) {
      setStatus({ 
        msg: err.response?.data || "Email not registered with us.", 
        loading: false, 
        success: false 
      });
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans">
      <div className="w-full max-w-md bg-white p-8 rounded-[2rem] shadow-2xl shadow-slate-200 animate-in fade-in zoom-in duration-500">
        
        {/* Back Link */}
        <Link 
          to="/auth" 
          className="inline-flex items-center gap-2 text-slate-400 hover:text-indigo-600 mb-8 transition-colors text-xs font-black uppercase tracking-widest"
        >
          <ArrowLeft size={14} /> Back to Login
        </Link>

        {/* Header */}
        <div className="text-center mb-10">
          <div className="w-16 h-16 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-3xl mx-auto mb-6 shadow-xl shadow-indigo-200">
            K
          </div>
          <h2 className="text-3xl font-black text-slate-800 tracking-tight">Recover Account</h2>
          <p className="text-slate-500 mt-2 text-sm leading-relaxed">
            Lost your key? Enter your email and we'll send a <br/> secure link to reset your password.
          </p>
        </div>

        {/* Success State */}
        {status.success ? (
          <div className="text-center py-4 animate-in slide-in-from-bottom-4">
            <div className="w-12 h-12 bg-emerald-50 text-emerald-500 rounded-full flex items-center justify-center mx-auto mb-4">
              <CheckCircle2 size={28} />
            </div>
            <p className="text-emerald-600 font-bold mb-2">Email Sent Successfully!</p>
            <p className="text-slate-500 text-xs px-4">
              Please check your inbox (and spam folder) for the reset link.
            </p>
          </div>
        ) : (
          /* Form State */
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="relative">
              <Mail className="absolute left-4 top-4.5 text-slate-400" size={20} />
              <input 
                type="email" 
                placeholder="Registered Email" 
                className="w-full p-4 pl-12 bg-slate-50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 focus:bg-white outline-none transition-all font-medium text-slate-700 placeholder:text-slate-400"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>

            <button 
              disabled={status.loading}
              className="w-full bg-indigo-600 text-white p-4 rounded-2xl font-black hover:bg-indigo-700 transition-all flex items-center justify-center gap-3 disabled:opacity-50 shadow-lg shadow-indigo-100 active:scale-[0.98]"
            >
              {status.loading ? (
                'Requesting...'
              ) : (
                <>
                  Send Reset Link
                  <Send size={18} />
                </>
              )}
            </button>
          </form>
        )}

        {/* Error Message */}
        {status.msg && !status.success && (
          <div className="mt-6 p-4 bg-red-50 rounded-xl border border-red-100 animate-shake">
            <p className="text-center text-xs font-bold text-red-500 uppercase tracking-tight">
              {status.msg}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}