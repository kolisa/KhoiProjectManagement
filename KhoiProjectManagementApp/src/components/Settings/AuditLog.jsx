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
        <div className="border border-gray-100 rounded-xl overflow-x-auto">
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
                    {log.status === 'Pending' && (
                      <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-amber-50 text-amber-700">Pending</span>
                    )}
                    {log.status === 'Sent' && (
                      <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-green-50 text-green-700">Sent</span>
                    )}
                    {log.status === 'Failed' && (
                      <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-red-50 text-red-700" title={log.errorMessage || ''}>Failed</span>
                    )}
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
        <div className="bg-gray-900 rounded-xl p-4 max-h-[28rem] overflow-y-auto font-mono text-xs space-y-2">
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

const AuditLog = ({ apiService }) => {
  const [view, setView] = useState('emails');

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 flex items-center">
          <ClipboardList className="h-5 w-5 mr-2 text-gray-700" />
          Audit
        </h3>
        <p className="text-sm text-gray-500">Sent-email history and application error logs.</p>
      </div>

      <div className="flex gap-1 border-b border-gray-100">
        {[{ key: 'emails', label: 'Sent Emails' }, { key: 'errors', label: 'Error Logs' }].map((tab) => (
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

      {view === 'emails' ? <SentEmailsView apiService={apiService} /> : <ErrorLogsView apiService={apiService} />}
    </div>
  );
};

export default AuditLog;
