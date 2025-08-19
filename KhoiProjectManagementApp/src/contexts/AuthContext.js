// src/contexts/AuthContext.js
import React, { createContext, useContext, useState, useEffect } from 'react';
import ApiService from '../services/ApiService';

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const token = localStorage.getItem('jwt_token');
        if (token) {
            try {
                const payload = JSON.parse(atob(token.split('.')[1]));
                if (payload.exp * 1000 > Date.now()) {
                    setUser({
                        id: parseInt(payload.nameid),
                        name: payload.unique_name,
                        email: payload.email,
                        role: payload.role
                    });
                } else {
                    localStorage.removeItem('jwt_token');
                }
            } catch (error) {
                console.error('Invalid token:', error);
                localStorage.removeItem('jwt_token');
            }
        }
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        const apiService = new ApiService();
        const response = await apiService.login(email, password);
        if (response?.user) {
            setUser(response.user);
            return response;
        }
        throw new Error('Login failed');
    };

    const logout = () => {
        setUser(null);
        localStorage.removeItem('jwt_token');
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