import React, { useState, useEffect, useCallback, useRef } from 'react';
import { MessageSquare, ListChecks, Link2, Paperclip, Trash2, Pencil, Download, X, History } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { STATUS_STYLES, TYPE_STYLES, statusLabel } from './pmConstants';

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';
const btnPrimary = 'px-3 py-1.5 text-sm rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-60';
const btnGhost = 'px-3 py-1.5 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50';

const escapeRegExp = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

// Plain "@Full Name" text (the same convention the wiki/ideas comments use) - the names that match a
// project member are highlighted, longest first so a short name never splits a longer one.
const MentionText = ({ text, members }) => {
  const names = members.map((m) => m.name).filter(Boolean).sort((a, b) => b.length - a.length);
  if (names.length === 0) return <>{text}</>;
  const re = new RegExp(`(@(?:${names.map(escapeRegExp).join('|')}))`, 'gi');
  return text.split(re).map((part, i) => (i % 2 === 1
    ? <span key={i} className="font-medium text-blue-700 bg-blue-50 rounded px-0.5">{part}</span>
    : <React.Fragment key={i}>{part}</React.Fragment>));
};

const formatSize = (bytes) => (bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);
const formatWhen = (iso) => new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });

// ---------------------------------------------------------------- comments

const CommentsPanel = ({ apiService, itemId, members, canComment, onCount }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [comments, setComments] = useState(null);
  const [draft, setDraft] = useState('');
  const [editingId, setEditingId] = useState(null);
  const [editText, setEditText] = useState('');
  const [busy, setBusy] = useState(false);
  const inputRef = useRef(null);

  const load = useCallback(async () => {
    try {
      const list = await apiService.getWorkItemComments(itemId);
      setComments(list);
      onCount(list.length);
    } catch (err) {
      reportApiError(toast, err, 'Could not load comments.');
      setComments([]);
    }
  }, [apiService, itemId, onCount, toast]);

  useEffect(() => { load(); }, [load]);

  const mention = (name) => {
    setDraft((d) => `${d}${d && !d.endsWith(' ') ? ' ' : ''}@${name} `);
    inputRef.current?.focus();
  };

  const post = async (e) => {
    e.preventDefault();
    if (!draft.trim()) return;
    setBusy(true);
    try {
      await apiService.addWorkItemComment(itemId, draft.trim());
      setDraft('');
      await load();
    } catch (err) {
      reportApiError(toast, err, 'Could not post the comment.');
    } finally {
      setBusy(false);
    }
  };

  const saveEdit = async (id) => {
    if (!editText.trim()) return;
    setBusy(true);
    try {
      await apiService.editWorkItemComment(id, editText.trim());
      setEditingId(null);
      await load();
    } catch (err) {
      reportApiError(toast, err, 'Could not save the comment.');
    } finally {
      setBusy(false);
    }
  };

  const remove = async (c) => {
    if (!(await confirm('Delete this comment?', { title: 'Delete comment', confirmText: 'Delete', danger: true }))) return;
    try {
      await apiService.deleteWorkItemComment(c.id);
      await load();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete the comment.');
    }
  };

  if (comments === null) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div className="space-y-4">
      {comments.length === 0 && <p className="text-sm text-gray-500">No comments yet.</p>}
      <ul className="space-y-3">
        {comments.map((c) => (
          <li key={c.id} className="bg-gray-50 rounded-lg px-3 py-2.5">
            <div className="flex items-center justify-between gap-2 text-xs text-gray-500">
              <span><span className="font-semibold text-gray-800">{c.authorName}</span> · {formatWhen(c.createdAt)}{c.editedAt && ' · edited'}</span>
              <span className="flex gap-2">
                {c.canEdit && editingId !== c.id && (
                  <button aria-label="Edit comment" onClick={() => { setEditingId(c.id); setEditText(c.body); }} className="text-gray-400 hover:text-blue-600"><Pencil className="h-3.5 w-3.5" /></button>
                )}
                {c.canDelete && (
                  <button aria-label="Delete comment" onClick={() => remove(c)} className="text-gray-400 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>
                )}
              </span>
            </div>
            {editingId === c.id ? (
              <div className="mt-2 space-y-2">
                <textarea aria-label="Edit comment" rows={3} maxLength={4000} className={fieldClass} value={editText} onChange={(e) => setEditText(e.target.value)} />
                <div className="flex gap-2">
                  <button type="button" disabled={busy} onClick={() => saveEdit(c.id)} className={btnPrimary}>Save</button>
                  <button type="button" onClick={() => setEditingId(null)} className={btnGhost}>Cancel</button>
                </div>
              </div>
            ) : (
              <p className="mt-1 text-sm text-gray-800 whitespace-pre-wrap break-words"><MentionText text={c.body} members={members} /></p>
            )}
          </li>
        ))}
      </ul>

      {canComment ? (
        <form onSubmit={post} className="space-y-2">
          <label htmlFor="pm-comment" className="sr-only">Add a comment</label>
          <textarea id="pm-comment" ref={inputRef} rows={3} maxLength={4000} className={fieldClass} value={draft}
            onChange={(e) => setDraft(e.target.value)} placeholder="Write a comment… type @Name to mention a teammate" />
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-xs text-gray-500">Mention:</span>
            {members.map((m) => (
              <button key={m.userId} type="button" onClick={() => mention(m.name)}
                className="text-xs px-2 py-0.5 rounded-full border border-gray-200 text-gray-600 hover:bg-blue-50 hover:text-blue-700">@{m.name}</button>
            ))}
            <button type="submit" disabled={busy || !draft.trim()} className={`${btnPrimary} ml-auto`}>Comment</button>
          </div>
        </form>
      ) : (
        <p className="text-xs text-gray-500">Your role can read this discussion but not post to it.</p>
      )}
    </div>
  );
};

