// Collapse and Accordion.
import React, { useId, useState } from 'react';
import { cn } from './cn';

export function Collapse({ title, defaultOpen = false, children, className }) {
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  return (
    <div className={cn('rounded-lg border border-border bg-card', className)}>
      <button type="button" aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-medium">
        {title}
        <span aria-hidden="true" className={cn('transition-transform', open && 'rotate-180')}>⌄</span>
      </button>
      {open && <div id={id} className="border-t border-border px-4 py-3 text-sm">{children}</div>}
    </div>
  );
}

// items: [{ key, title, content }]. Only one open at a time unless multiple is set.
export function Accordion({ items = [], multiple = false, defaultOpen = [] }) {
  const [openKeys, setOpenKeys] = useState(defaultOpen);
  const toggle = (key) => setOpenKeys((cur) => {
    if (cur.includes(key)) return cur.filter((k) => k !== key);
    return multiple ? [...cur, key] : [key];
  });
  return (
    <div className="divide-y divide-border rounded-lg border border-border bg-card">
      {items.map((item) => {
        const open = openKeys.includes(item.key);
        return (
          <div key={item.key}>
            <button type="button" aria-expanded={open} onClick={() => toggle(item.key)}
              className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-medium hover:bg-muted">
              {item.title}
              <span aria-hidden="true" className={cn('transition-transform', open && 'rotate-180')}>⌄</span>
            </button>
            {open && <div className="px-4 pb-4 text-sm text-muted-foreground">{item.content}</div>}
          </div>
        );
      })}
    </div>
  );
}
