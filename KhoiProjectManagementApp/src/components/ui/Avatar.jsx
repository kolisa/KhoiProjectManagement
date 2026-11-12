import React, { useState } from 'react';
import { cn } from './cn';

const SIZES = { sm: 'h-8 w-8 text-xs', md: 'h-10 w-10 text-sm', lg: 'h-12 w-12 text-base' };
const initials = (name) => (name || '?').split(' ').filter(Boolean).map((n) => n[0]).slice(0, 2).join('').toUpperCase();

export function Avatar({ name, src, size = 'md', className }) {
  const [failed, setFailed] = useState(false);
  return (
    <span className={cn('relative inline-flex flex-shrink-0 items-center justify-center overflow-hidden rounded-full bg-blue-100 font-semibold text-blue-700', SIZES[size], className)} title={name}>
      {src && !failed ? <img src={src} alt={name || ''} className="h-full w-full object-cover" onError={() => setFailed(true)} /> : initials(name)}
    </span>
  );
}

export function AvatarGroup({ people = [], max = 4, size = 'sm' }) {
  const shown = people.slice(0, max);
  const extra = people.length - shown.length;
  return (
    <div className="flex -space-x-2">
      {shown.map((p, i) => <Avatar key={p.id ?? i} name={p.name} src={p.src} size={size} className="ring-2 ring-card" />)}
      {extra > 0 && <span className={cn('inline-flex items-center justify-center rounded-full bg-muted text-xs font-medium ring-2 ring-card', SIZES[size])}>+{extra}</span>}
    </div>
  );
}