// ---------------------------------------------------------------- checklist

const ChecklistPanel = ({ apiService, itemId, canEdit, onCount }) => {
  const toast = useToast();
  const [items, setItems] = useState(null);
  const [text, setText] = useState('');

  const load = useCallback(async () => {
    try {
      const list = await apiService.getWorkItemChecklist(itemId);
      setItems(list);
      onCount(`${list.filter((i) => i.isDone).length}/${list.length}`);
    } catch (err) {
      reportApiError(toast, err, 'Could not load the checklist.');
      setItems([]);
    }
  }, [apiService, itemId, onCount, toast]);

  useEffect(() => { load(); }, [load]);

  const run = async (fn, fallback) => {
    try { await fn(); await load(); } catch (err) { reportApiError(toast, err, fallback); }
  };

  const add = (e) => {
    e.preventDefault();
    if (!text.trim()) return;
    run(async () => { await apiService.addChecklistItem(itemId, text.trim()); setText(''); }, 'Could not add the item.');
  };

  if (items === null) return <p className="text-sm text-gray-500">Loading…</p>;
  const done = items.filter((i) => i.isDone).length;
  const pct = items.length ? Math.round((done / items.length) * 100) : 0;

  return (
    <div className="space-y-3">
      {items.length > 0 && (
        <div>
          <div className="flex justify-between text-xs text-gray-600 mb-1"><span>{done} of {items.length} done</span><span>{pct}%</span></div>
          <div className="h-1.5 bg-gray-200 rounded-full overflow-hidden" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Checklist progress">
            <div className="h-full bg-green-500" style={{ width: `${pct}%` }} />
          </div>
        </div>
      )}
      {items.length === 0 && <p className="text-sm text-gray-500">No checklist items yet.</p>}
      <ul className="space-y-1">
        {items.map((i) => (
          <li key={i.id} className="flex items-center gap-2 group">
            <input id={`cl-${i.id}`} type="checkbox" checked={i.isDone} disabled={!canEdit}
              onChange={(e) => run(() => apiService.updateChecklistItem(i.id, i.text, e.target.checked), 'Could not update the item.')}
              className="h-4 w-4 rounded border-gray-300 text-blue-600" />
            <label htmlFor={`cl-${i.id}`} className={`flex-1 text-sm break-words ${i.isDone ? 'line-through text-gray-400' : 'text-gray-800'}`}>{i.text}</label>
            {canEdit && (
              <button aria-label={`Remove ${i.text}`} onClick={() => run(() => apiService.deleteChecklistItem(i.id), 'Could not remove the item.')}
                className="text-gray-300 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>
            )}
          </li>
        ))}
      </ul>
      {canEdit && (
        <form onSubmit={add} className="flex gap-2">
          <label htmlFor="pm-checklist-new" className="sr-only">New checklist item</label>
          <input id="pm-checklist-new" className={fieldClass} value={text} maxLength={300} onChange={(e) => setText(e.target.value)} placeholder="Add an item…" />
          <button type="submit" disabled={!text.trim()} className={btnPrimary}>Add</button>
        </form>
      )}
    </div>
  );
};

