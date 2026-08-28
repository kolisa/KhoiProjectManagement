// src/components/Auth/LoginForm.js
// Extracted from App.jsx (2026-08-28) as part of decomposing that file - previously inline, now a
// real, live component (unlike the AuthGuard.jsx/LoginForm.jsx pair that was deleted earlier the same
// day as confirmed dead code; this is a fresh file at that path, not a resurrection of the old one).
import React, { useState } from 'react';
import { CheckCircle, Mail, Lock, Eye, EyeOff } from 'lucide-react';
import { NetworkError } from '../../services/ApiService';
import { useAuth } from '../../contexts/AuthContext';
import khoiLogo from '../../assets/khoi-logo.png';

const LoginForm = ({ onForgotPassword, onMustChangePassword }) => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(true);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const { login } = useAuth();

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError('');

        try {
            const result = await login(email, password, rememberMe);
            if (result?.mustChangePassword) {
                onMustChangePassword(result.passwordResetToken);
            }
        } catch (error) {
            // A NetworkError means the request never reached the server at all (CORS block, offline,
            // timeout, DNS failure) - blaming the password for that is actively misleading and sends
            // people chasing the wrong problem (this exact confusion is why this distinction exists -
            // see ApiService.js's NetworkError).
            setError(
                error instanceof NetworkError
                    ? "Couldn't reach the server. Check your connection, or that this site is allowed to call the API (CORS)."
                    : 'Invalid email or password'
            );
        } finally {
            setLoading(false);
        }
    };

    const inputWrapClass = 'relative flex items-center rounded-[10px] border border-gray-200 bg-gray-50 transition-all focus-within:bg-white focus-within:border-blue-500 focus-within:ring-4 focus-within:ring-blue-500/10';
    const inputIconClass = 'h-[18px] w-[18px] ml-3.5 flex-shrink-0 text-gray-400 transition-colors peer-focus:text-blue-600';

    return (
        <div className="min-h-screen flex bg-gray-50">
            {/* Brand panel */}
            <div className="hidden lg:flex lg:w-[44%] relative overflow-hidden flex-col justify-between bg-gradient-to-br from-blue-900 via-blue-700 to-blue-600 px-14 py-14">
                <div
                    className="absolute inset-0 opacity-[0.07]"
                    style={{ backgroundImage: 'linear-gradient(rgba(255,255,255,0.6) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.6) 1px, transparent 1px)', backgroundSize: '42px 42px' }}
                />
                <div className="absolute -top-32 -right-32 h-[420px] w-[420px] rounded-full bg-blue-300/20 blur-3xl" />
                <div className="absolute -bottom-40 -left-24 h-[480px] w-[480px] rounded-full bg-blue-400/20 blur-3xl" />

                <div className="relative flex items-center gap-3">
                    <img src={khoiLogo} alt="Khoi" className="h-8 w-auto brightness-0 invert" />
                    <span className="text-white text-lg font-bold tracking-tight">Khoi Pro</span>
                </div>

                <div className="relative max-w-md">
                    <h1 className="text-white text-[34px] font-extrabold leading-[1.15] tracking-tight mb-4">
                        Where the whole company keeps its work in one place.
                    </h1>
                    <p className="text-blue-100/80 text-base leading-relaxed">
                        Projects, tasks, wiki, vault and finance &mdash; unified under one roof.
                    </p>

                    <div className="flex flex-col gap-3.5 mt-9">
                        {[
                            'Space-based permissions, inherited automatically',
                            'A secrets vault with a full audit trail',
                            'Timesheets, invoicing and reminders built in',
                        ].map((feature) => (
                            <div key={feature} className="flex items-center gap-3">
                                <div className="h-6 w-6 rounded-md bg-white/15 ring-1 ring-white/10 flex items-center justify-center flex-shrink-0">
                                    <CheckCircle className="h-3.5 w-3.5 text-white" />
                                </div>
                                <span className="text-blue-50/90 text-sm">{feature}</span>
                            </div>
                        ))}
                    </div>
                </div>

                <p className="relative text-blue-200/50 text-xs">&copy; 2026 Khoi. All rights reserved.</p>
            </div>

            {/* Form panel */}
            <div className="flex-1 flex items-center justify-center px-4 sm:px-6 lg:px-8 py-12">
                <div className="max-w-sm w-full">
                    <div className="lg:hidden flex flex-col items-center mb-8">
                        <img src={khoiLogo} alt="Khoi" className="h-10 w-auto mb-3" />
                    </div>

                    <div className="relative bg-white rounded-[20px] border border-gray-100 shadow-[0_2px_4px_rgba(16,24,40,0.04),0_20px_48px_-12px_rgba(16,24,40,0.14)] p-8 sm:p-10">
                        <div className="absolute inset-x-0 top-0 h-1 rounded-t-[20px] bg-gradient-to-r from-blue-600 via-blue-500 to-blue-400" />
                        <div className="hidden lg:flex h-11 w-11 rounded-xl bg-blue-50 items-center justify-center mb-5">
                            <Lock className="h-5 w-5 text-blue-600" />
                        </div>
                        <h2 className="text-[26px] font-bold text-gray-900 tracking-tight">
                            Sign in to Khoi Pro
                        </h2>
                        <p className="mt-1.5 text-sm text-gray-500">
                            Enter your credentials to access the project management system
                        </p>

                        <form className="mt-8 space-y-4" onSubmit={handleSubmit}>
                            <div className={inputWrapClass}>
                                <Mail className={inputIconClass} />
                                <input
                                    type="email"
                                    required
                                    value={email}
                                    onChange={(e) => setEmail(e.target.value)}
                                    className="peer w-full pl-2.5 pr-4 py-3 bg-transparent rounded-[10px] text-[15px] text-gray-900 placeholder-gray-400 focus:outline-none"
                                    placeholder="Email address"
                                />
                            </div>
                            <div className={inputWrapClass}>
                                <Lock className={inputIconClass} />
                                <input
                                    type={showPassword ? 'text' : 'password'}
                                    required
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    className="peer w-full pl-2.5 pr-2 py-3 bg-transparent rounded-[10px] text-[15px] text-gray-900 placeholder-gray-400 focus:outline-none"
                                    placeholder="Password"
                                />
                                <button
                                    type="button"
                                    onClick={() => setShowPassword((v) => !v)}
                                    className="mr-2.5 flex-shrink-0 text-gray-400 hover:text-gray-600 transition-colors p-1 -m-1"
                                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                                    tabIndex={-1}
                                >
                                    {showPassword ? <EyeOff className="h-[18px] w-[18px]" /> : <Eye className="h-[18px] w-[18px]" />}
                                </button>
                            </div>

                            <div className="flex items-center justify-between">
                                <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
                                    <input
                                        type="checkbox"
                                        checked={rememberMe}
                                        onChange={(e) => setRememberMe(e.target.checked)}
                                        className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                    />
                                    Remember me for 30 days
                                </label>
                                <button
                                    type="button"
                                    onClick={onForgotPassword}
                                    className="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors"
                                >
                                    Forgot password?
                                </button>
                            </div>

                            {error && (
                                <div className="rounded-[10px] bg-red-50 border border-red-100 px-3.5 py-2.5 text-sm text-red-600 text-center">
                                    {error}
                                </div>
                            )}

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full flex items-center justify-center gap-2 py-3 px-4 rounded-[10px] text-[15px] font-semibold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 focus:outline-none focus:ring-4 focus:ring-blue-500/25 disabled:opacity-60 disabled:cursor-not-allowed transition-all shadow-sm hover:shadow-md"
                            >
                                {loading && (
                                    <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                )}
                                {loading ? 'Signing in...' : 'Sign in'}
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LoginForm;
