// src/components/Auth/AuthGuard.js
import React from 'react';
import { useAuth } from '../../contexts/AuthContext';
import LoginForm from './LoginForm';
import ProjectManagementSystem from '../Layout/ProjectManagementSystem';
import LoadingSpinner from '../Common/LoadingSpinner';

const AuthGuard = () => {
    const { user, loading } = useAuth();

    if (loading) {
        return (
            <div className="min-h-screen flex items-center justify-center">
                <LoadingSpinner text="Loading application..." />
            </div>
        );
    }

    return user ? <ProjectManagementSystem /> : <LoginForm />;
};

export default AuthGuard;