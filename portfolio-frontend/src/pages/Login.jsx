import React, { useState } from 'react';
import { auth } from './firebaseConfig'; // You'll need to create this config file
import { RecaptchaVerifier, signInWithPhoneNumber } from "firebase/auth";

const Login = () => {
    const [phone, setPhone] = useState('+91');
    const [otp, setOtp] = useState('');
    const [showOtpInput, setShowOtpInput] = useState(false);
    const [confirmationResult, setConfirmationResult] = useState(null);

    const setupRecaptcha = () => {
        if (!window.recaptchaVerifier) {
            window.recaptchaVerifier = new RecaptchaVerifier(auth, 'recaptcha-container', {
                'size': 'invisible'
            });
        }
    };

    const onSendOTP = async () => {
        try {
            setupRecaptcha();
            const appVerifier = window.recaptchaVerifier;
            const confirmation = await signInWithPhoneNumber(auth, phone, appVerifier);
            setConfirmationResult(confirmation);
            setShowOtpInput(true);
            alert("OTP Sent to " + phone);
        } catch (error) {
            console.error("SMS Error:", error);
            alert("Error sending SMS. Check if you added your number to Firebase Test Numbers.");
        }
    };

    const onVerifyOTP = async () => {
        try {
            const result = await confirmationResult.confirm(otp);
            const idToken = await result.user.getIdToken();
            
            // --- SEND TO YOUR C# BACKEND ---
            const response = await fetch("https://localhost:7123/api/auth/sync", {
                method: "POST",
                headers: {
                    "Authorization": `Bearer ${idToken}`,
                    "Content-Type": "application/json"
                }
            });

            const data = await response.json();
            console.log("Backend Sync Result:", data);
            alert("Login Successful! User ID: " + data.userId);
        } catch (error) {
            console.error("Verification Error:", error);
            alert("Invalid OTP");
        }
    };

    return (
        <div style={{ padding: '20px' }}>
            <h2>Kinetic Capital Login</h2>
            <div id="recaptcha-container"></div> {/* Important for Firebase */}
            
            {!showOtpInput ? (
                <>
                    <input 
                        value={phone} 
                        onChange={(e) => setPhone(e.target.value)} 
                        placeholder="+919876543210"
                    />
                    <button onClick={onSendOTP}>Send OTP</button>
                </>
            ) : (
                <>
                    <input 
                        value={otp} 
                        onChange={(e) => setOtp(e.target.value)} 
                        placeholder="Enter 6-digit OTP"
                    />
                    <button onClick={onVerifyOTP}>Verify & Login</button>
                </>
            )}
        </div>
    );
};

export default Login;