import React from 'react';
import { cn } from './cn';

const VARIANTS = {
  default: 'border-border bg-card text-foreground',
  info: 'border-blue-200 bg-blue-50 text-blue-900',
  success: 'border-green-200 bg-green-50 text-green-900',
  warning: 'border-amber-200 bg-amber-50 text-amber-900',
  destructive: 'border-red-200 bg-red-50 text-red-900',
};

export default function Alert({ variant = 'default', title, children, onDismiss, className }) {
  return (
    <div role={variant === 'destructive' ? 'alert' : 'status'} className={cn('relative flex gap-3 rounded-lg border p-4 text-sm', VARIANTS[variant], className)}>
      <div className="min-w-0 flex-1">
        {title && <p className="font-medium leading-tight">{title}</p>}
        {children && <div className={cn(title && 'mt-1 opacity-90')}>{children}</div>}
      </div>
      {onDismiss && (
        <button type="button" onClick={onDismiss} aria-label="Dismiss" className="self-start rounded-sm p-0.5 opacity-60 hover:opacity-100">&times;</button>
      )}
    </div>
  );
}
