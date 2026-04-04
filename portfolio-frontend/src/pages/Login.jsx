import React, { useState } from "react";
import { auth } from "./firebaseConfig";
import { RecaptchaVerifier, signInWithPhoneNumber } from "firebase/auth";
import { Phone, Key, ArrowRight } from "lucide-react";

const Login = () => {
  const [phone, setPhone] = useState("+91");
  const [otp, setOtp] = useState("");
  const [showOtpInput, setShowOtpInput] = useState(false);
  const [confirmationResult, setConfirmationResult] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const setupRecaptcha = () => {
    if (!window.recaptchaVerifier) {
      window.recaptchaVerifier = new RecaptchaVerifier(
        auth,
        "recaptcha-container",
        {
          size: "invisible",
        },
      );
    }
  };

  const onSendOTP = async () => {
    setIsLoading(true);
    try {
      setupRecaptcha();
      const appVerifier = window.recaptchaVerifier;
      const confirmation = await signInWithPhoneNumber(
        auth,
        phone,
        appVerifier,
      );
      setConfirmationResult(confirmation);
      setShowOtpInput(true);
    } catch (error) {
      console.error("SMS Error:", error);
      alert("Error sending SMS. Check your Firebase settings.");
    } finally {
      setIsLoading(false);
    }
  };

  const onVerifyOTP = async () => {
    setIsLoading(true);
    try {
      const result = await confirmationResult.confirm(otp);
      const idToken = await result.user.getIdToken();

      // Sync with C# Backend
      const response = await fetch("https://localhost:7123/api/auth/sync", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${idToken}`,
          "Content-Type": "application/json",
        },
      });

      const data = await response.json();
      alert("Login Successful! User ID: " + data.userId);
      // navigate('/') ideally goes here
    } catch (error) {
      console.error("Verification Error:", error);
      alert("Invalid OTP");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4 font-sans relative overflow-hidden">
      {/* Soft Background Accents */}
      <div className="absolute top-[-10%] left-[-10%] w-[30rem] h-[30rem] bg-indigo-200 rounded-full mix-blend-multiply filter blur-[100px] opacity-40"></div>

      <div className="w-full max-w-md bg-white/90 backdrop-blur-xl p-10 rounded-[2.5rem] shadow-2xl shadow-indigo-100 border border-white/50 z-10">
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-indigo-600 rounded-2xl flex items-center justify-center text-white font-black text-3xl mx-auto mb-6 shadow-xl shadow-indigo-200">
            K
          </div>
          <h2 className="text-3xl font-black text-slate-800 tracking-tight">
            Phone Sign In
          </h2>
          <p className="text-slate-500 mt-2 text-sm font-medium">
            Securely access Kinetic Capital
          </p>
        </div>

        <div id="recaptcha-container"></div>

        {!showOtpInput ? (
          <div className="space-y-4 animate-in fade-in duration-500">
            <div className="relative group">
              <Phone
                className="absolute left-4 top-4.5 text-slate-400 group-focus-within:text-indigo-500 transition-colors"
                size={20}
              />
              <input
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="+91 98765 43210"
                className="w-full p-4 pl-12 bg-slate-50/50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 focus:bg-white outline-none transition-all font-medium text-slate-700"
              />
            </div>
            <button
              onClick={onSendOTP}
              disabled={isLoading}
              className="w-full bg-indigo-600 text-white p-4 rounded-2xl font-black hover:bg-indigo-700 transition-all flex items-center justify-center gap-2 disabled:opacity-50 shadow-lg shadow-indigo-200 mt-2 active:scale-[0.98]"
            >
              {isLoading ? "Sending..." : "Send OTP"}
              {!isLoading && <ArrowRight size={18} />}
            </button>
          </div>
        ) : (
          <div className="space-y-4 animate-in slide-in-from-right-4 duration-500">
            <div className="relative group">
              <Key
                className="absolute left-4 top-4.5 text-slate-400 group-focus-within:text-indigo-500 transition-colors"
                size={20}
              />
              <input
                value={otp}
                onChange={(e) => setOtp(e.target.value)}
                placeholder="Enter 6-digit OTP"
                maxLength={6}
                className="w-full p-4 pl-12 text-center tracking-[0.5em] font-black bg-slate-50/50 border border-slate-200 rounded-2xl focus:ring-2 focus:ring-indigo-500 focus:bg-white outline-none transition-all text-slate-700"
              />
            </div>
            <button
              onClick={onVerifyOTP}
              disabled={isLoading || otp.length < 6}
              className="w-full bg-emerald-600 text-white p-4 rounded-2xl font-black hover:bg-emerald-700 transition-all flex items-center justify-center gap-2 disabled:opacity-50 shadow-lg shadow-emerald-200 mt-2 active:scale-[0.98]"
            >
              {isLoading ? "Verifying..." : "Verify & Login"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default Login;
