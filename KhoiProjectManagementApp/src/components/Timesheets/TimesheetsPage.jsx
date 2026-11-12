// src/components/Timesheets/TimesheetsPage.jsx
import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Clock, Plus, Upload } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { Modal } from '../ui/Overlay';
import StatusBadge from '../Common/StatusBadge';
import TimesheetDetail from './TimesheetDetail';
import { formatDuration } from './duration';
import { parseTimesheetCsv } from './timesheetImport';
import { Button, Card, Input, Label } from '../ui';

const STATUS_COLORS = {
  Submitted: 'bg-[#EEEEFF] text-[#4131B0]',
  Approved: 'bg-[#E3F8E9] text-[#005F2E]',
  Rejected: 'bg-[#FFEBE8] text-[#B71824]',
};

// startOf/endOfWeek default the New Timesheet period to the current Mon-Sun week, matching the most
// common real-world timesheet cadence - still freely editable before creating.
const defaultPeriod = () => {
  const today = new Date();
  const dayIndex = (today.getDay() + 6) % 7; // Monday = 0
  const monday = new Date(today);
  monday.setDate(today.getDate() - dayIndex);
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  const iso = (d) => d.toISOString().slice(0, 10);
  return { periodStart: iso(monday), periodEnd: iso(sunday) };
};