// ---------------------------------------------------------------- links

const LINK_TYPES = [
  { key: 'Blocks', label: 'Blocks' },
  { key: 'Relates', label: 'Relates to' },
  { key: 'Duplicates', label: 'Duplicates' },
];

const LinksPanel = ({ apiService, item, items, canEdit, onCount, active }) => {
  const toast = useToast();
  const [links, setLinks] = useState(null);
  // The picker offers every item in the project (Epics, Features and other sprints included), not just
  // whatever list the opener happened to have on screen - the board, for one, only holds sprint cards.
  const [projectItems, setProjectItems] = useState(null);

  // Fetched only once the Links tab is opened, and as a light id/title list rather than the full backlog.
  useEffect(() => {
    if (!active || !canEdit || projectItems !== null) return undefined;
    let cancelled = false;
    apiService.getWorkItemLookup(item.projectId)
      .then((list) => { if (!cancelled) setProjectItems(list); })
      .catch(() => { /* the opener's own list stays as the fallback */ });
    return () => { cancelled = true; };
  }, [active, canEdit, projectItems, apiService, item.projectId]);
  const [type, setType] = useState('Relates');
  const [targetId, setTargetId] = useState('');

  const load = useCallback(async () => {
    try {
      const list = await apiService.getWorkItemLinks(item.id);
      setLinks(list);
      onCount(list.length);
    } catch (err) {
      reportApiError(toast, err, 'Could not load links.');
      setLinks([]);
    }
  }, [apiService, item.id, onCount, toast]);

  useEffect(() => { load(); }, [load]);

  const add = async (e) => {
    e.preventDefault();
    if (!targetId) return;
    try {
      await apiService.addWorkItemLink(item.id, Number(targetId), type);
      setTargetId('');
      await load();
    } catch (err) {
      reportApiError(toast, err, 'Could not add the link.');
    }
  };

  const remove = async (id) => {
    try { await apiService.deleteWorkItemLink(id); await load(); } catch (err) { reportApiError(toast, err, 'Could not remove the link.'); }
  };

  if (links === null) return <p className="text-sm text-gray-500">Loading…</p>;
  const linked = new Set(links.map((l) => l.otherId));
  const candidates = (projectItems || items).filter((i) => i.id !== item.id && !linked.has(i.id));
  const openBlockers = links.filter((l) => l.relation === 'Is blocked by' && l.otherStatus !== 'completed');

  return (
    <div className="space-y-3">
      {openBlockers.length > 0 && (
        <div role="status" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
          Blocked by {openBlockers.length} unfinished item{openBlockers.length === 1 ? '' : 's'}.
        </div>
      )}
      {links.length === 0 && <p className="text-sm text-gray-500">No linked items.</p>}
      <ul className="space-y-1.5">
        {links.map((l) => (
          <li key={l.id} className="flex items-center gap-2 text-sm bg-gray-50 rounded-lg px-3 py-2">
            <span className="text-xs font-medium text-gray-500 w-24 flex-shrink-0">{l.relation}</span>
            <span className={`px-1.5 py-0.5 rounded text-[10.5px] font-semibold ${TYPE_STYLES[l.otherType] || 'bg-gray-100 text-gray-700'}`}>{l.otherType}</span>
            <span className="flex-1 truncate text-gray-800" title={l.otherTitle}>#{l.otherId} {l.otherTitle}</span>
            <span className={`px-2 py-0.5 rounded-full text-[11px] font-medium ${STATUS_STYLES[l.otherStatus] || 'bg-gray-100 text-gray-700'}`}>{statusLabel(l.otherStatus)}</span>
            {canEdit && <button aria-label={`Remove link to ${l.otherTitle}`} onClick={() => remove(l.id)} className="text-gray-300 hover:text-red-600"><X className="h-4 w-4" /></button>}
          </li>
        ))}
      </ul>
      {canEdit && (
        <form onSubmit={add} className="flex flex-wrap gap-2">
          <select aria-label="Link type" value={type} onChange={(e) => setType(e.target.value)} className="border border-gray-300 rounded-lg px-2 py-2 text-sm bg-white">
            {LINK_TYPES.map((t) => <option key={t.key} value={t.key}>{t.label}</option>)}
          </select>
          <select aria-label="Item to link" value={targetId} onChange={(e) => setTargetId(e.target.value)} className="flex-1 min-w-[10rem] border border-gray-300 rounded-lg px-2 py-2 text-sm bg-white">
            <option value="">Select an item…</option>
            {candidates.map((i) => <option key={i.id} value={i.id}>#{i.id} {i.title}</option>)}
          </select>
          <button type="submit" disabled={!targetId} className={btnPrimary}>Link</button>
        </form>
      )}
    </div>
  );
};

// ---------------------------------------------------------------- attachments

// Thumbnails are fetched at full size, so only small images get one; bigger ones show the file icon.
const THUMB_MAX_BYTES = 2 * 1024 * 1024;

const ImageThumb = ({ apiService, attachment }) => {
  const [src, setSrc] = useState(null);
  useEffect(() => {
    let url = null;
    let cancelled = false;
    apiService.getWorkItemAttachmentObjectUrl(attachment.id)
      .then((u) => { if (cancelled) window.URL.revokeObjectURL(u); else { url = u; setSrc(u); } })
      .catch(() => {});
    return () => { cancelled = true; if (url) window.URL.revokeObjectURL(url); };
  }, [apiService, attachment.id]);
  if (!src) return <div className="h-12 w-12 rounded bg-gray-200 flex-shrink-0" aria-hidden="true" />;
  return <img src={src} alt={attachment.fileName} className="h-12 w-12 rounded object-cover flex-shrink-0" />;
};

const AttachmentsPanel = ({ apiService, itemId, canUpload, onCount, active }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [files, setFiles] = useState(null);
  const [uploading, setUploading] = useState(false);
  const inputRef = useRef(null);

  const load = useCallback(async () => {
    try {
      const list = await apiService.getWorkItemAttachments(itemId);
      setFiles(list);
      onCount(list.length);
    } catch (err) {
      reportApiError(toast, err, 'Could not load attachments.');
      setFiles([]);
    }
  }, [apiService, itemId, onCount, toast]);

  useEffect(() => { load(); }, [load]);

  const upload = async (e) => {
    const picked = Array.from(e.target.files || []);
    e.target.value = '';
    if (picked.length === 0) return;
    setUploading(true);
    for (const file of picked) {
      try {
        await apiService.uploadWorkItemAttachment(itemId, file);
      } catch (err) {
        reportApiError(toast, err, `Could not upload ${file.name}.`);
      }
    }
    setUploading(false);
    await load();
  };

  const remove = async (f) => {
    if (!(await confirm(`Delete ${f.fileName}?`, { title: 'Delete file', confirmText: 'Delete', danger: true }))) return;
    try { await apiService.deleteWorkItemAttachment(f.id); await load(); } catch (err) { reportApiError(toast, err, 'Could not delete the file.'); }
  };

  const download = async (f) => {
    try { await apiService.downloadWorkItemAttachment(f.id, f.fileName); } catch (err) { reportApiError(toast, err, 'Could not download the file.'); }
  };

  if (files === null) return <p className="text-sm text-gray-500">Loading…</p>;

  return (
    <div className="space-y-3">
      {files.length === 0 && <p className="text-sm text-gray-500">No files attached.</p>}
      <ul className="space-y-1.5">
        {files.map((f) => (
          <li key={f.id} className="flex items-center gap-3 bg-gray-50 rounded-lg px-3 py-2">
            {f.isImage && active && f.fileSize <= THUMB_MAX_BYTES ? <ImageThumb apiService={apiService} attachment={f} /> : <Paperclip className="h-5 w-5 text-gray-400 flex-shrink-0" aria-hidden="true" />}
            <div className="flex-1 min-w-0">
              <div className="text-sm text-gray-800 truncate" title={f.fileName}>{f.fileName}</div>
              <div className="text-xs text-gray-500">{formatSize(f.fileSize)} · {f.uploadedBy} · {formatWhen(f.uploadedAt)}</div>
            </div>
            <button aria-label={`Download ${f.fileName}`} onClick={() => download(f)} className="text-gray-400 hover:text-blue-600"><Download className="h-4 w-4" /></button>
            {f.canDelete && <button aria-label={`Delete ${f.fileName}`} onClick={() => remove(f)} className="text-gray-400 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>}
          </li>
        ))}
      </ul>
      {canUpload && (
        <div>
          <input ref={inputRef} type="file" multiple className="sr-only" id="pm-attach" onChange={upload} />
          <label htmlFor="pm-attach" className={`${btnGhost} inline-flex items-center gap-1.5 cursor-pointer ${uploading ? 'opacity-60 pointer-events-none' : ''}`}>
            <Paperclip className="h-4 w-4" aria-hidden="true" /> {uploading ? 'Uploading…' : 'Attach files'}
          </label>
          <span className="ml-2 text-xs text-gray-500">Images, PDF, Office documents, up to 10 MB each.</span>
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------- activity

const ActivityPanel = ({ apiService, itemId, active }) => {
  const toast = useToast();
  const [entries, setEntries] = useState(null);

  // Loaded the first time the tab is opened, not with the modal.
  useEffect(() => {
    if (!active || entries !== null) return undefined;
    let cancelled = false;
    apiService.getWorkItemActivity(itemId)
      .then((list) => { if (!cancelled) setEntries(list); })
      .catch((err) => { if (!cancelled) { reportApiError(toast, err, 'Could not load the activity.'); setEntries([]); } });
    return () => { cancelled = true; };
  }, [active, entries, apiService, itemId, toast]);

  if (entries === null) return <p className="text-sm text-gray-500">{active ? 'Loading…' : ''}</p>;
  if (entries.length === 0) return <p className="text-sm text-gray-500">No activity recorded yet.</p>;
  return (
    <ol className="space-y-2">
      {entries.map((e, i) => (
        <li key={`${e.when}-${i}`} className="flex gap-2 text-sm">
          <span className={`mt-1.5 h-2 w-2 rounded-full flex-shrink-0 ${e.kind === 'status' ? 'bg-blue-500' : 'bg-gray-300'}`} aria-hidden="true" />
          <span className="flex-1 text-gray-800"><span className="font-medium">{e.actorName}</span> {e.text}</span>
          <span className="text-xs text-gray-500 whitespace-nowrap">{formatWhen(e.when)}</span>
        </li>
      ))}
    </ol>
  );
};

// ---------------------------------------------------------------- labels

// Labels are saved straight away (not with the form's Save): each change replaces the item's whole label set.
const LabelsEditor = ({ apiService, item, canEdit, onChanged }) => {
  const toast = useToast();
  const [labels, setLabels] = useState(item.labels || []);
  const [known, setKnown] = useState([]);
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!canEdit) return;
    apiService.getProjectLabels(item.projectId).then(setKnown).catch(() => setKnown([]));
  }, [apiService, item.projectId, canEdit]);

  const save = async (names) => {
    setBusy(true);
    try {
      setLabels(await apiService.setWorkItemLabels(item.id, names));
      onChanged?.();
    } catch (err) {
      reportApiError(toast, err, 'Could not update the labels.');
    } finally {
      setBusy(false);
    }
  };

  const add = (e) => {
    e.preventDefault();
    const name = draft.trim();
    if (!name || labels.some((l) => l.name.toLowerCase() === name.toLowerCase())) { setDraft(''); return; }
    setDraft('');
    save([...labels.map((l) => l.name), name]);
  };

  return (
    <div className="px-5 pb-3">
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-sm font-medium text-gray-700 mr-1">Labels</span>
        {labels.length === 0 && !canEdit && <span className="text-sm text-gray-500">None</span>}
        {labels.map((l) => (
          <span key={l.id} className="inline-flex items-center gap-1 text-xs font-medium rounded-full pl-2 pr-1 py-0.5 bg-gray-100 text-gray-700">
            <span className="h-1.5 w-1.5 rounded-full" style={{ background: l.color }} aria-hidden="true" />
            {l.name}
            {canEdit && (
              <button type="button" disabled={busy} aria-label={`Remove label ${l.name}`} onClick={() => save(labels.filter((x) => x.id !== l.id).map((x) => x.name))}
                className="text-gray-400 hover:text-red-600 disabled:opacity-50"><X className="h-3 w-3" /></button>
            )}
          </span>
        ))}
        {canEdit && (
          <form onSubmit={add} className="inline-flex">
            <label htmlFor="pm-label-input" className="sr-only">Add a label</label>
            <input id="pm-label-input" list="pm-label-options" value={draft} onChange={(e) => setDraft(e.target.value)} maxLength={30} disabled={busy}
              placeholder="Add label…" className="border border-gray-300 rounded-full px-2.5 py-0.5 text-xs w-28 focus:outline-none focus:ring-2 focus:ring-blue-500" />
            <datalist id="pm-label-options">{known.map((k) => <option key={k.id} value={k.name} />)}</datalist>
          </form>
        )}
      </div>
    </div>
  );
};

// ---------------------------------------------------------------- container

const TABS = [
  { key: 'comments', label: 'Comments', Icon: MessageSquare },
  { key: 'checklist', label: 'Checklist', Icon: ListChecks },
  { key: 'links', label: 'Links', Icon: Link2 },
  { key: 'files', label: 'Files', Icon: Paperclip },
  { key: 'activity', label: 'History', Icon: History },
];

// Discussion, checklist, dependencies and files for an existing work item. `can` carries the caller's
// project-role abilities (mirrors the server matrix; the API is still the authority).
const WorkItemCollab = ({ apiService, item, items, members, can: abilities, onChanged }) => {
  const [tab, setTab] = useState('comments');
  const [counts, setCounts] = useState({});
  const counter = (key) => (value) => setCounts((c) => (c[key] === value ? c : { ...c, [key]: value }));
  // Stable identities: each panel's load() depends on its onCount callback.
  const onComments = useCallback(counter('comments'), []); // eslint-disable-line react-hooks/exhaustive-deps
  const onChecklist = useCallback(counter('checklist'), []); // eslint-disable-line react-hooks/exhaustive-deps
  const onLinks = useCallback(counter('links'), []); // eslint-disable-line react-hooks/exhaustive-deps
  const onFiles = useCallback(counter('files'), []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
    <div className="border-t border-gray-200 pt-3"><LabelsEditor apiService={apiService} item={item} canEdit={abilities.edit} onChanged={onChanged} /></div>
    <section aria-label="Collaboration" className="border-t border-gray-200 px-5 py-4">
      <div role="tablist" aria-label="Work item collaboration" className="flex gap-1 mb-4 overflow-x-auto">
        {TABS.map(({ key, label, Icon }) => (
          <button key={key} role="tab" id={`collab-tab-${key}`} aria-selected={tab === key} aria-controls={`collab-panel-${key}`}
            onClick={() => setTab(key)}
            className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg whitespace-nowrap ${tab === key ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-600 hover:bg-gray-100'}`}>
            <Icon className="h-4 w-4" aria-hidden="true" /> {label}
            {counts[key] !== undefined && counts[key] !== 0 && <span className="text-xs text-gray-500">({counts[key]})</span>}
          </button>
        ))}
      </div>

      {/* All four stay mounted so their counts load up front; inactive ones are just hidden. */}
      <div role="tabpanel" id="collab-panel-comments" aria-labelledby="collab-tab-comments" hidden={tab !== 'comments'}>
        <CommentsPanel apiService={apiService} itemId={item.id} members={members} canComment={abilities.collaborate} onCount={onComments} />
      </div>
      <div role="tabpanel" id="collab-panel-checklist" aria-labelledby="collab-tab-checklist" hidden={tab !== 'checklist'}>
        <ChecklistPanel apiService={apiService} itemId={item.id} canEdit={abilities.edit} onCount={onChecklist} />
      </div>
      <div role="tabpanel" id="collab-panel-links" aria-labelledby="collab-tab-links" hidden={tab !== 'links'}>
        <LinksPanel apiService={apiService} item={item} items={items} canEdit={abilities.edit} onCount={onLinks} active={tab === 'links'} />
      </div>
      <div role="tabpanel" id="collab-panel-activity" aria-labelledby="collab-tab-activity" hidden={tab !== 'activity'}>
        <ActivityPanel apiService={apiService} itemId={item.id} active={tab === 'activity'} />
      </div>
      <div role="tabpanel" id="collab-panel-files" aria-labelledby="collab-tab-files" hidden={tab !== 'files'}>
        <AttachmentsPanel apiService={apiService} itemId={item.id} canUpload={abilities.collaborate} onCount={onFiles} active={tab === 'files'} />
      </div>
    </section>
    </>
  );
};

export default WorkItemCollab;
