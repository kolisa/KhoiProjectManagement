// Breadcrumb, Pagination, Tabs, Stepper.
import React, { useState } from 'react';
import { cn } from './cn';

export function Breadcrumb({ items = [] }) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex flex-wrap items-center gap-1.5 text-sm text-muted-foreground">
        {items.map((item, i) => {
          const last = i === items.length - 1;
          return (
            <li key={`${item.label}-${i}`} className="inline-flex items-center gap-1.5 not-last:after:text-muted-foreground not-last:after:content-['/']">
              {item.onClick && !last
                ? <button type="button" onClick={item.onClick} className="hover:text-foreground">{item.label}</button>
                : <span aria-current={last ? 'page' : undefined} className={cn(last && 'font-medium text-foreground')}>{item.label}</span>}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

export function Pagination({ page, pageCount, onChange }) {
  if (pageCount <= 1) return null;
  const btn = 'h-8 min-w-8 rounded-lg px-2 text-sm font-medium transition-colors disabled:opacity-50';
  const pages = Array.from({ length: pageCount }, (_, i) => i + 1)
    .filter((p) => p === 1 || p === pageCount || Math.abs(p - page) <= 1);
  return (
    <nav aria-label="Pagination" className="flex items-center gap-1">
      <button type="button" className={cn(btn, 'hover:bg-muted')} disabled={page <= 1} onClick={() => onChange(page - 1)}>Previous</button>
      {pages.map((p, i) => (
        <React.Fragment key={p}>
          {i > 0 && p - pages[i - 1] > 1 && <span aria-hidden="true" className="px-1 text-muted-foreground">…</span>}
          <button type="button" aria-current={p === page ? 'page' : undefined} onClick={() => onChange(p)}
            className={cn(btn, p === page ? 'bg-blue-600 text-white' : 'hover:bg-muted')}>{p}</button>
        </React.Fragment>
      ))}
      <button type="button" className={cn(btn, 'hover:bg-muted')} disabled={page >= pageCount} onClick={() => onChange(page + 1)}>Next</button>
    </nav>
  );
}

// tabs: [{ key, label, content? }]. Controlled via value/onChange, or uncontrolled. A tab without `content` renders
// no panel, so Tabs can act as pure navigation (the caller renders the page itself). variant: 'pill' | 'underline'.
export function Tabs({ tabs = [], value, onChange, defaultValue, variant = 'pill', ariaLabel, className }) {
  const [inner, setInner] = useState(defaultValue ?? tabs[0]?.key);
  const active = value ?? inner;
  const select = (key) => { setInner(key); onChange?.(key); };
  const onKeyDown = (e) => {
    const i = tabs.findIndex((t) => t.key === active);
    const next = e.key === 'ArrowRight' ? i + 1 : e.key === 'ArrowLeft' ? i - 1 : null;
    if (next === null) return;
    e.preventDefault();
    select(tabs[(next + tabs.length) % tabs.length].key);
  };
  const current = tabs.find((t) => t.key === active);
  const underline = variant === 'underline';
  return (
    <div>
      <div role="tablist" aria-label={ariaLabel} onKeyDown={onKeyDown}
        className={cn(underline ? 'flex gap-1 overflow-x-auto border-b border-border' : 'inline-flex gap-1 rounded-lg bg-muted p-1', className)}>
        {tabs.map((t) => {
          const on = t.key === active;
          return (
            <button key={t.key} type="button" role="tab" id={`tab-${t.key}`} aria-selected={on}
              aria-controls={t.content !== undefined ? `panel-${t.key}` : undefined} tabIndex={on ? 0 : -1} onClick={() => select(t.key)}
              className={cn(
                'text-sm font-medium transition-colors',
                underline
                  ? cn('-mb-px inline-flex items-center gap-1.5 whitespace-nowrap border-b-2 px-3.5 py-2.5', on ? 'border-blue-600 text-blue-700' : 'border-transparent text-muted-foreground hover:text-foreground')
                  : cn('rounded-md px-3 py-1.5', on ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'))}>
              {t.label}
            </button>
          );
        })}
      </div>
      {current && current.content !== undefined && <div role="tabpanel" id={`panel-${current.key}`} aria-labelledby={`tab-${current.key}`} className="mt-4">{current.content}</div>}
    </div>
  );
}

export function Stepper({ steps = [], current = 0 }) {
  return (
    <ol className="flex items-center gap-2">
      {steps.map((label, i) => {
        const done = i < current;
        const active = i === current;
        return (
          <li key={label} aria-current={active ? 'step' : undefined} className="flex items-center gap-2">
            <span className={cn('flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold', done || active ? 'bg-blue-600 text-white' : 'bg-muted text-muted-foreground')}>
              {done ? '✓' : i + 1}
            </span>
            <span className={cn('text-sm', active ? 'font-medium text-foreground' : 'text-muted-foreground')}>{label}</span>
            {i < steps.length - 1 && <span aria-hidden="true" className={cn('h-px w-8', done ? 'bg-blue-600' : 'bg-border')} />}
          </li>
        );
      })}
    </ol>
  );
}
