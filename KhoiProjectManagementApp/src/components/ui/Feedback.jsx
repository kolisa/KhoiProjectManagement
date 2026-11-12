// Skeleton, Progress, Separator, Kbd.
import React from 'react';
import { cn } from './cn';

export function Skeleton({ className, ...props }) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-lg bg-muted', className)} {...props} />;
}

const BAR = { default: 'bg-blue-600', success: 'bg-success', warning: 'bg-warning', destructive: 'bg-red-600' };
export function Progress({ value = 0, max = 100, variant = 'default', label, className }) {
  const pct = Math.min(100, Math.max(0, max > 0 ? (value / max) * 100 : 0));
  return (
    <div role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={max} aria-valuenow={value}
      className={cn('h-2 w-full overflow-hidden rounded-full bg-muted', className)}>
      <div className={cn('h-full rounded-full transition-all', BAR[variant])} style={{ width: `${pct}%` }} />
    </div>
  );
}

export function Separator({ orientation = 'horizontal', className }) {
  return <div role="separator" aria-orientation={orientation}
    className={cn('flex-shrink-0 bg-border', orientation === 'vertical' ? 'h-full w-px' : 'h-px w-full', className)} />;
}

export function Kbd({ className, ...props }) {
  return <kbd className={cn('inline-flex items-center rounded-md border border-border bg-card px-1.5 py-0.5 font-mono text-[11px] font-medium text-muted-foreground', className)} {...props} />;
}
