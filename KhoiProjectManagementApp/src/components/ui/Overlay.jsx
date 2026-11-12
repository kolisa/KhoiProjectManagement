// Modal, Drawer, Dropdown, Tooltip. Modal/Drawer reuse the app's shared useModalA11y (Escape, focus trap, restore).
import React, { useEffect, useId, useRef, useState } from 'react';
import useModalA11y from '../Common/useModalA11y';
import { cn } from './cn';

const MODAL_SIZES = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' };

export function Modal({ open, onClose, title, size = 'md', children, footer }) {
  const ref = useModalA11y(onClose);
  const titleId = useId();
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="modal-backdrop fixed inset-0 bg-black/40" onClick={onClose} />
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby={title ? titleId : undefined} tabIndex={-1}
        className={cn('relative w-full rounded-2xl border border-border bg-card shadow-xl focus:outline-hidden', MODAL_SIZES[size])}>
        {title && (
          <div className="flex items-center justify-between border-b border-border px-6 py-4">
            <h2 id={titleId} className="text-lg font-semibold">{title}</h2>
            <button type="button" onClick={onClose} aria-label="Close" className="rounded-sm p-1 text-muted-foreground hover:bg-muted">&times;</button>
          </div>
        )}
        <div className="px-6 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-border px-6 py-4">{footer}</div>}
      </div>
    </div>
  );
}

export function Drawer({ open, onClose, title, side = 'right', children }) {
  const ref = useModalA11y(onClose);
  const titleId = useId();
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="modal-backdrop fixed inset-0 bg-black/40" onClick={onClose} />
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby={title ? titleId : undefined} tabIndex={-1}
        className={cn('relative h-full w-80 max-w-[85%] overflow-y-auto border-border bg-card shadow-xl focus:outline-hidden', side === 'right' ? 'ml-auto border-l' : 'mr-auto border-r')}>
        {title && (
          <div className="flex items-center justify-between border-b border-border px-5 py-4">
            <h2 id={titleId} className="text-base font-semibold">{title}</h2>
            <button type="button" onClick={onClose} aria-label="Close" className="rounded-sm p-1 text-muted-foreground hover:bg-muted">&times;</button>
          </div>
        )}
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

// items: [{ key, label, onSelect, destructive?, disabled? }]
export function Dropdown({ trigger, items = [], align = 'right' }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  useEffect(() => {
    if (!open) return undefined;
    const onDown = (e) => { if (!rootRef.current?.contains(e.target)) setOpen(false); };
    const onKey = (e) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey); };
  }, [open]);
  return (
    <div ref={rootRef} className="relative inline-block">
      <span onClick={() => setOpen(!open)} aria-haspopup="menu" aria-expanded={open}>{trigger}</span>
      {open && (
        <div role="menu" className={cn('absolute z-50 mt-2 min-w-40 overflow-hidden rounded-xl border border-border bg-card py-1 shadow-lg', align === 'right' ? 'right-0' : 'left-0')}>
          {items.map((item) => (
            <button key={item.key} type="button" role="menuitem" disabled={item.disabled}
              onClick={() => { setOpen(false); item.onSelect?.(); }}
              className={cn('block w-full px-3 py-2 text-left text-sm hover:bg-muted disabled:opacity-50', item.destructive && 'text-red-600 hover:bg-red-50')}>
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export function Tooltip({ text, children }) {
  const id = useId();
  return (
    <span className="group relative inline-flex">
      {React.cloneElement(children, { 'aria-describedby': id })}
      <span id={id} role="tooltip"
        className="pointer-events-none absolute bottom-full left-1/2 z-50 mb-1.5 -translate-x-1/2 whitespace-nowrap rounded-md bg-gray-900 px-2 py-1 text-xs text-white opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100">
        {text}
      </span>
    </span>
  );
}
