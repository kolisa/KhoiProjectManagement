// src/contexts/AuthContext.js
import React, { createContext, useContext, useState, useEffect, useRef } from 'react';
import { Clock } from 'lucide-react';
import ApiService, { onSessionExpired, getStoredToken } from '../services/ApiService';
import { useToast } from './ToastContext';
import useModalA11y from '../components/Common/useModalA11y';

const AuthContext = createContext();

// Auto-logout after this many minutes of no mouse/keyboard/scroll/touch activity - deliberately
// matches Jwt:AccessTokenExpiryMinutes (appsettings.json) so a fully idle session and its access
// token both lapse around the same time, not two independently-chosen numbers. The last
// WARNING_SECONDS of that window show a countdown modal offering to stay signed in before the
// logout actually happens, rather than logging the user out with no warning mid-idle.
const IDLE_TIMEOUT_MINUTES = 15;
const WARNING_SECONDS = 60;
const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart'];

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);
    const toast = useToast();

    // ApiService (any instance - it's a module-level event, see ApiService.js) fires this once a 401
    // survives a silent refresh attempt. This is the one place that reacts: clear the user so
    // AuthGuard falls back to the login screen on its own, and show a single clear toast instead of
    // every in-flight component's own catch block separately alerting/toasting the same thing.
    useEffect(() => {
        const unsubscribe = onSessionExpired(() => {
            setUser(null);
            toast.info('Your session has expired - please log in again.');
        });
        return unsubscribe;
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        const loadCurrentUser = async () => {
            const token = getStoredToken();
            if (token) {
                try {
                    const apiService = new ApiService();
                    const response = await apiService.getMe();
                    if (response?.user) {
                        setUser({ ...response.user, permissions: response.permissions || [] });
                    }
                } catch (error) {
                    console.error('Failed to load current user:', error);
                    localStorage.removeItem('jwt_token');
                    localStorage.removeItem('refresh_token');
                    sessionStorage.removeItem('jwt_token');
                    sessionStorage.removeItem('refresh_token');
                }
            }
            setLoading(false);
        };

        loadCurrentUser();
    }, []);

    const login = async (email, password, remember = true) => {
        const apiService = new ApiService();
        const response = await apiService.login(email, password, remember);
        // A temp/forced-reset password authenticates correctly but issues no session - the caller
        // (LoginForm) must send the person to the reset-password flow instead of treating this as a
        // normal login, so don't set user or throw here.
        if (response?.mustChangePassword) {
            return response;
        }
        if (response?.user) {
            setUser({ ...response.user, permissions: response.permissions || [] });
            return response;
        }
        throw new Error('Login failed');
    };

    // preserveLastTab: an explicit, deliberate logout always starts fresh at the dashboard next time -
    // only an auto-logout (session expiry via the 401 handler above, or the idle-timeout below)
    // preserves the last-active tab for restoration after the user logs back in.
    const logout = async (preserveLastTab = false) => {
        const apiService = new ApiService();
        setUser(null);
        if (!preserveLastTab) {
            localStorage.removeItem('khoi_last_tab');
        }
        await apiService.logout();
    };

    // ---- Idle timeout: auto-logout after IDLE_TIMEOUT_MINUTES of no activity ----
    const [idleWarningSecondsLeft, setIdleWarningSecondsLeft] = useState(null); // null = warning not showing
    const idleTimerRef = useRef(null);
    const warningTimerRef = useRef(null);
    const countdownIntervalRef = useRef(null);

    const clearIdleTimers = () => {
        clearTimeout(idleTimerRef.current);
        clearTimeout(warningTimerRef.current);
        clearInterval(countdownIntervalRef.current);
    };

    const resetIdleTimer = () => {
        clearIdleTimers();
        setIdleWarningSecondsLeft(null);
        if (!user) return;

        const idleMs = IDLE_TIMEOUT_MINUTES * 60 * 1000;
        const warningMs = WARNING_SECONDS * 1000;

        warningTimerRef.current = setTimeout(() => {
            setIdleWarningSecondsLeft(WARNING_SECONDS);
            countdownIntervalRef.current = setInterval(() => {
                setIdleWarningSecondsLeft((prev) => (prev !== null && prev > 1 ? prev - 1 : 0));
            }, 1000);
        }, idleMs - warningMs);

        idleTimerRef.current = setTimeout(async () => {
            clearIdleTimers();
            setIdleWarningSecondsLeft(null);
            await logout(true);
            toast.info("You've been signed out after a period of inactivity.");
        }, idleMs);
    };

    useEffect(() => {
        if (!user) {
            clearIdleTimers();
            setIdleWarningSecondsLeft(null);
            return;
        }

        // Throttled - mousemove alone can fire dozens of times a second, and every reset just needs
        // to happen within about a second of real activity, not on every pixel of movement.
        let lastReset = 0;
        const handleActivity = () => {
            const now = Date.now();
            if (now - lastReset < 1000) return;
            lastReset = now;
            resetIdleTimer();
        };

        resetIdleTimer();
        ACTIVITY_EVENTS.forEach((evt) => window.addEventListener(evt, handleActivity));

        return () => {
            clearIdleTimers();
            ACTIVITY_EVENTS.forEach((evt) => window.removeEventListener(evt, handleActivity));
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [user]);

    const idleModalRef = useModalA11y(() => resetIdleTimer());

    const value = {
        user,
        login,
        logout,
        loading
    };

    return (
        <AuthContext.Provider value={value}>
            {children}
            {idleWarningSecondsLeft !== null && (
                <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-[120]">
                    <div
                        ref={idleModalRef}
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="idle-timeout-title"
                        aria-describedby="idle-timeout-message"
                        tabIndex={-1}
                        className="bg-white rounded-2xl shadow-xl max-w-sm w-full outline-none"
                    >
                        <div className="px-6 pt-6 pb-2 flex items-start gap-3">
                            <div className="bg-amber-50 rounded-lg p-2 flex-shrink-0">
                                <Clock className="h-5 w-5 text-amber-600" />
                            </div>
                            <div className="min-w-0">
                                <h3 id="idle-timeout-title" className="text-base font-semibold text-gray-900">Still there?</h3>
                                <p id="idle-timeout-message" className="text-sm text-gray-600 mt-1">
                                    You've been inactive for a while. For security, you'll be signed out in{' '}
                                    <span className="font-semibold tabular-nums">{idleWarningSecondsLeft}s</span> unless you stay signed in.
                                </p>
                            </div>
                        </div>
                        <div className="px-6 py-4 flex justify-end gap-3 mt-2">
                            <button
                                type="button"
                                onClick={() => logout()}
                                className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
                            >
                                Log out now
                            </button>
                            <button
                                type="button"
                                onClick={() => resetIdleTimer()}
                                className="inline-flex items-center gap-2 px-4 py-2.5 rounded-[10px] text-sm font-semibold shadow-sm transition-colors text-white bg-blue-600 hover:bg-blue-700"
                            >
                                Stay signed in
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return context;
};
