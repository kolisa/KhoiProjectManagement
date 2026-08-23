// src/components/Auth/ResetPasswordForm.jsx
// Reached via the emailed reset link (?token=...) rather than through normal navigation - this app has
// no router, so AuthGuard in App.jsx renders this directly for any request to /reset-password,
// regardless of auth state, using the same manual query-string technique as Wiki/Library share links.
import React, { useState } from 'react';
import { Lock, ArrowLeft, CheckCircle2 } from 'lucide-react';
import ApiService from '../../services/ApiService';
import khoiLogo from '../../assets/khoi-logo.png';

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
        <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-gray-50 via-blue-50 to-gray-100 py-12 px-4 sm:px-6 lg:px-8 relative overflow-hidden">
            <div className="absolute -top-24 -left-24 h-96 w-96 rounded-full bg-blue-200/40 blur-3xl" />
            <div className="absolute -bottom-24 -right-24 h-96 w-96 rounded-full bg-blue-300/30 blur-3xl" />

            <div className="max-w-md w-full space-y-8 relative">
                <div className="bg-white/90 backdrop-blur-sm rounded-2xl shadow-xl border border-gray-100 px-8 py-10">
                    <div className="flex flex-col items-center">
                        <img src={khoiLogo} alt="Khoi" className="h-12 w-auto mb-4" />
                        {success ? (
                            <>
                                <div className="h-12 w-12 rounded-full bg-green-100 flex items-center justify-center mb-4">
                                    <CheckCircle2 className="h-6 w-6 text-green-600" />
                                </div>
                                <h2 className="text-2xl font-bold text-gray-900 text-center">Password reset</h2>
                                <p className="mt-2 text-sm text-gray-600 text-center">
                                    Your password has been updated. All existing sessions have been signed out for security.
                                </p>
                            </>
                        ) : (
                            <>
                                <h2 className="text-2xl font-bold text-gray-900 text-center">Choose a new password</h2>
                                <p className="mt-2 text-sm text-gray-600 text-center">
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
                        className="mt-6 w-full flex items-center justify-center text-sm font-medium text-gray-500 hover:text-blue-600 transition-colors"
                    >
                        <ArrowLeft className="h-4 w-4 mr-1.5" />
                        Back to sign in
                    </button>
                </div>
            </div>
        </div>
    );
};

export default ResetPasswordForm;
