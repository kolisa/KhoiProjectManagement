// src/components/Settings/AuditLog.js
// Admin-only visibility into what the system has done behind the scenes: emails it sent (previously
// write-only - EmailLog was logged on every send but nothing ever read it back) and application error
// logs (Serilog's rolling-daily text files). One Settings section, two internal views (plain local
// tab state, no routing) - gated by audit.view, mirroring PermissionsManagement/GroupsManagement's
// Settings-section look.
import React, { useState, useEffect } from 'react';
import { ClipboardList, Search } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import StatusBadge from '../Common/StatusBadge';

const EMAIL_STATUS_COLORS = {
  Pending: 'bg-amber-50 text-amber-700',
  Sent: 'bg-green-50 text-green-700',
  Failed: 'bg-red-50 text-red-700',
};

const SentEmailsView = ({ apiService }) => {
  const toast = useToast();
  const [logs, setLogs] = useState(null);
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');

  const load = async () => {
    try {
      const result = await apiService.getEmailAuditLog({
        status: statusFilter === 'all' ? undefined : statusFilter,
        toEmailContains: search.trim() || undefined,
      });
      setLogs(result || []);
    } catch (err) {
      reportApiError(toast, err, 'Could not load the email audit log.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  };

  useEffect(() => {
    const debounce = setTimeout(load, 300);
    return () => clearTimeout(debounce);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, search]);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <div className="relative flex-1 max-w-xs">
          <Search className="h-3.5 w-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search recipient..."
            className="w-full border border-gray-300 rounded-md pl-8 pr-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        >
          <option value="all">All statuses</option>
          <option value="Pending">Pending</option>
          <option value="Sent">Sent</option>
          <option value="Failed">Failed</option>
        </select>
      </div>

      {logs === null ? (
        <div className="text-sm text-gray-400">Loading...</div>
      ) : logs.length === 0 ? (
        <div className="text-sm text-gray-400 italic p-4 text-center">No emails match.</div>
      ) : (
        <div className="border border-gray-100 rounded-2xl overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="text-left px-3 py-2 font-medium">To</th>
                <th className="text-left px-3 py-2 font-medium">Subject</th>
                <th className="text-left px-3 py-2 font-medium">Type</th>
                <th className="text-left px-3 py-2 font-medium">Sent At</th>
                <th className="text-left px-3 py-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {logs.map((log) => (
                <tr key={log.id}>
                  <td className="px-3 py-2 text-gray-900 truncate max-w-[180px]" title={log.toEmail}>{log.toEmail}</td>
                  <td className="px-3 py-2 text-gray-700 truncate max-w-[220px]" title={log.subject}>{log.subject}</td>
                  <td className="px-3 py-2 text-gray-500">{log.emailType}</td>
                  <td className="px-3 py-2 text-gray-500 whitespace-nowrap">{new Date(log.sentAt).toLocaleString()}</td>
                  <td className="px-3 py-2">
                    <span title={log.status === 'Failed' ? (log.errorMessage || '') : undefined}>
                      <StatusBadge status={log.status} colorMap={EMAIL_STATUS_COLORS} />
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

const LEVEL_COLORS = {
  FTL: 'text-red-700',
  ERR: 'text-red-600',
  WRN: 'text-amber-600',
};

const ErrorLogsView = ({ apiService }) => {
  const toast = useToast();
  const [dates, setDates] = useState(null);
  const [selectedDate, setSelectedDate] = useState('');
  const [levelFilter, setLevelFilter] = useState('');
  const [entries, setEntries] = useState(null);

  useEffect(() => {
    const loadDates = async () => {
      try {
        const result = await apiService.getErrorLogDates();
        setDates(result || []);
        if (result?.length > 0) setSelectedDate(result[0]);
      } catch (err) {
        reportApiError(toast, err, 'Could not load available log dates.');
        setDates([]);
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    };
    loadDates();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!selectedDate) return;
    const load = async () => {
      try {
        const result = await apiService.getErrorLogs({ date: selectedDate, level: levelFilter || undefined });
        setEntries(result || []);
      } catch (err) {
        reportApiError(toast, err, 'Could not load this log file.');
        setEntries([]);
      }
    };
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedDate, levelFilter]);

  if (dates === null) {
    return <div className="text-sm text-gray-400">Loading...</div>;
  }

  if (dates.length === 0) {
    return <div className="text-sm text-gray-400 italic p-4 text-center">No log files found.</div>;
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <select
          value={selectedDate}
          onChange={(e) => setSelectedDate(e.target.value)}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        >
          {dates.map((d) => <option key={d} value={d}>{d}</option>)}
        </select>
        <select
          value={levelFilter}
          onChange={(e) => setLevelFilter(e.target.value)}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        >
          <option value="">All levels</option>
          <option value="Warning">Warning</option>
          <option value="Error">Error</option>
        </select>
      </div>

      {entries === null ? (
        <div className="text-sm text-gray-400">Loading...</div>
      ) : entries.length === 0 ? (
        <div className="text-sm text-gray-400 italic p-4 text-center">No matching entries for this day.</div>
      ) : (
        <div className="bg-gray-900 rounded-2xl p-4 max-h-[28rem] overflow-y-auto font-mono text-xs space-y-2">
          {entries.map((entry, i) => (
            <div key={i} className="whitespace-pre-wrap break-all">
              <span className="text-gray-500">{entry.timestamp ? new Date(entry.timestamp).toLocaleString() : ''}</span>{' '}
              <span className={`font-semibold ${LEVEL_COLORS[entry.level] || 'text-gray-300'}`}>[{entry.level}]</span>{' '}
              <span className="text-gray-200">{entry.message}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

const LOGIN_STATUS_COLORS = {
  Success: 'bg-green-50 text-green-700',
  Failed: 'bg-red-50 text-red-700',
};

const LoginsView = ({ apiService }) => {
  const toast = useToast();
  const [logs, setLogs] = useState(null);
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');

  const load = async () => {
    try {
      const result = await apiService.getLoginAuditLog({
        success: statusFilter === 'all' ? undefined : statusFilter === 'success',
        emailContains: search.trim() || undefined,
      });
      setLogs(result || []);
    } catch (err) {
      reportApiError(toast, err, 'Could not load the login audit log.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  };

  useEffect(() => {
    const debounce = setTimeout(load, 300);
    return () => clearTimeout(debounce);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, search]);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <div className="relative flex-1 max-w-xs">
          <Search className="h-3.5 w-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search email..."
            className="w-full border border-gray-300 rounded-md pl-8 pr-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        >
          <option value="all">All attempts</option>
          <option value="success">Success only</option>
          <option value="failed">Failed only</option>
        </select>
      </div>

      {logs === null ? (
        <div className="text-sm text-gray-400">Loading...</div>
      ) : logs.length === 0 ? (
        <div className="text-sm text-gray-400 italic p-4 text-center">No login attempts match.</div>
      ) : (
        <div className="border border-gray-100 rounded-2xl overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="text-left px-3 py-2 font-medium">Email</th>
                <th className="text-left px-3 py-2 font-medium">Status</th>
                <th className="text-left px-3 py-2 font-medium">Failure Reason</th>
                <th className="text-left px-3 py-2 font-medium">IP Address</th>
                <th className="text-left px-3 py-2 font-medium">Timestamp</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {logs.map((log) => (
                <tr key={log.id}>
                  <td className="px-3 py-2 text-gray-900 truncate max-w-[220px]" title={log.emailAttempted}>{log.emailAttempted}</td>
                  <td className="px-3 py-2">
                    <StatusBadge status={log.success ? 'Success' : 'Failed'} colorMap={LOGIN_STATUS_COLORS} />
                  </td>
                  <td className="px-3 py-2 text-gray-500">{log.failureReason || '—'}</td>
                  <td className="px-3 py-2 text-gray-500 font-mono">{log.ipAddress || '—'}</td>
                  <td className="px-3 py-2 text-gray-500 whitespace-nowrap">{new Date(log.timestamp).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

// Matches the tab keys in App.jsx's nav groups (activeTab) - this app has no router, so a tab switch
// is the closest equivalent of a "page visit".
const TAB_LABELS = {
  dashboard: 'Dashboard',
  reminders: 'Reminders',
  calendar: 'Calendar',
  projects: 'Projects',
  tasks: 'Tasks',
  timesheets: 'Timesheets',
  team: 'Team',
  vault: 'Vault',
  wiki: 'Wiki',
  library: 'Library',
  ideas: 'Ideas',
  finance: 'Finance',
  reports: 'Reports',
  settings: 'Settings',
};

const PageVisitsView = ({ apiService }) => {
  const toast = useToast();
  const [visits, setVisits] = useState(null);

  const load = async () => {
    try {
      const result = await apiService.getPageVisitLog({});
      setVisits(result || []);
    } catch (err) {
      reportApiError(toast, err, 'Could not load the page visit log.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="space-y-3">
      {visits === null ? (
        <div className="text-sm text-gray-400">Loading...</div>
      ) : visits.length === 0 ? (
        <div className="text-sm text-gray-400 italic p-4 text-center">No page visits recorded yet.</div>
      ) : (
        <div className="border border-gray-100 rounded-2xl overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="text-left px-3 py-2 font-medium">User</th>
                <th className="text-left px-3 py-2 font-medium">Page</th>
                <th className="text-left px-3 py-2 font-medium">Timestamp</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {visits.map((visit) => (
                <tr key={visit.id}>
                  <td className="px-3 py-2 text-gray-900">{visit.userName}</td>
                  <td className="px-3 py-2 text-gray-700">{TAB_LABELS[visit.tabKey] || visit.tabKey}</td>
                  <td className="px-3 py-2 text-gray-500 whitespace-nowrap">{new Date(visit.timestamp).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

const VIEWS = [
  { key: 'emails', label: 'Sent Emails' },
  { key: 'errors', label: 'Error Logs' },
  { key: 'logins', label: 'Logins' },
  { key: 'pageVisits', label: 'Page Visits' },
];

const AuditLog = ({ apiService }) => {
  const [view, setView] = useState('emails');

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 flex items-center">
          <ClipboardList className="h-5 w-5 mr-2 text-gray-700" />
          Audit
        </h3>
        <p className="text-sm text-gray-500">Sent-email history, application error logs, login attempts, and page visits.</p>
      </div>

      <div className="flex gap-1 border-b border-gray-100">
        {VIEWS.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setView(tab.key)}
            className={`px-3.5 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              view === tab.key ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {view === 'emails' && <SentEmailsView apiService={apiService} />}
      {view === 'errors' && <ErrorLogsView apiService={apiService} />}
      {view === 'logins' && <LoginsView apiService={apiService} />}
      {view === 'pageVisits' && <PageVisitsView apiService={apiService} />}
    </div>
  );
};

export default AuditLog;
