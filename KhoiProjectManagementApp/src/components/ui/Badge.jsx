import React from 'react';
import { cn } from './cn';

const VARIANTS = {
  default: 'bg-blue-600 text-white',
  secondary: 'bg-muted text-foreground',
  outline: 'border border-border text-foreground',
  success: 'bg-green-100 text-green-800',
  warning: 'bg-amber-100 text-amber-800',
  destructive: 'bg-red-100 text-red-800',
  info: 'bg-blue-100 text-blue-800',
};

export default function Badge({ variant = 'default', className, ...props }) {
  return (
    <span
      className={cn('inline-flex items-center justify-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium', VARIANTS[variant], className)}
      {...props}
    />
  );
}
