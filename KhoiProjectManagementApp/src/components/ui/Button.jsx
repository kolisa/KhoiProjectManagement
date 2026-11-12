// Cleopatra-style button. Variants/sizes mirror the template's btn-* classes; primary uses the app's brand accent.
import React from 'react';
import { cn } from './cn';

const VARIANTS = {
  default: 'bg-blue-600 text-white hover:bg-blue-700',
  secondary: 'bg-muted text-foreground hover:bg-gray-200',
  outline: 'border border-border bg-transparent text-foreground hover:bg-muted',
  ghost: 'bg-transparent text-foreground hover:bg-muted',
  link: 'bg-transparent text-blue-600 underline-offset-4 hover:underline',
  destructive: 'bg-red-600 text-white hover:bg-red-700',
  success: 'bg-success text-white hover:opacity-90',
  warning: 'bg-warning text-gray-900 hover:opacity-90',
};
const SIZES = { sm: 'h-8 px-3 text-xs', md: 'h-10 px-4 text-sm', lg: 'h-11 px-8 text-base' };
const ICON_SIZES = { sm: 'h-8 w-8', md: 'h-10 w-10', lg: 'h-11 w-11' };

const Button = React.forwardRef(function Button(
  { variant = 'default', size = 'md', icon = false, type = 'button', className, ...props }, ref) {
  return (
    <button
      ref={ref}
      type={type}
      className={cn(
        'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg font-medium transition-colors',
        'focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2',
        'disabled:pointer-events-none disabled:opacity-50',
        VARIANTS[variant], icon ? cn(ICON_SIZES[size], 'p-0') : SIZES[size], className)}
      {...props}
    />
  );
});
export default Button;
