import React, { useState, useRef, useEffect, useMemo, useCallback } from 'react';
import { ChevronDown, Check, Search, X } from 'lucide-react';
import { Avatar } from './pmUi';

// An advanced select: a button that opens a searchable list. Options can carry an avatar (a person), a second line of
// detail and an "empty" choice (e.g. Unassigned). Full keyboard support (Arrow keys, Enter, Escape, type to search) and the
// ARIA combobox/listbox pattern, so it is as accessible as the native <select> it replaces. Associate a <label htmlFor={id}>.
//
// options: [{ value, label, description?, person? }]   (person: true shows an avatar from the label)
const Combobox = ({
  id, value, onChange, options, placeholder = 'Select…', emptyLabel, disabled = false, title,
  searchPlaceholder = 'Search…', clearable = false, className = '', size = 'md',
}) => {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const root = useRef(null);
  const input = useRef(null);
  const listId = `${id}-list`;

  // The "empty" choice (Unassigned / None) is just another option with value ''.
  const all = useMemo(() => (emptyLabel ? [{ value: '', label: emptyLabel, empty: true }, ...options] : options), [emptyLabel, options]);
  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return all;
    return all.filter((o) => `${o.label} ${o.description || ''}`.toLowerCase().includes(q));
  }, [all, query]);

  const selected = all.find((o) => String(o.value) === String(value ?? ''));

  const close = useCallback(() => { setOpen(false); setQuery(''); }, []);

  useEffect(() => {
    if (!open) return undefined;
    const onDown = (e) => { if (root.current && !root.current.contains(e.target)) close(); };
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [open, close]);

  useEffect(() => { if (open) input.current?.focus(); }, [open]);

  // Escape closes just this list. The surrounding modal listens for Escape natively, so this must be a native listener that
  // stops the event before it gets there (a React handler runs too late).
  useEffect(() => {
    const el = input.current;
    if (!open || !el) return undefined;
    const onEsc = (e) => { if (e.key === 'Escape') { e.stopPropagation(); close(); } };
    el.addEventListener('keydown', onEsc);
    return () => el.removeEventListener('keydown', onEsc);
  }, [open, close]);
  useEffect(() => { setActive(0); }, [query, open]);

  const choose = (opt) => { onChange(opt.value); close(); };

  const onSearchKey = (e) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(shown.length - 1, a + 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(0, a - 1)); }
    else if (e.key === 'Enter') { e.preventDefault(); if (shown[active]) choose(shown[active]); }
    else if (e.key === 'Tab') close();
  };

  const pad = size === 'sm' ? 'px-2 py-1 text-xs' : 'px-3 py-2 text-sm';

  return (
    <div ref={root} className={`relative ${className}`}>
      <button type="button" id={id} role="combobox" aria-haspopup="listbox" aria-expanded={open} aria-controls={open ? listId : undefined}
        disabled={disabled} title={title}
        onClick={() => setOpen((o) => !o)}
        onKeyDown={(e) => { if (!open && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) { e.preventDefault(); setOpen(true); } }}
        className={`flex w-full items-center gap-2 rounded-lg border border-gray-300 bg-white text-left focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:text-gray-500 disabled:cursor-not-allowed ${pad}`}>
        {selected?.person && !selected.empty && <Avatar name={selected.label} size="sm" />}
        <span className={`min-w-0 flex-1 truncate ${selected && !selected.empty ? 'text-gray-900' : 'text-gray-500'}`}>{selected ? selected.label : placeholder}</span>
        <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" aria-hidden="true" />
      </button>

      {open && (
        <div className="absolute left-0 right-0 z-50 mt-1 min-w-[12rem] rounded-lg border border-gray-200 bg-white shadow-lg">
          <div className="flex items-center gap-2 border-b border-gray-100 px-2.5 py-1.5">
            <Search className="h-3.5 w-3.5 text-gray-400" aria-hidden="true" />
            <input ref={input} value={query} onChange={(e) => setQuery(e.target.value)} onKeyDown={onSearchKey} placeholder={searchPlaceholder}
              aria-label="Search options" aria-controls={listId} aria-activedescendant={shown[active] ? `${id}-opt-${active}` : undefined}
              className="w-full bg-transparent text-sm outline-none placeholder:text-gray-400" />
            {query && <button type="button" aria-label="Clear search" onClick={() => setQuery('')} className="text-gray-400 hover:text-gray-600"><X className="h-3.5 w-3.5" /></button>}
          </div>
          <ul id={listId} role="listbox" className="max-h-60 overflow-y-auto py-1">
            {shown.length === 0 && <li className="px-3 py-2 text-sm text-gray-500" role="presentation">No matches</li>}
            {shown.map((o, i) => {
              const isSelected = String(o.value) === String(value ?? '');
              return (
                <li key={`${o.value}-${i}`} id={`${id}-opt-${i}`} role="option" aria-selected={isSelected}
                  onMouseDown={(e) => e.preventDefault()} onClick={() => choose(o)} onMouseEnter={() => setActive(i)}
                  className={`flex cursor-pointer items-center gap-2 px-3 py-1.5 text-sm ${i === active ? 'bg-blue-50' : ''} ${o.empty ? 'text-gray-600' : 'text-gray-900'}`}>
                  {o.person && !o.empty && <Avatar name={o.label} size="sm" />}
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">{o.label}</span>
                    {o.description && <span className="block truncate text-xs text-gray-500">{o.description}</span>}
                  </span>
                  {isSelected && <Check className="h-4 w-4 shrink-0 text-blue-600" aria-hidden="true" />}
                </li>
              );
            })}
          </ul>
          {clearable && value !== '' && value != null && (
            <button type="button" onClick={() => choose({ value: '' })} className="w-full border-t border-gray-100 px-3 py-1.5 text-left text-xs text-gray-600 hover:bg-gray-50">Clear selection</button>
          )}
        </div>
      )}
    </div>
  );
};

export const peopleOptions = (members) => members.map((m) => ({ value: m.userId, label: m.name, description: m.email, person: true }));

export default Combobox;
