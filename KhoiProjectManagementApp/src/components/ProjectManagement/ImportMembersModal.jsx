import React, { useState } from 'react';
import { X, Download, Upload } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { PROJECT_ROLES, roleLabel } from './pmConstants';
import { parseMemberRows, MEMBER_TEMPLATE_CSV, downloadText, OUTCOME_STYLES, OUTCOME_LABELS } from './pmImport';

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';

// Bulk add / re-role people on a project from a pasted or uploaded list. Nothing changes until the person
// has seen a preview: the first step is always a dry run that says, row by row, what would happen.
const ImportMembersModal = ({ apiService, project, otherProjects = [], canGrantAccess, onImported, onClose }) => {
  const ref = useModalA11y(onClose);
  const toast = useToast();
  const [text, setText] = useState('');
  const [defaultRole, setDefaultRole] = useState('Developer');
  const [grant, setGrant] = useState(false);
  const [moduleOnly, setModuleOnly] = useState(false);
  const [preview, setPreview] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const rows = parseMemberRows(text).map((r) => ({ ...r, role: r.role || defaultRole }));

  const request = (dryRun) => ({ rows, grantModuleAccess: grant, moduleOnly: grant && moduleOnly, dryRun });

  const edit = (value) => { setText(value); setPreview(null); setError(null); };

  const loadFile = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    try { edit(await file.text()); } catch { setError('Could not read that file.'); }
  };

  const copyFrom = async (e) => {
    const id = Number(e.target.value);
    e.target.value = '';
    if (!id) return;
    try {
      const members = await apiService.getPmMembers(id);
      edit(members.map((m) => `${m.email},${m.role}`).join('\n'));
    } catch (err) {
      reportApiError(toast, err, 'Could not read that project\'s members.');
    }
  };

  const run = async (dryRun) => {
    setBusy(true);
    setError(null);
    try {
      const result = await apiService.importPmMembers(project.id, request(dryRun));
      if (dryRun) {
        setPreview(result);
      } else {
        toast.success(`Import done: ${result.added} added, ${result.updated} updated.`);
        onImported?.(result);
        onClose();
      }
    } catch (err) {
      setError(err?.message || 'The import failed.');
    } finally {
      setBusy(false);
    }
  };

  const changes = preview ? preview.added + preview.updated : 0;

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby="import-members-title" tabIndex={-1}
        className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 id="import-members-title" className="text-lg font-semibold text-gray-900">Import members into {project.name}</h2>
          <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600"><X className="h-5 w-5" /></button>
        </div>

        <div className="p-5 space-y-4">
          {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}

          <p className="text-sm text-gray-600">
            One person per line: <code className="bg-gray-100 px-1 rounded">email, role</code>. Roles can be written naturally
            (Scrum Master, Dev, QA, Viewer…). People are matched to <strong>existing accounts</strong> by email; this never creates accounts.
          </p>

          <div className="flex flex-wrap items-center gap-2">
            <label className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50 cursor-pointer">
              <Upload className="h-4 w-4" aria-hidden="true" /> Upload CSV
              <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" onChange={loadFile} aria-label="Upload a CSV file" />
            </label>
            <button type="button" onClick={() => downloadText('project-members-template.csv', MEMBER_TEMPLATE_CSV)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50">
              <Download className="h-4 w-4" aria-hidden="true" /> Template
            </button>
            {otherProjects.length > 0 && (
              <select aria-label="Copy members from another project" defaultValue="" onChange={copyFrom} className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm bg-white">
                <option value="">Copy from a project…</option>
                {otherProjects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            )}
          </div>

          <div>
            <label htmlFor="import-text" className="block text-sm font-medium text-gray-700 mb-1">People</label>
            <textarea id="import-text" rows={7} className={`${fieldClass} font-mono`} value={text} onChange={(e) => edit(e.target.value)}
              placeholder={'dana@company.com, Scrum Master\nsam@company.com, Developer\npat@company.com'} />
            <p className="text-xs text-gray-500 mt-1">{rows.length} row{rows.length === 1 ? '' : 's'} read.</p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="import-default-role" className="block text-sm font-medium text-gray-700 mb-1">Role when a line has none</label>
              <select id="import-default-role" className={fieldClass} value={defaultRole} onChange={(e) => { setDefaultRole(e.target.value); setPreview(null); }}>
                {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
              </select>
            </div>
            {canGrantAccess && (
              <fieldset className="text-sm">
                <legend className="font-medium text-gray-700 mb-1">Module access</legend>
                <label className="flex items-center gap-2 text-gray-700">
                  <input type="checkbox" checked={grant} onChange={(e) => { setGrant(e.target.checked); setPreview(null); }} className="h-4 w-4 rounded border-gray-300" />
                  Also enable Project Management access for people who lack it
                </label>
                <label className={`flex items-center gap-2 mt-1 ml-6 ${grant ? 'text-gray-700' : 'text-gray-400'}`}>
                  <input type="checkbox" disabled={!grant} checked={moduleOnly} onChange={(e) => { setModuleOnly(e.target.checked); setPreview(null); }} className="h-4 w-4 rounded border-gray-300" />
                  Confine them to this module only <span className="text-xs text-gray-500">(only accounts with no other permissions)</span>
                </label>
              </fieldset>
            )}
          </div>

          {preview && (
            <section aria-label="Import preview">
              <p className="text-sm text-gray-800 mb-2" aria-live="polite">
                <strong>{preview.added}</strong> to add, <strong>{preview.updated}</strong> to change, <strong>{preview.skipped}</strong> skipped
                {preview.moduleAccessGranted > 0 && <>, <strong>{preview.moduleAccessGranted}</strong> will get module access</>}.
              </p>
              <div className="border border-gray-200 rounded-lg overflow-x-auto max-h-64 overflow-y-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500">
                    <tr><th className="text-left px-3 py-2 w-10">#</th><th className="text-left px-3 py-2">Email</th><th className="text-left px-3 py-2">Role</th><th className="text-left px-3 py-2">Result</th></tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((r) => (
                      <tr key={r.row} className="border-t border-gray-100">
                        <td className="px-3 py-1.5 text-gray-500">{r.row}</td>
                        <td className="px-3 py-1.5 text-gray-900 break-all">{r.email}</td>
                        <td className="px-3 py-1.5 text-gray-700">{r.role ? roleLabel(r.role) : '–'}</td>
                        <td className="px-3 py-1.5">
                          <span className={`inline-block text-[11px] font-semibold rounded px-1.5 py-0.5 ${OUTCOME_STYLES[r.outcome] || 'bg-gray-100 text-gray-700'}`}>{OUTCOME_LABELS[r.outcome] || r.outcome}</span>
                          <span className="block text-xs text-gray-500">{r.message}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          <div className="flex justify-end gap-2 pt-1">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50">Cancel</button>
            <button type="button" disabled={busy || rows.length === 0} onClick={() => run(true)}
              className="px-4 py-2 text-sm rounded-lg border border-blue-600 text-blue-700 font-medium hover:bg-blue-50 disabled:opacity-50">
              {busy && !preview ? 'Checking…' : 'Preview'}
            </button>
            <button type="button" disabled={busy || !preview || changes === 0} onClick={() => run(false)}
              className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-50">
              {busy && preview ? 'Importing…' : `Import ${changes || ''}`.trim()}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ImportMembersModal;
