import React from 'react';
import { cn } from './cn';

export function Card({ className, ...props }) {
  return <div className={cn('rounded-2xl border border-border bg-card text-card-foreground shadow-sm', className)} {...props} />;
}
export function CardHeader({ className, ...props }) {
  return <div className={cn('flex flex-col gap-1 p-6 pb-0', className)} {...props} />;
}
export function CardTitle({ as: Tag = 'h3', className, ...props }) {
  return <Tag className={cn('text-base font-semibold leading-none tracking-tight', className)} {...props} />;
}
export function CardDescription({ className, ...props }) {
  return <p className={cn('text-sm text-muted-foreground', className)} {...props} />;
}
export function CardContent({ className, ...props }) {
  return <div className={cn('p-6', className)} {...props} />;
}
export function CardFooter({ className, ...props }) {
  return <div className={cn('flex items-center gap-2 border-t border-border p-4', className)} {...props} />;
}
