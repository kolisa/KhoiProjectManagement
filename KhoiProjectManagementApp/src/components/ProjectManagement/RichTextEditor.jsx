import React, { useRef, useState, useEffect, useCallback } from 'react';
import { Bold, Italic, Underline, List, ListOrdered, Heading2, Link2, Eraser, SpellCheck, Undo2 } from 'lucide-react';
import { sanitizeHtml, toEditorHtml, tidyText, isEmptyHtml } from './richText';

const MAX_HTML = 20000;

const ToolButton = ({ label, onClick, disabled, children }) => (
  <button type="button" aria-label={label} title={label} disabled={disabled}
    onMouseDown={(e) => e.preventDefault()}   // keep the text selection while a toolbar button is clicked
    onClick={onClick}
    className="p-1.5 rounded text-gray-600 hover:bg-gray-100 disabled:text-gray-300 disabled:hover:bg-transparent disabled:cursor-not-allowed">
    {children}
  </button>
);

// Rich-text description editor: a small formatting toolbar over a contentEditable area, the browser's own spell-checker
// (red underlines while typing) and a "Tidy" action that fixes common spelling/spacing/capital-letter mistakes and says
// exactly what it changed, with Undo. Output is allow-list sanitised HTML (the server sanitises again on save).
// `value` is the stored description (HTML, or legacy plain text); it seeds the editor and is not re-applied while typing.
const RichTextEditor = ({ id, label, value, onChange, disabled = false, reason }) => {
  const ref = useRef(null);
  const [tidyReport, setTidyReport] = useState(null);   // { changes, before }
  const [linkOpen, setLinkOpen] = useState(false);
  const [linkUrl, setLinkUrl] = useState('');
  const [tooLong, setTooLong] = useState(false);

  // Seed (and re-seed when switching to a different item) - never while the user is typing.
  useEffect(() => {
    if (ref.current) ref.current.innerHTML = toEditorHtml(value);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [disabled]);

  const emit = useCallback(() => {
    const html = sanitizeHtml(ref.current?.innerHTML || '');
    setTooLong(html.length > MAX_HTML);
    onChange(isEmptyHtml(html) ? '' : html);
  }, [onChange]);

  const exec = (command, arg) => {
    ref.current?.focus();
    document.execCommand?.(command, false, arg);
    emit();
  };

  const tidy = () => {
    const before = ref.current.innerHTML;
    const { html, changes } = tidyText(before);
    if (changes.length === 0) { setTidyReport({ changes: [], before }); return; }
    ref.current.innerHTML = html;
    setTidyReport({ changes, before });
    emit();
  };

  const undoTidy = () => {
    if (!tidyReport) return;
    ref.current.innerHTML = tidyReport.before;
    setTidyReport(null);
    emit();
  };

  const applyLink = (e) => {
    e.preventDefault();
    const url = linkUrl.trim();
    if (/^(https?:\/\/|mailto:)/i.test(url)) {
      const sel = window.getSelection?.();
      // Nothing selected: createLink would insert nothing, so insert the address itself as the link text.
      if (!sel || sel.isCollapsed) exec('insertHTML', `<a href="${url.replace(/"/g, '&quot;')}">${url.replace(/&/g, '&amp;').replace(/</g, '&lt;')}</a>`);
      else exec('createLink', url);
    }
    setLinkOpen(false);
    setLinkUrl('');
  };

  // Paste as plain text: pasted web pages bring styles and markup nobody wants in a description.
  const onPaste = (e) => {
    e.preventDefault();
    document.execCommand?.('insertText', false, e.clipboardData.getData('text/plain'));
  };

  const labelId = `${id}-label`;

  return (
    <div>
      <div className="flex items-center justify-between mb-1">
        <span id={labelId} className="block text-sm font-medium text-gray-700">{label}</span>
        {!disabled && (
          <button type="button" onClick={tidy} className="inline-flex items-center gap-1 text-xs font-medium text-blue-700 hover:text-blue-800"
            title="Fix common spelling, spacing and capital-letter mistakes">
            <SpellCheck className="h-3.5 w-3.5" aria-hidden="true" /> Tidy spelling &amp; grammar
          </button>
        )}
      </div>

      <div className={`rounded-lg border ${disabled ? 'border-gray-200 bg-gray-50' : 'border-gray-300 focus-within:ring-2 focus-within:ring-blue-500'}`}
        title={disabled ? reason || undefined : undefined}>
        {!disabled && (
          <div role="toolbar" aria-label="Formatting" className="flex flex-wrap items-center gap-0.5 border-b border-gray-200 px-1.5 py-1">
            <ToolButton label="Bold" onClick={() => exec('bold')}><Bold className="h-4 w-4" /></ToolButton>
            <ToolButton label="Italic" onClick={() => exec('italic')}><Italic className="h-4 w-4" /></ToolButton>
            <ToolButton label="Underline" onClick={() => exec('underline')}><Underline className="h-4 w-4" /></ToolButton>
            <span className="mx-1 h-4 w-px bg-gray-200" aria-hidden="true" />
            <ToolButton label="Heading" onClick={() => exec('formatBlock', 'H2')}><Heading2 className="h-4 w-4" /></ToolButton>
            <ToolButton label="Bulleted list" onClick={() => exec('insertUnorderedList')}><List className="h-4 w-4" /></ToolButton>
            <ToolButton label="Numbered list" onClick={() => exec('insertOrderedList')}><ListOrdered className="h-4 w-4" /></ToolButton>
            <ToolButton label="Link" onClick={() => setLinkOpen((o) => !o)}><Link2 className="h-4 w-4" /></ToolButton>
            <ToolButton label="Clear formatting" onClick={() => { exec('removeFormat'); exec('formatBlock', 'P'); }}><Eraser className="h-4 w-4" /></ToolButton>
          </div>
        )}
        {linkOpen && !disabled && (
          <div className="flex items-center gap-2 border-b border-gray-200 px-2 py-1.5">
            <label htmlFor={`${id}-url`} className="sr-only">Link address</label>
            <input id={`${id}-url`} autoFocus value={linkUrl} onChange={(e) => setLinkUrl(e.target.value)} placeholder="https://…"
              onKeyDown={(e) => { if (e.key === 'Enter') applyLink(e); }}
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm" />
            <button type="button" onClick={applyLink} disabled={!/^(https?:\/\/|mailto:)/i.test(linkUrl.trim())}
              className="px-2 py-1 text-xs rounded bg-blue-600 text-white disabled:opacity-50">Add link</button>
          </div>
        )}
        <div
          ref={ref}
          id={id}
          role="textbox"
          aria-multiline="true"
          aria-labelledby={labelId}
          aria-disabled={disabled || undefined}
          contentEditable={!disabled}
          suppressContentEditableWarning
          spellCheck={!disabled}
          lang="en"
          onInput={() => { setTidyReport(null); emit(); }}
          onPaste={onPaste}
          className={`pm-richtext min-h-[8rem] max-h-72 overflow-y-auto px-3 py-2 text-sm text-gray-900 outline-none ${disabled ? 'text-gray-600 cursor-not-allowed' : ''}`}
        />
      </div>

      {tooLong && <p role="alert" className="mt-1 text-xs text-red-600">This description is too long to save. Shorten it a little.</p>}
      {tidyReport && (
        <p role="status" className="mt-1 text-xs text-gray-600">
          {tidyReport.changes.length === 0
            ? 'Nothing to fix - looks good.'
            : <>{tidyReport.changes.join(' · ')}. <button type="button" onClick={undoTidy} className="inline-flex items-center gap-0.5 font-medium text-blue-700 hover:underline"><Undo2 className="h-3 w-3" aria-hidden="true" />Undo</button></>}
        </p>
      )}
    </div>
  );
};

export default RichTextEditor;
