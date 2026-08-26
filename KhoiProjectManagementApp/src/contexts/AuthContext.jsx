// src/contexts/AuthContext.js
import React, { createContext, useContext, useState, useEffect } from 'react';
import ApiService, { onSessionExpired, getStoredToken } from '../services/ApiService';
import { useToast } from './ToastContext';

const AuthContext = createContext();

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

    const logout = async () => {
        const apiService = new ApiService();
        setUser(null);
        // An explicit, deliberate logout always starts fresh at the dashboard next time - only an
        // auto-logout (session expiry, handled separately in ApiService's 401 handler) preserves the
        // last-active tab for restoration after the user logs back in.
        localStorage.removeItem('khoi_last_tab');
        await apiService.logout();
    };

    const value = {
        user,
        login,
        logout,
        loading
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return context;
};
