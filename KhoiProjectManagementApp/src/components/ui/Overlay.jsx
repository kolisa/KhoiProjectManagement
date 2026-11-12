// Modal, Drawer, Dropdown, Tooltip. Modal/Drawer reuse the app's shared useModalA11y (Escape, focus trap, restore).
import React, { useEffect, useId, useRef, useState } from 'react';
import useModalA11y from '../Common/useModalA11y';
import { cn } from './cn';

const MODAL_SIZES = { sm: 'max-w-sm', md: 'max-w-md', lg: 'max-w-lg', xl: 'max-w-2xl', '2xl': 'max-w-4xl' };

// Props:
//  open            render nothing when false (default true, so conditionally-rendered modals can omit it)
//  onClose         Escape / close button / (optionally) backdrop click
//  closeOnBackdrop default true; pass false for forms so a stray click can't discard unsaved input
//  title           default header text; the <h2> gets `titleId` so aria-labelledby stays stable
//  titleId         explicit id for the title (existing tests/aria rely on fixed ids); generated if omitted
//  ariaLabel       use instead of title when there is no visible heading
//  header          replaces the default header entirely (render your own element carrying `titleId`)
//  showClose       default true; the X button in the default header
//  footer          rendered in a bordered, right-aligned footer bar
//  padded          default true; false renders children straight into the panel (custom layouts)
//  scroll          default true: panel gets max-h-[90vh] overflow-y-auto. Pass false when `className`
//                  supplies its own flex/overflow layout (cn does not merge conflicting classes).
//  describedBy     id of the element describing the dialog (aria-describedby)
//  zIndex          stacking class for the overlay (default 'z-50'); confirm/idle dialogs sit above other modals
//  bare            no panel chrome (bg/border/radius/shadow) for content that draws its own card
//  className       extra classes on the panel (layout only; avoid re-declaring width/overflow)
export function Modal({
  open = true, onClose, title, titleId: titleIdProp, ariaLabel, describedBy, zIndex = 'z-50', bare = false, header, showClose = true, size = 'md',
  closeOnBackdrop = true, padded = true, scroll = true, className, children, footer,
}) {
  const ref = useModalA11y(onClose);
  const generatedId = useId();
  const titleId = titleIdProp || generatedId;
  if (!open) return null;
  const labelled = !ariaLabel && (title || header);
  return (
    <div className={cn('fixed inset-0 flex items-center justify-center p-4', zIndex)}>
      <div className="modal-backdrop fixed inset-0 bg-black/40" onClick={closeOnBackdrop ? onClose : undefined} />
      <div ref={ref} role="dialog" aria-modal="true" aria-label={ariaLabel} aria-labelledby={labelled ? titleId : undefined} aria-describedby={describedBy} tabIndex={-1}
        className={cn('relative w-full focus:outline-hidden', !bare && 'rounded-2xl border border-border bg-card shadow-xl', MODAL_SIZES[size], scroll && 'max-h-[90vh] overflow-y-auto', className)}>
        {header}
        {!header && title && (
          <div className="flex items-center justify-between border-b border-border px-6 py-4">
            <h2 id={titleId} className="text-lg font-semibold">{title}</h2>
            {showClose && <button type="button" onClick={onClose} aria-label="Close" className="rounded-sm p-1 text-muted-foreground hover:bg-muted">&times;</button>}
          </div>
        )}
        {padded ? <div className="px-6 py-4">{children}</div> : children}
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
