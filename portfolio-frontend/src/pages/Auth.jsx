import React, { useState, useEffect, useRef } from 'react';
import { useNavigate, Link } from 'react-router-dom'; 
import api from '../api/axios';
import { Mail, Lock, Shield, ArrowRight, UserPlus, LogIn, Eye, EyeOff } from 'lucide-react';

export default function Auth({ onLoginSuccess }) {
  const navigate = useNavigate(); 
  const [isLogin, setIsLogin] = useState(true);
  const [step, setStep] = useState('input');
  const [showPassword, setShowPassword] = useState(false); 
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

  useEffect(() => {
    let interval;
    if (resendTimer > 0) {
      interval = setInterval(() => setResendTimer((prev) => prev - 1), 1000);
    }
    return () => clearInterval(interval);
  }, [resendTimer]);

  const handleInitialSubmit = async (e) => {
    if (e) e.preventDefault();
    setIsLoading(true);
    try {
      if (isLogin) {
        const res = await api.post('/auth/login', { 
          email: formData.email, 
          password: formData.password 
        });
        onLoginSuccess(res.data.token, res.data.userId);
        navigate('/'); 
      } else {
        await api.post('/auth/send-otp', { 
          email: formData.email,
          flow: 'register' 
        });
        setStep('otp');
        setResendTimer(60);
      }
    } catch (err) {
      alert(err.response?.data?.message || err.response?.data || 'Authentication failed');
    } finally {
      setIsLoading(false);
    }
  };

  const handleFinalSignup = async () => {
    if (otp.length !== 6) return;
    setIsLoading(true);
    try {
      const res = await api.post('/auth/verify-otp-register', {
        ...formData,
        otp: otp,
        preferredSectors: []
      });
      onLoginSuccess(res.data.token, res.data.userId);
      navigate('/');
    } catch (err) {
      alert(err.response?.data?.message || err.response?.data || 'Registration failed');
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
      handleFinalSignup();
    }
  };

  const renderInputStep = () => (
    <div className="w-full max-w-md bg-white p-8 rounded-3xl shadow-xl animate-in fade-in zoom-in duration-300">
      <div className="text-center mb-8">
        <div className="w-14 h-14 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-2xl mx-auto mb-4 shadow-lg shadow-indigo-200">K</div>
        <h2 className="text-3xl font-black text-slate-800 tracking-tight">{isLogin ? 'Welcome Back' : 'Create Account'}</h2>
        <p className="text-slate-500 mt-2 text-sm">Access your Kinetic Capital portfolio</p>
      </div>

      <form onSubmit={handleInitialSubmit} className="space-y-4">
        <div className="relative">
          <Mail className="absolute left-4 top-4 text-slate-400" size={20} />
          <input 
            type="email" 
            placeholder="Email Address" 
            className="w-full p-4 pl-12 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all font-medium"
            value={formData.email}
            onChange={(e) => setFormData({...formData, email: e.target.value})}
            required
          />
        </div>

        <div className="relative">
          <Lock className="absolute left-4 top-4 text-slate-400" size={20} />
          <input 
            type={showPassword ? "text" : "password"}
            placeholder="Password" 
            className="w-full p-4 pl-12 pr-12 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none transition-all font-medium"
            value={formData.password}
            onChange={(e) => setFormData({...formData, password: e.target.value})}
            required
          />
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-4 top-4 text-slate-400 hover:text-indigo-600 transition-colors"
          >
            {showPassword ? <EyeOff size={20} /> : <Eye size={20} />}
          </button>
        </div>

        {isLogin && (
          <div className="flex justify-end px-1">
            <Link to="/forgot-password" size={20} className="text-xs font-bold text-indigo-600 hover:text-indigo-800 transition-colors">
              Forgot Password?
            </Link>
          </div>
        )}

        {!isLogin && (
          <div className="grid grid-cols-2 gap-4 animate-in slide-in-from-top-4 duration-500">
            <div className="space-y-1">
              <label className="text-[10px] font-black text-slate-400 uppercase ml-1">Risk Profile</label>
              <select 
                className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none font-bold text-slate-700"
                value={formData.riskProfile}
                onChange={(e) => setFormData({...formData, riskProfile: e.target.value})}
              >
                <option value="Low">Low</option>
                <option value="Moderate">Moderate</option>
                <option value="High">High</option>
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-black text-slate-400 uppercase ml-1">Horizon (Yrs)</label>
              <input 
                type="number" 
                className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl outline-none font-bold text-slate-700"
                value={formData.investmentHorizon}
                onChange={(e) => setFormData({...formData, investmentHorizon: e.target.value})}
              />
            </div>
          </div>
        )}

        <button 
          disabled={isLoading}
          className="w-full bg-indigo-600 text-white p-4 rounded-xl font-bold hover:bg-indigo-700 transition-all flex items-center justify-center gap-2 disabled:opacity-50 shadow-lg shadow-indigo-100 mt-2"
        >
          {isLoading ? 'Processing...' : isLogin ? 'Sign In' : 'Get Verification Code'}
          <ArrowRight size={18} />
        </button>
      </form>

      <div className="mt-8 text-center border-t pt-6">
        <button 
          onClick={() => { setIsLogin(!isLogin); setStep('input'); setShowPassword(false); }}
          className="text-indigo-600 font-bold flex items-center justify-center gap-2 mx-auto hover:text-indigo-800 transition-colors"
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
        <div className="w-14 h-14 bg-emerald-50 rounded-2xl flex items-center justify-center text-emerald-600 mx-auto mb-4 border border-emerald-100">
          <Shield size={32} />
        </div>
        <h2 className="text-3xl font-black text-slate-800 tracking-tight">Verify Email</h2>
        <p className="text-slate-500 mt-2 text-sm leading-relaxed">
          Enter the code sent to <br/>
          <span className="font-bold text-slate-800">{formData.email}</span>
        </p>
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
            onKeyDown={(e) => {
              if (e.key === 'Backspace' && !otp[i] && i > 0) otpRefs.current[i-1].focus();
            }}
            className="w-12 h-14 text-center text-xl font-black bg-slate-50 border-2 border-slate-200 rounded-xl focus:border-indigo-500 focus:bg-white outline-none transition-all shadow-sm"
          />
        ))}
      </div>

      <button 
        onClick={handleFinalSignup}
        disabled={isLoading || otp.length < 6}
        className="w-full bg-indigo-600 text-white p-4 rounded-xl font-bold hover:bg-indigo-700 mb-4 transition-all disabled:opacity-50 shadow-lg shadow-indigo-100"
      >
        {isLoading ? 'Verifying...' : 'Complete Registration'}
      </button>

      <div className="text-center">
        {resendTimer > 0 ? (
          <p className="text-xs text-slate-400 font-bold uppercase tracking-wider">Resend in {resendTimer}s</p>
        ) : (
          <button onClick={handleInitialSubmit} className="text-indigo-600 text-xs font-black uppercase tracking-wider hover:underline">Resend Code</button>
        )}
        <button onClick={() => setStep('input')} className="block w-full mt-6 text-[10px] font-black uppercase tracking-[0.2em] text-slate-300 hover:text-slate-500">Back to Details</button>
      </div>
    </div>
  );

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans">
      {step === 'input' ? renderInputStep() : renderOtpStep()}
    </div>
  );
}