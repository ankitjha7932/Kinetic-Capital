import React, { useState, useRef, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import api from "../api/axios";
import { GoogleLogin } from "@react-oauth/google";

/* ─── OTP digit sub-component ─── */
function OtpBox({ index, otpArr, setOtpArr, inputRefs }) {
  const handleChange = (e) => {
    const val = e.target.value.replace(/\D/g, "").slice(-1);
    const next = [...otpArr];
    next[index] = val;
    setOtpArr(next);
    if (val && index < 5) inputRefs.current[index + 1]?.focus();
  };
  const handleKeyDown = (e) => {
    if (e.key === "Backspace" && !otpArr[index] && index > 0)
      inputRefs.current[index - 1]?.focus();
  };
  const filled = !!otpArr[index];
  return (
    <input
      ref={(el) => (inputRefs.current[index] = el)}
      type="text"
      inputMode="numeric"
      maxLength={1}
      value={otpArr[index]}
      onChange={handleChange}
      onKeyDown={handleKeyDown}
      style={{
        width: 48,
        height: 56,
        textAlign: "center",
        fontSize: 22,
        fontWeight: 600,
        fontFamily: "'DM Mono', monospace",
        border: filled ? "1.5px solid #6366f1" : "1.5px solid #e2e5f0",
        borderRadius: 12,
        background: filled ? "#f5f4ff" : "#fafafa",
        color: "#1a1a2e",
        outline: "none",
        transition: "all 0.15s",
        caretColor: "#6366f1",
      }}
      onFocus={(e) =>
        (e.target.style.boxShadow = "0 0 0 3px rgba(99,102,241,0.15)")
      }
      onBlur={(e) => (e.target.style.boxShadow = "none")}
    />
  );
}

export default function Auth({ onLoginSuccess }) {
  const navigate = useNavigate();
  const [isLogin, setIsLogin] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [otpSent, setOtpSent] = useState(false);
  const [otpArr, setOtpArr] = useState(["", "", "", "", "", ""]);
  const [error, setError] = useState("");
  const inputRefs = useRef([]);

  const [formData, setFormData] = useState({
    email: "",
    password: "",
    riskProfile: "Moderate",
    investmentHorizon: 5,
  });

  useEffect(() => {
    if (otpSent) setTimeout(() => inputRefs.current[0]?.focus(), 100);
  }, [otpSent]);

  const handleGoogleSuccess = async (credentialResponse) => {
    setIsLoading(true);
    setError("");
    try {
      const res = await api.post("/auth/google-login", {
        token: credentialResponse.credential,
      });
      onLoginSuccess(res.data.token, res.data.userId);
      navigate("/");
    } catch {
      setError("Google Sign-In failed. Please try again.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleEmailSubmit = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");
    try {
      if (isLogin) {
        const res = await api.post("/auth/login", {
          email: formData.email,
          password: formData.password,
        });
        onLoginSuccess(res.data.token, res.data.userId);
        navigate("/");
      } else if (!otpSent) {
        await api.post("/auth/send-otp", {
          email: formData.email,
          flow: "register",
        });
        setOtpSent(true);
      } else {
        const otp = otpArr.join("");
        const res = await api.post("/auth/verify-otp-register", {
          email: formData.email,
          otp,
        });
        onLoginSuccess(res.data.token, res.data.userId);
        navigate("/");
      }
    } catch (err) {
      setError(
        err.response?.data?.message ||
        err.response?.data ||
        "Authentication failed. Please try again."
      );
    } finally {
      setIsLoading(false);
    }
  };

  const switchMode = () => {
    setIsLogin(!isLogin);
    setShowPassword(false);
    setOtpSent(false);
    setOtpArr(["", "", "", "", "", ""]);
    setError("");
  };

  /* ─── Styles ─── */
  const s = {
    page: {
      minHeight: "100vh",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      background: "#f7f7fb",
      fontFamily: "'DM Sans', sans-serif",
      padding: "1.5rem",
      position: "relative",
      overflow: "hidden",
    },
    noise: {
      position: "fixed",
      inset: 0,
      backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' opacity='0.03'/%3E%3C/svg%3E")`,
      pointerEvents: "none",
      zIndex: 0,
    },
    accent1: {
      position: "absolute",
      width: 420,
      height: 420,
      borderRadius: "50%",
      background: "radial-gradient(circle, #e0e7ff 0%, transparent 70%)",
      top: "-120px",
      right: "-80px",
      zIndex: 0,
    },
    accent2: {
      position: "absolute",
      width: 320,
      height: 320,
      borderRadius: "50%",
      background: "radial-gradient(circle, #ede9fe 0%, transparent 70%)",
      bottom: "-80px",
      left: "-60px",
      zIndex: 0,
    },
    wrap: {
      position: "relative",
      zIndex: 1,
      width: "100%",
      maxWidth: 420,
    },
    card: {
      background: "#ffffff",
      borderRadius: 24,
      border: "1px solid #ebebf5",
      overflow: "hidden",
      boxShadow: "0 4px 40px rgba(99,102,241,0.07), 0 1px 4px rgba(0,0,0,0.04)",
    },
    cardInner: { padding: "2rem 2rem 1.75rem" },
    logoRow: {
      display: "flex",
      alignItems: "center",
      gap: 10,
      marginBottom: "1.75rem",
    },
    logoBox: {
      width: 34,
      height: 34,
      background: "#6366f1",
      borderRadius: 9,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      color: "#fff",
      fontSize: 16,
      fontWeight: 700,
      letterSpacing: -0.5,
      flexShrink: 0,
    },
    logoText: {
      fontSize: 14,
      fontWeight: 600,
      color: "#1a1a2e",
      letterSpacing: -0.2,
    },
    logoSub: { fontSize: 11, color: "#9ca3af", marginTop: 1 },
    tabRow: {
      display: "flex",
      background: "#f4f4f9",
      borderRadius: 12,
      padding: 3,
      marginBottom: "1.5rem",
    },
    tab: (active) => ({
      flex: 1,
      padding: "8px 0",
      textAlign: "center",
      fontSize: 13,
      fontWeight: 600,
      borderRadius: 9,
      cursor: "pointer",
      border: "none",
      fontFamily: "'DM Sans', sans-serif",
      transition: "all 0.18s",
      background: active ? "#ffffff" : "transparent",
      color: active ? "#6366f1" : "#9ca3af",
      boxShadow: active ? "0 1px 4px rgba(0,0,0,0.07)" : "none",
    }),
    googleWrap: { marginBottom: "1.25rem" },
    divider: {
      display: "flex",
      alignItems: "center",
      gap: 12,
      marginBottom: "1.25rem",
    },
    divLine: { flex: 1, height: 1, background: "#f0f0f7" },
    divText: { fontSize: 11, color: "#c4c4d4", fontWeight: 600, letterSpacing: 0.5 },
    field: { marginBottom: 12 },
    label: {
      fontSize: 11,
      fontWeight: 600,
      color: "#9ca3af",
      textTransform: "uppercase",
      letterSpacing: 0.6,
      display: "block",
      marginBottom: 6,
    },
    input: {
      width: "100%",
      padding: "10px 14px",
      fontSize: 14,
      fontFamily: "'DM Sans', sans-serif",
      fontWeight: 500,
      color: "#1a1a2e",
      background: "#fafafa",
      border: "1.5px solid #e8e8f0",
      borderRadius: 12,
      outline: "none",
      transition: "border-color 0.15s, box-shadow 0.15s",
      boxSizing: "border-box",
    },
    pwWrap: { position: "relative" },
    pwInput: {
      width: "100%",
      padding: "10px 44px 10px 14px",
      fontSize: 14,
      fontFamily: "'DM Sans', sans-serif",
      fontWeight: 500,
      color: "#1a1a2e",
      background: "#fafafa",
      border: "1.5px solid #e8e8f0",
      borderRadius: 12,
      outline: "none",
      transition: "border-color 0.15s, box-shadow 0.15s",
      boxSizing: "border-box",
    },
    eyeBtn: {
      position: "absolute",
      right: 12,
      top: "50%",
      transform: "translateY(-50%)",
      background: "none",
      border: "none",
      cursor: "pointer",
      color: "#c4c4d4",
      display: "flex",
      alignItems: "center",
      padding: 0,
    },
    gridTwo: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 },
    select: {
      width: "100%",
      padding: "10px 14px",
      fontSize: 14,
      fontFamily: "'DM Sans', sans-serif",
      fontWeight: 500,
      color: "#1a1a2e",
      background: "#fafafa",
      border: "1.5px solid #e8e8f0",
      borderRadius: 12,
      outline: "none",
      boxSizing: "border-box",
      appearance: "none",
      cursor: "pointer",
    },
    forgotRow: { textAlign: "right", marginBottom: "1rem" },
    forgotLink: {
      fontSize: 12,
      color: "#6366f1",
      fontWeight: 600,
      textDecoration: "none",
    },
    submitBtn: {
      width: "100%",
      padding: "11px 16px",
      background: isLoading ? "#a5b4fc" : "#6366f1",
      color: "#ffffff",
      border: "none",
      borderRadius: 12,
      fontSize: 14,
      fontWeight: 700,
      fontFamily: "'DM Sans', sans-serif",
      cursor: isLoading ? "not-allowed" : "pointer",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      gap: 8,
      transition: "background 0.15s, transform 0.1s",
      letterSpacing: -0.1,
    },
    errorBox: {
      background: "#fff5f5",
      border: "1px solid #fecaca",
      borderRadius: 10,
      padding: "10px 14px",
      fontSize: 13,
      color: "#dc2626",
      marginTop: 12,
      fontWeight: 500,
    },
    footer: {
      borderTop: "1px solid #f0f0f7",
      padding: "1.1rem 2rem",
      textAlign: "center",
      fontSize: 13,
      color: "#9ca3af",
      fontWeight: 500,
    },
    footerLink: {
      color: "#6366f1",
      fontWeight: 700,
      cursor: "pointer",
      background: "none",
      border: "none",
      fontFamily: "'DM Sans', sans-serif",
      fontSize: 13,
    },
    /* OTP screen */
    otpHeading: {
      fontSize: 20,
      fontWeight: 700,
      color: "#1a1a2e",
      marginBottom: 4,
      letterSpacing: -0.4,
    },
    otpSub: {
      fontSize: 13,
      color: "#9ca3af",
      marginBottom: "1.5rem",
      lineHeight: 1.6,
    },
    otpEmail: { color: "#6366f1", fontWeight: 600 },
    otpRow: {
      display: "flex",
      gap: 8,
      justifyContent: "center",
      marginBottom: "1.5rem",
    },
    backBtn: {
      background: "none",
      border: "none",
      cursor: "pointer",
      fontSize: 13,
      color: "#9ca3af",
      fontFamily: "'DM Sans', sans-serif",
      display: "flex",
      alignItems: "center",
      gap: 4,
      padding: 0,
      marginBottom: "1.25rem",
      fontWeight: 600,
    },
    resendRow: {
      textAlign: "center",
      fontSize: 12,
      color: "#c4c4d4",
      marginTop: 14,
      fontWeight: 500,
    },
    resendLink: {
      color: "#6366f1",
      fontWeight: 700,
      cursor: "pointer",
      background: "none",
      border: "none",
      fontFamily: "'DM Sans', sans-serif",
      fontSize: 12,
    },
  };

  const focusStyle = (e) => {
    e.target.style.borderColor = "#6366f1";
    e.target.style.boxShadow = "0 0 0 3px rgba(99,102,241,0.1)";
    e.target.style.background = "#fff";
  };
  const blurStyle = (e) => {
    e.target.style.borderColor = "#e8e8f0";
    e.target.style.boxShadow = "none";
    e.target.style.background = "#fafafa";
  };

  return (
    <>
      <link
        href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&family=DM+Mono:wght@500&display=swap"
        rel="stylesheet"
      />
      <div style={s.page}>
        <div style={s.noise} />
        <div style={s.accent1} />
        <div style={s.accent2} />

        <div style={s.wrap}>
          <div style={s.card}>
            <div style={s.cardInner}>
              {/* Brand */}
              <div style={s.logoRow}>
                <div style={s.logoBox}>K</div>
                <div>
                  <div style={s.logoText}>Kinetic Capital</div>
                  <div style={s.logoSub}>Investment Portfolio</div>
                </div>
              </div>

              {/* ── OTP screen ── */}
              {otpSent ? (
                <form onSubmit={handleEmailSubmit}>
                  <button
                    type="button"
                    style={s.backBtn}
                    onClick={() => { setOtpSent(false); setOtpArr(["", "", "", "", "", ""]); setError(""); }}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
                    Back
                  </button>
                  <div style={s.otpHeading}>Check your inbox</div>
                  <div style={s.otpSub}>
                    We sent a 6-digit code to{" "}
                    <span style={s.otpEmail}>{formData.email}</span>.<br />
                    It expires in 10 minutes.
                  </div>
                  <div style={s.otpRow}>
                    {otpArr.map((_, i) => (
                      <OtpBox
                        key={i}
                        index={i}
                        otpArr={otpArr}
                        setOtpArr={setOtpArr}
                        inputRefs={inputRefs}
                      />
                    ))}
                  </div>
                  <button
                    type="submit"
                    disabled={isLoading || otpArr.join("").length < 6}
                    style={{
                      ...s.submitBtn,
                      background:
                        isLoading || otpArr.join("").length < 6
                          ? "#a5b4fc"
                          : "#6366f1",
                      cursor:
                        isLoading || otpArr.join("").length < 6
                          ? "not-allowed"
                          : "pointer",
                    }}
                  >
                    {isLoading ? (
                      "Verifying..."
                    ) : (
                      <>
                        Verify & create account
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M5 12h14M12 5l7 7-7 7" /></svg>
                      </>
                    )}
                  </button>
                  {error && <div style={s.errorBox}>{error}</div>}
                  <div style={s.resendRow}>
                    Didn't receive it?{" "}
                    <button type="button" style={s.resendLink}
                      onClick={async () => {
                        try { await api.post("/auth/send-otp", { email: formData.email, flow: "register" }); setError(""); }
                        catch { setError("Failed to resend. Try again."); }
                      }}
                    >
                      Resend code
                    </button>
                  </div>
                </form>
              ) : (
                /* ── Main form ── */
                <>
                  <div style={s.tabRow}>
                    <button style={s.tab(isLogin)} onClick={() => { setIsLogin(true); setError(""); }}>Sign in</button>
                    <button style={s.tab(!isLogin)} onClick={() => { setIsLogin(false); setError(""); }}>Create account</button>
                  </div>

                  {/* Google */}
                  <div style={s.googleWrap}>
                    <GoogleLogin
                      onSuccess={handleGoogleSuccess}
                      onError={() => setError("Google sign-in failed.")}
                      theme="outline"
                      size="large"
                      width="100%"
                      text={isLogin ? "signin_with" : "signup_with"}
                      shape="rectangular"
                    />
                  </div>

                  <div style={s.divider}>
                    <div style={s.divLine} />
                    <span style={s.divText}>OR</span>
                    <div style={s.divLine} />
                  </div>

                  <form onSubmit={handleEmailSubmit}>
                    {/* Email */}
                    <div style={s.field}>
                      <label style={s.label}>Email</label>
                      <input
                        type="email"
                        placeholder="you@example.com"
                        style={s.input}
                        value={formData.email}
                        onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                        onFocus={focusStyle}
                        onBlur={blurStyle}
                        required
                      />
                    </div>

                    {/* Password */}
                    <div style={s.field}>
                      <label style={s.label}>Password</label>
                      <div style={s.pwWrap}>
                        <input
                          type={showPassword ? "text" : "password"}
                          placeholder="••••••••"
                          style={s.pwInput}
                          value={formData.password}
                          onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                          onFocus={focusStyle}
                          onBlur={blurStyle}
                          required
                        />
                        <button type="button" style={s.eyeBtn} onClick={() => setShowPassword(!showPassword)}>
                          {showPassword ? (
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" /><line x1="1" y1="1" x2="23" y2="23" /></svg>
                          ) : (
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" /><circle cx="12" cy="12" r="3" /></svg>
                          )}
                        </button>
                      </div>
                    </div>

                    {/* Register extras */}
                    {!isLogin && (
                      <div style={{ ...s.gridTwo, marginBottom: 12 }}>
                        <div>
                          <label style={s.label}>Risk profile</label>
                          <select
                            style={s.select}
                            value={formData.riskProfile}
                            onChange={(e) => setFormData({ ...formData, riskProfile: e.target.value })}
                          >
                            <option value="Low">Low</option>
                            <option value="Moderate">Moderate</option>
                            <option value="High">High</option>
                          </select>
                        </div>
                        <div>
                          <label style={s.label}>Horizon (yrs)</label>
                          <input
                            type="number"
                            min={1}
                            max={40}
                            style={s.input}
                            value={formData.investmentHorizon}
                            onChange={(e) => setFormData({ ...formData, investmentHorizon: e.target.value })}
                            onFocus={focusStyle}
                            onBlur={blurStyle}
                          />
                        </div>
                      </div>
                    )}

                    {/* Forgot */}
                    {isLogin && (
                      <div style={s.forgotRow}>
                        <Link to="/forgot-password" style={s.forgotLink}>
                          Forgot password?
                        </Link>
                      </div>
                    )}

                    <button
                      type="submit"
                      disabled={isLoading}
                      style={s.submitBtn}
                      onMouseEnter={(e) => !isLoading && (e.target.style.background = "#4f46e5")}
                      onMouseLeave={(e) => !isLoading && (e.target.style.background = "#6366f1")}
                    >
                      {isLoading ? (
                        "Processing..."
                      ) : isLogin ? (
                        <>Sign in <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M5 12h14M12 5l7 7-7 7" /></svg></>
                      ) : (
                        <>Send verification code <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M5 12h14M12 5l7 7-7 7" /></svg></>
                      )}
                    </button>

                    {error && <div style={s.errorBox}>{error}</div>}
                  </form>
                </>
              )}
            </div>

            {/* Footer toggle */}
            {!otpSent && (
              <div style={s.footer}>
                {isLogin ? (
                  <>New to Kinetic?{" "}<button style={s.footerLink} onClick={switchMode}>Create an account</button></>
                ) : (
                  <>Already have an account?{" "}<button style={s.footerLink} onClick={switchMode}>Sign in</button></>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </>
  );
}