const TimesheetsPage = ({ apiService, user }) => {
  const toast = useToast();
  const canApprove = hasPermission(user?.permissions, 'timesheets.approve');
  const [view, setView] = useState('mine'); // 'mine' | 'approvals'
  const [timesheets, setTimesheets] = useState(null);
  const [projects, setProjects] = useState([]);
  const [error, setError] = useState(null);
  const [selected, setSelected] = useState(null); // a TimesheetDto, or 'new', or null
  const [newPeriod, setNewPeriod] = useState(defaultPeriod);
  // Set only when 'new' was reached via Upload rather than the New Timesheet modal - pre-fills
  // TimesheetDetail's grid instead of starting empty. Cleared whenever the detail view closes so a
  // later plain "New Timesheet" doesn't accidentally reuse a stale upload.
  const [uploadedDraft, setUploadedDraft] = useState(null);
  const fileInputRef = useRef(null);

  const [showNew, setShowNew] = useState(false);
  const closeNewModal = () => {
    setShowNew(false);
    setNewPeriod(defaultPeriod());
  };

  const handleUploadClick = () => fileInputRef.current?.click();

  const handleFileSelected = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = ''; // reset so re-selecting the same file re-fires onChange
    if (!file) return;

    try {
      const text = await file.text();
      const { periodStart, periodEnd, entries, warnings } = parseTimesheetCsv(text, projects);
      if (warnings.length > 0) {
        toast.info(`Imported ${entries.length} row${entries.length === 1 ? '' : 's'}, ${warnings.length} skipped or flagged - review before saving.`);
      }
      setUploadedDraft({
        periodStart: periodStart || defaultPeriod().periodStart,
        periodEnd: periodEnd || defaultPeriod().periodEnd,
        entries,
      });
      setSelected('new');
    } catch (err) {
      toast.error(err.message || "Couldn't read that file as a timesheet.");
    }
  };

  const load = useCallback(async () => {
    try {
      const result = view === 'approvals'
        ? await apiService.getTimesheets(undefined, 'Submitted')
        : await apiService.getTimesheets(user?.id);
      setTimesheets(result || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view, user?.id]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    apiService.getProjects().then((list) => setProjects(list || [])).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleCreateNew = (e) => {
    e.preventDefault();
    setShowNew(false);
    setUploadedDraft(null);
    setSelected('new');
  };

  const closeDetail = () => {
    setSelected(null);
    setUploadedDraft(null);
  };

  if (selected) {
    return (
      <TimesheetDetail
        apiService={apiService}
        user={user}
        timesheet={selected === 'new' ? null : selected}
        initialPeriod={selected === 'new' ? (uploadedDraft ? { periodStart: uploadedDraft.periodStart, periodEnd: uploadedDraft.periodEnd } : newPeriod) : undefined}
        initialEntries={selected === 'new' ? uploadedDraft?.entries : undefined}
        projects={projects}
        onClose={closeDetail}
        onChanged={load}
      />
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
            <Clock className="h-7 w-7 mr-2 text-gray-700" />
            Timesheets
          </h2>
          <p className="text-gray-600">Log your hours per period and submit them for approval</p>
        </div>
        <div className="flex items-center gap-2.5">
          <input ref={fileInputRef} type="file" accept=".csv" onChange={handleFileSelected} className="hidden" />
          <Button variant="outline" onClick={handleUploadClick}>
            <Upload className="h-4 w-4" />
            Upload Timesheet
          </Button>
          <Button onClick={() => setShowNew(true)}>
            <Plus className="h-4 w-4" />
            New Timesheet
          </Button>
        </div>
      </div>

      {canApprove && (
        <div className="flex items-center gap-1.5">
          {[{ key: 'mine', label: 'My Timesheets' }, { key: 'approvals', label: 'Approvals' }].map(({ key, label }) => (
            <button
              key={key}
              onClick={() => { setTimesheets(null); setView(key); }}
              className={`px-3 py-1.5 rounded-lg text-sm font-semibold transition-colors ${
                view === key ? 'bg-blue-50 text-blue-700' : 'text-gray-500 hover:bg-gray-100'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      )}

      {error && <div className="text-red-600 text-sm bg-red-50 border border-red-200 rounded-lg p-3">Error: {error}</div>}

      <Card className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                {view === 'approvals' && <th className="text-left px-4 py-2.5 font-medium">Submitted by</th>}
                <th className="text-left px-4 py-2.5 font-medium">Period</th>
                <th className="text-left px-4 py-2.5 font-medium">Total Hours</th>
                <th className="text-left px-4 py-2.5 font-medium">Status</th>
                <th className="text-left px-4 py-2.5 font-medium"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {timesheets === null ? (
                <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-400">Loading...</td></tr>
              ) : timesheets.length === 0 ? (
                <tr><td colSpan={5} className="px-4 py-10 text-center text-gray-400">
                  {view === 'approvals' ? 'Nothing pending approval.' : 'No timesheets yet.'}
                </td></tr>
              ) : (
                timesheets.map((t) => (
                  <tr key={t.id} onClick={() => setSelected(t)} className="hover:bg-gray-50/60 transition-colors cursor-pointer">
                    {view === 'approvals' && <td className="px-4 py-3 text-gray-900">{t.userName}</td>}
                    <td className="px-4 py-3 text-gray-900 whitespace-nowrap">
                      {new Date(t.periodStart).toLocaleDateString()} - {new Date(t.periodEnd).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-3 text-gray-900">{formatDuration(t.totalHours)}</td>
                    <td className="px-4 py-3"><StatusBadge status={t.status} colorMap={STATUS_COLORS} /></td>
                    <td className="px-4 py-3 text-right text-blue-600 font-medium">View</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {showNew && (
        <Modal onClose={closeNewModal} title="New Timesheet" titleId="new-timesheet-title" showClose={false} closeOnBackdrop={false} size="sm" padded={false}>
            <form onSubmit={handleCreateNew}>
              <div className="px-6 py-5 space-y-4">
                <div>
                  <Label htmlFor="period-start">Period start</Label>
                  <Input id="period-start" type="date" required value={newPeriod.periodStart}
                    onChange={(e) => setNewPeriod({ ...newPeriod, periodStart: e.target.value })} />
                </div>
                <div>
                  <Label htmlFor="period-end">Period end</Label>
                  <Input id="period-end" type="date" required value={newPeriod.periodEnd}
                    onChange={(e) => setNewPeriod({ ...newPeriod, periodEnd: e.target.value })} />
                </div>
              </div>
              <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
                <Button variant="outline" type="button" onClick={closeNewModal}>
                  Cancel
                </Button>
                <Button type="submit">
                  Continue
                </Button>
              </div>
            </form>
        </Modal>
      )}
    </div>
  );
};

export default TimesheetsPage;
