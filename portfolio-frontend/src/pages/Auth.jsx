import React, { useState, useEffect, useRef } from 'react';
import api from '../api/axios';
import { Mail, Lock, Shield, Target, ArrowRight, UserPlus, LogIn } from 'lucide-react';

export default function Auth({ onLoginSuccess }) {
  const [isLogin, setIsLogin] = useState(true);
  const [step, setStep] = useState('input'); // 'input' or 'otp'
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    riskProfile: 'Moderate',
    investmentHorizon: 5
  });
  const [otp, setOtp] = useState('');
  const [resendTimer, setResendTimer] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const otpRefs = useRef([]);

  // Timer logic for resending OTP
  useEffect(() => {
    let interval;
    if (resendTimer > 0) {
      interval = setInterval(() => setResendTimer((prev) => prev - 1), 1000);
    }
    return () => clearInterval(interval);
  }, [resendTimer]);

  const handleInitialSubmit = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      if (isLogin) {
        // FLOW 1: Direct Login with Email/Password
        const res = await api.post('/auth/login', { 
          email: formData.email, 
          password: formData.password 
        });
        localStorage.setItem('token', res.data.token);
        localStorage.setItem('userId', res.data.userId);
        onLoginSuccess(res.data.token, res.data.userId);
      } else {
        // FLOW 2: Start Registration by sending OTP
        await api.post('/auth/send-otp', { email: formData.email });
        setStep('otp');
        setResendTimer(60);
      }
    } catch (err) {
      alert(err.response?.data || 'Authentication failed');
    } finally {
      setIsLoading(false);
    }
  };

  const handleFinalSignup = async (e) => {
    if (e) e.preventDefault();
    if (otp.length !== 6) return;

    setIsLoading(true);
    try {
      // FLOW 2 (Cont.): Complete Registration with OTP and Profile data
      const res = await api.post('/auth/verify-otp-register', {
        ...formData,
        otp: otp,
        preferredSectors: [] // You can expand this to a multi-select later
      });

      localStorage.setItem('token', res.data.token);
      localStorage.setItem('userId', res.data.userId);
      onLoginSuccess(res.data.token, res.data.userId);
    } catch (err) {
      alert(err.response?.data || 'Registration failed');
      setOtp('');
      otpRefs.current[0]?.focus();
    } finally {
      setIsLoading(false);
    }
  };

  const handleOtpChange = (value, index) => {
    if (isNaN(value)) return;
    const otpArray = otp.split('');
    otpArray[index] = value;
    const newOtp = otpArray.join('');
    setOtp(newOtp);

    if (value && index < 5) otpRefs.current[index + 1]?.focus();
    if (newOtp.length === 6 && index === 5) {
      setTimeout(() => handleFinalSignup(), 100);
    }
  };

  const renderInputStep = () => (
    <div className="w-full max-w-md bg-white p-8 rounded-3xl shadow-xl animate-in fade-in zoom-in duration-300">
      <div className="text-center mb-8">
        <div className="w-14 h-14 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-2xl mx-auto mb-4 shadow-lg shadow-indigo-200">K</div>
        <h2 className="text-3xl font-black text-slate-800">{isLogin ? 'Welcome Back' : 'Create Account'}</h2>
        <p className="text-slate-500 mt-2">Enter your details to access your portfolio</p>
      </div>

      <form onSubmit={handleInitialSubmit} className="space-y-4">
        <div className="relative">
          <Mail className="absolute left-4 top-4 text-slate-400" size={20} />
          <input 
            type="email" 
            placeholder="Email Address" 
            className="w-full p-4 pl-12 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all"
            value={formData.email}
            onChange={(e) => setFormData({...formData, email: e.target.value})}
            required
          />
        </div>

        <div className="relative">
          <Lock className="absolute left-4 top-4 text-slate-400" size={20} />
          <input 
            type="password" 
            placeholder="Password" 
            className="w-full p-4 pl-12 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all"
            value={formData.password}
            onChange={(e) => setFormData({...formData, password: e.target.value})}
            required
          />
        </div>

        {!isLogin && (
          <div className="grid grid-cols-2 gap-4 animate-in slide-in-from-top-4 duration-500">
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-slate-400 uppercase ml-1">Risk Profile</label>
              <select 
                className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none"
                value={formData.riskProfile}
                onChange={(e) => setFormData({...formData, riskProfile: e.target.value})}
              >
                <option value="Low">Low</option>
                <option value="Moderate">Moderate</option>
                <option value="High">High</option>
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-slate-400 uppercase ml-1">Horizon (Yrs)</label>
              <input 
                type="number" 
                className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none"
                value={formData.investmentHorizon}
                onChange={(e) => setFormData({...formData, investmentHorizon: e.target.value})}
              />
            </div>
          </div>
        )}

        <button 
          disabled={isLoading}
          className="w-full bg-indigo-600 text-white p-4 rounded-xl font-bold hover:bg-indigo-700 transition-all flex items-center justify-center gap-2 disabled:opacity-50"
        >
          {isLoading ? 'Processing...' : isLogin ? 'Sign In' : 'Get Verification Code'}
          <ArrowRight size={18} />
        </button>
      </form>

      <div className="mt-8 text-center border-t pt-6">
        <button 
          onClick={() => { setIsLogin(!isLogin); setStep('input'); }}
          className="text-indigo-600 font-bold flex items-center justify-center gap-2 mx-auto hover:underline"
        >
          {isLogin ? <UserPlus size={18} /> : <LogIn size={18} />}
          {isLogin ? "New here? Create an account" : "Have an account? Sign in"}
        </button>
      </div>
    </div>
  );

  const renderOtpStep = () => (
    <div className="w-full max-w-md bg-white p-8 rounded-3xl shadow-xl animate-in fade-in zoom-in duration-300">
      <div className="text-center mb-8">
        <div className="w-14 h-14 bg-green-100 rounded-2xl flex items-center justify-center text-green-600 mx-auto mb-4">
          <Shield size={32} />
        </div>
        <h2 className="text-3xl font-black text-slate-800">Verify Email</h2>
        <p className="text-slate-500 mt-2">Enter the 6-digit code sent to <br/><span className="font-semibold text-slate-700">{formData.email}</span></p>
      </div>
      
      <div className="flex gap-2 justify-center mb-8">
        {Array(6).fill(0).map((_, i) => (
          <input
            key={i}
            ref={el => otpRefs.current[i] = el}
            type="text"
            maxLength={1}
            value={otp[i] || ''}
            onChange={e => handleOtpChange(e.target.value.slice(-1), i)}
            onKeyDown={(e) => e.key === 'Backspace' && !otp[i] && i > 0 && otpRefs.current[i-1].focus()}
            className="w-12 h-14 text-center text-xl font-bold bg-slate-50 border-2 border-slate-200 rounded-lg focus:border-indigo-500 outline-none"
          />
        ))}
      </div>

      <button 
        onClick={handleFinalSignup}
        disabled={isLoading || otp.length < 6}
        className="w-full bg-indigo-600 text-white p-4 rounded-xl font-bold hover:bg-indigo-700 mb-4 transition-all disabled:opacity-50"
      >
        {isLoading ? 'Verifying...' : 'Complete Registration'}
      </button>

      <div className="text-center">
        {resendTimer > 0 ? (
          <p className="text-sm text-slate-400">Resend code in {resendTimer}s</p>
        ) : (
          <button onClick={handleInitialSubmit} className="text-indigo-600 text-sm font-bold hover:underline">Resend Code</button>
        )}
        <button onClick={() => setStep('input')} className="block w-full mt-4 text-xs text-slate-400 hover:text-slate-600">Back to Details</button>
      </div>
    </div>
  );

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans">
      {step === 'input' ? renderInputStep() : renderOtpStep()}
    </div>
  );
}