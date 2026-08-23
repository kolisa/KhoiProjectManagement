// src/components/Layout/Navigation.js
import React from 'react';
import { useAuth } from '../../contexts/AuthContext';

const Navigation = ({ activeTab, setActiveTab, mobileMenuOpen, setMobileMenuOpen }) => {
  const { logout } = useAuth();

  const tabs = ['dashboard', 'projects', 'tasks', 'team', 'reports'];

  const handleTabClick = (tab) => {
    setActiveTab(tab);
    setMobileMenuOpen(false);
  };

  return (
    <>
      {/* Mobile Navigation */}
      {mobileMenuOpen && (
        <div className="md:hidden bg-white border-b">
          <div className="px-4 py-2 space-y-1">
            {tabs.map((tab) => (
              <button
                key={tab}
                onClick={() => handleTabClick(tab)}
                className={`block w-full text-left px-3 py-2 rounded-md text-sm font-medium capitalize ${
                  activeTab === tab
                    ? 'bg-blue-100 text-blue-700'
                    : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
                }`}
              >
                {tab}
              </button>
            ))}
            <button
              onClick={logout}
              className="block w-full text-left px-3 py-2 rounded-md text-sm font-medium text-red-600 hover:text-red-900 hover:bg-red-50"
            >
              Logout
            </button>
          </div>
        </div>
      )}

      {/* Desktop Navigation */}
      <nav className="bg-white border-b hidden md:block">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex space-x-8">
            {tabs.map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={`py-4 px-1 border-b-2 font-medium text-sm capitalize ${
                  activeTab === tab
                    ? 'border-blue-500 text-blue-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                {tab}
              </button>
            ))}
          </div>
        </div>
      </nav>
    </>
  );
};

export default Navigation;