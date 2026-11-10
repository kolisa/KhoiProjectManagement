import React, { useState, useEffect, useCallback } from 'react';
import { Download } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { CumulativeFlowChart, ThroughputChart } from './PmCharts';

const RANGES = [
  { days: 14, label: '14 days' },
  { days: 30, label: '30 days' },
  { days: 90, label: '90 days' },
];

const fmtDays = (v) => (v == null ? '–' : `${v} d`);

const Card = ({ title, hint, children }) => (
  <section className="bg-white border border-gray-200 rounded-xl p-4">
    <h3 className="text-sm font-semibold text-gray-900">{title}</h3>
    {hint && <p className="text-xs text-gray-500 mb-3">{hint}</p>}
    {children}
  </section>
);

const Stat = ({ label, stats }) => (
  <div className="bg-gray-50 rounded-lg px-3 py-2.5">
    <div className="text-xs text-gray-500">{label}</div>
    <div className="text-lg font-semibold text-gray-900">{fmtDays(stats.average)}<span className="text-xs font-normal text-gray-500"> avg</span></div>
    <div className="text-xs text-gray-600">median {fmtDays(stats.median)} · 85% within {fmtDays(stats.p85)}</div>
    <div className="text-[11px] text-gray-400">{stats.samples} item{stats.samples === 1 ? '' : 's'}</div>
  </div>
);

// Delivery analytics for a project: cumulative flow, lead/cycle time and throughput, and workload per
// person. Everything is derived from the same status history the board writes - nothing is entered here.
const PmReports = ({ apiService, project }) => {
  const toast = useToast();
  const [days, setDays] = useState(30);
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    try {
      setError(null);
      const [flow, metrics, workload] = await Promise.all([
        apiService.getCumulativeFlow(project.id, days),
        apiService.getFlowMetrics(project.id, days),
        apiService.getWorkloadReport(project.id),
      ]);
      setData({ flow, metrics, workload });
    } catch (err) {
      setError(err?.message || 'Could not load the reports.');
    }
  }, [apiService, project.id, days]);

  useEffect(() => { setData(null); load(); }, [load]);

  const exportCsv = async () => {
    setExporting(true);
    try {
      await apiService.exportWorkItemsCsv(project.id);
    } catch (err) {
      reportApiError(toast, err, 'Could not export the work items.');
    } finally {
      setExporting(false);
    }
  };

  if (error) return <ErrorMessage message={error} onRetry={load} />;

  const maxPoints = data ? Math.max(1, ...data.workload.map((w) => w.openPoints)) : 1;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <div role="group" aria-label="Report period" className="inline-flex rounded-lg border border-gray-300 overflow-hidden">
          {RANGES.map((r) => (
            <button key={r.days} aria-pressed={days === r.days} onClick={() => setDays(r.days)}
              className={`px-3 py-1.5 text-sm ${days === r.days ? 'bg-blue-600 text-white' : 'bg-white text-gray-700 hover:bg-gray-50'}`}>{r.label}</button>
          ))}
        </div>
        <button onClick={exportCsv} disabled={exporting}
          className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-60">
          <Download className="h-4 w-4" aria-hidden="true" /> {exporting ? 'Exporting…' : 'Export work items (CSV)'}
        </button>
      </div>

      {!data ? <LoadingSpinner /> : (
        <>
          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="Cumulative flow" hint="Items in each status at the end of each day. A widening band is a bottleneck.">
              <CumulativeFlowChart flow={data.flow} />
            </Card>
            <Card title="Throughput" hint={`${data.metrics.completedCount} item${data.metrics.completedCount === 1 ? '' : 's'} (${data.metrics.completedPoints} pts) finished in the last ${days} days.`}>
              {data.metrics.completedCount === 0
                ? <p className="text-sm text-gray-500 py-8 text-center">Nothing has been finished in this period.</p>
                : <ThroughputChart weeks={data.metrics.weeks} />}
            </Card>
          </div>

          <Card title="Lead time and cycle time" hint="Lead time runs from creation to done; cycle time from when work started to done. Items finished in the period.">
            <div className="grid gap-3 sm:grid-cols-2">
              <Stat label="Lead time" stats={data.metrics.leadTime} />
              <Stat label="Cycle time" stats={data.metrics.cycleTime} />
            </div>
          </Card>

          <Card title="Workload" hint="Open work per person (Epics and Features excluded). Points bar is relative to the busiest person.">
            {data.workload.length === 0 ? <p className="text-sm text-gray-500">No work items yet.</p> : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-xs text-gray-500 border-b border-gray-200">
                      <th className="py-2 pr-3 font-medium">Person</th>
                      <th className="py-2 pr-3 font-medium">Open</th>
                      <th className="py-2 pr-3 font-medium w-48">Open points</th>
                      <th className="py-2 pr-3 font-medium">In progress</th>
                      <th className="py-2 pr-3 font-medium">Blocked</th>
                      <th className="py-2 pr-3 font-medium">Overdue</th>
                      <th className="py-2 font-medium">Done (30 d)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.workload.map((w) => (
                      <tr key={w.userId ?? 'none'} className="border-b border-gray-100">
                        <td className="py-2 pr-3 text-gray-900">{w.name}</td>
                        <td className="py-2 pr-3">{w.openItems}</td>
                        <td className="py-2 pr-3">
                          <div className="flex items-center gap-2">
                            <div className="h-2 flex-1 bg-gray-100 rounded-full overflow-hidden" aria-hidden="true">
                              <div className="h-full bg-blue-500" style={{ width: `${(w.openPoints / maxPoints) * 100}%` }} />
                            </div>
                            <span className="w-8 text-right tabular-nums">{w.openPoints}</span>
                          </div>
                        </td>
                        <td className="py-2 pr-3">{w.inProgress}</td>
                        <td className={`py-2 pr-3 ${w.blocked ? 'text-red-600 font-medium' : ''}`}>{w.blocked}</td>
                        <td className={`py-2 pr-3 ${w.overdue ? 'text-red-600 font-medium' : ''}`}>{w.overdue}</td>
                        <td className="py-2">{w.doneLast30Days}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}
    </div>
  );
};

export default PmReports;
