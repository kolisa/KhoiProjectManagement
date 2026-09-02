// src/components/Auth/ResetPasswordForm.jsx
// Reached via the emailed reset link (?token=...) rather than through normal navigation - this app has
// no router, so AuthGuard in App.jsx renders this directly for any request to /reset-password,
// regardless of auth state, using the same manual query-string technique as Wiki/Library share links.
import React, { useState } from 'react';
import { Lock, ArrowLeft, CheckCircle2 } from 'lucide-react';
import ApiService from '../../services/ApiService';

const ResetPasswordForm = ({ token, onBackToLogin }) => {
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState(false);

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (loading) return;
        setError('');

        if (password.length < 8) {
            setError('Password must be at least 8 characters.');
            return;
        }
        if (password !== confirmPassword) {
            setError('Passwords do not match.');
            return;
        }

        setLoading(true);
        try {
            const apiService = new ApiService();
            await apiService.resetPassword(token, password);
            setSuccess(true);
        } catch (err) {
            setError('This reset link is invalid or has expired. Please request a new one.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen flex">
            {/* Brand panel */}
            <div className="hidden lg:flex lg:w-[44%] relative overflow-hidden flex-col justify-between bg-gradient-to-br from-blue-900 via-blue-700 to-blue-600 px-14 py-14">
                <div className="absolute -top-32 -right-32 h-[420px] w-[420px] rounded-full bg-blue-300/20 blur-3xl" />
                <div className="absolute -bottom-40 -left-24 h-[480px] w-[480px] rounded-full bg-blue-400/20 blur-3xl" />

                <div className="relative flex items-center gap-3">
                    <div className="h-9 w-9 rounded-lg bg-white flex items-center justify-center text-blue-700 font-bold text-base flex-shrink-0">K</div>
                    <span className="text-white text-lg font-bold tracking-tight">KhoiHub</span>
                </div>

                <div className="relative max-w-md">
                    <h1 className="text-white text-3xl font-extrabold leading-tight tracking-tight mb-4">
                        Where the whole company keeps its work in one place.
                    </h1>
                    <p className="text-blue-100/80 text-base leading-relaxed">
                        Projects, tasks, wiki, vault and finance &mdash; unified under one roof.
                    </p>
                </div>

                <p className="relative text-blue-200/50 text-xs">&copy; 2026 KhoiHub. All rights reserved.</p>
            </div>

            {/* Form panel */}
            <div className="flex-1 flex items-center justify-center bg-gray-50 px-4 sm:px-6 lg:px-8 py-12">
                <div className="max-w-sm w-full">
                    <div className="lg:hidden flex flex-col items-center mb-8">
                        <div className="flex items-center gap-2.5">
                            <div className="h-10 w-10 rounded-xl bg-blue-600 flex items-center justify-center text-white font-bold text-lg flex-shrink-0">K</div>
                            <span className="text-gray-900 text-xl font-bold tracking-tight">KhoiHub</span>
                        </div>
                    </div>

                    <div className="text-center lg:text-left">
                        {success ? (
                            <>
                                <div className="h-12 w-12 rounded-full bg-green-50 flex items-center justify-center mb-4 mx-auto lg:mx-0">
                                    <CheckCircle2 className="h-6 w-6 text-green-600" />
                                </div>
                                <h2 className="text-2xl font-bold text-gray-900 tracking-tight">Password reset</h2>
                                <p className="mt-1.5 text-sm text-gray-500">
                                    Your password has been updated. All existing sessions have been signed out for security.
                                </p>
                            </>
                        ) : (
                            <>
                                <h2 className="text-2xl font-bold text-gray-900 tracking-tight">Choose a new password</h2>
                                <p className="mt-1.5 text-sm text-gray-500">
                                    Enter a new password for your account.
                                </p>
                            </>
                        )}
                    </div>

                    {!success && (
                        <form className="mt-8 space-y-5" onSubmit={handleSubmit}>
                            <div className="relative">
                                <Lock className="h-5 w-5 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                                <input
                                    type="password"
                                    required
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    placeholder="New password"
                                />
                            </div>
                            <div className="relative">
                                <Lock className="h-5 w-5 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                                <input
                                    type="password"
                                    required
                                    value={confirmPassword}
                                    onChange={(e) => setConfirmPassword(e.target.value)}
                                    className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    placeholder="Confirm new password"
                                />
                            </div>

                            {error && (
                                <div className="rounded-lg bg-red-50 border border-red-100 px-3 py-2 text-sm text-red-600 text-center">
                                    {error}
                                </div>
                            )}

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full flex justify-center py-2.5 px-4 rounded-lg text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 transition-colors shadow-sm"
                            >
                                {loading ? 'Resetting...' : 'Reset password'}
                            </button>
                        </form>
                    )}

                    <button
                        type="button"
                        onClick={onBackToLogin}
                        className="mt-6 w-full flex items-center justify-center lg:justify-start gap-1.5 text-sm font-medium text-gray-500 hover:text-blue-600 transition-colors"
                    >
                        <ArrowLeft className="h-4 w-4" />
                        Back to sign in
                    </button>
                </div>
            </div>
        </div>
    );
};

export default ResetPasswordForm;
