// Form primitives: Label, Input, Textarea, Select, FieldError, Checkbox, Radio, Switch.
import React from 'react';
import { cn } from './cn';

const FIELD = 'w-full rounded-lg border border-gray-300 bg-card px-3 text-sm text-foreground placeholder:text-muted-foreground focus:border-blue-500 focus:outline-hidden focus:ring-2 focus:ring-blue-500/40 disabled:cursor-not-allowed disabled:opacity-50';
const INVALID = 'border-red-500 focus:border-red-500 focus:ring-red-500/40';

export function Label({ className, ...props }) {
  return <label className={cn('mb-1.5 block text-sm font-medium text-foreground', className)} {...props} />;
}

export const Input = React.forwardRef(function Input({ invalid, className, ...props }, ref) {
  return <input ref={ref} aria-invalid={invalid || undefined} className={cn(FIELD, 'h-10', invalid && INVALID, className)} {...props} />;
});

export const Textarea = React.forwardRef(function Textarea({ invalid, className, rows = 3, ...props }, ref) {
  return <textarea ref={ref} rows={rows} aria-invalid={invalid || undefined} className={cn(FIELD, 'py-2', invalid && INVALID, className)} {...props} />;
});

export const Select = React.forwardRef(function Select({ invalid, className, children, ...props }, ref) {
  return <select ref={ref} aria-invalid={invalid || undefined} className={cn(FIELD, 'h-10', invalid && INVALID, className)} {...props}>{children}</select>;
});

export function FieldError({ children }) {
  return children ? <p role="alert" className="mt-1 text-xs text-red-600">{children}</p> : null;
}

function ChoiceInput({ type, label, className, ...props }) {
  return (
    <label className={cn('inline-flex cursor-pointer items-center gap-2 text-sm', props.disabled && 'cursor-not-allowed opacity-50', className)}>
      <input type={type} className="h-4 w-4 border-gray-300 text-blue-600 focus:ring-2 focus:ring-blue-500" {...props} />
      {label && <span>{label}</span>}
    </label>
  );
}
export const Checkbox = (props) => <ChoiceInput type="checkbox" {...props} />;
export const Radio = (props) => <ChoiceInput type="radio" {...props} />;

export function Switch({ checked, onChange, label, disabled = false }) {
  return (
    <label className={cn('inline-flex cursor-pointer items-center gap-3 text-sm', disabled && 'cursor-not-allowed opacity-50')}>
      <button
        type="button" role="switch" aria-checked={!!checked} disabled={disabled}
        onClick={() => onChange?.(!checked)}
        className={cn('relative h-[22px] w-10 shrink-0 rounded-full transition-colors focus:outline-hidden focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-1', checked ? 'bg-blue-600' : 'bg-gray-300')}
      >
        <span className={cn('absolute left-[2px] top-[2px] h-[18px] w-[18px] rounded-full bg-white shadow-xs transition-transform', checked && 'translate-x-[18px]')} />
      </button>
      {label && <span>{label}</span>}
    </label>
  );
}
