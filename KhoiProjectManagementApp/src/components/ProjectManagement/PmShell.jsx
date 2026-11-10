import React, { useMemo } from 'react';
import { LogOut } from 'lucide-react';
import ApiService from '../../services/ApiService';
import { useAuth } from '../../contexts/AuthContext';
import PmWorkspace from './PmWorkspace';
import PmNotifications from './PmNotifications';

// The whole UI for a module-only account: a minimal header and the PM workspace - no sidebar, no
// other modules to navigate to. (The API enforces the same confinement; this is just the matching UX.)
const PmShell = () => {
  const { user, logout } = useAuth();
  const apiService = useMemo(() => new ApiService(), []);

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-blue-800 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="h-8 w-8 rounded-lg bg-white flex items-center justify-center text-blue-700 font-bold">K</div>
            <span className="font-bold tracking-tight">KhoiHub · Projects</span>
          </div>
          <div className="flex items-center gap-4 text-sm">
            <PmNotifications apiService={apiService} />
            <span className="hidden sm:inline text-blue-100">{user?.name}</span>
            <button onClick={() => logout()} className="inline-flex items-center gap-1.5 text-blue-100 hover:text-white">
              <LogOut className="h-4 w-4" aria-hidden="true" /> Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
        <PmWorkspace apiService={apiService} user={user} />
      </main>
    </div>
  );
};

export default PmShell;
