import React from 'react';
import { Eye, Pencil, ShieldCheck } from 'lucide-react';
import { accessLevel, ACCESS_LEVEL_LABELS, roleLabel } from './pmConstants';

// Shared look for the Project Management module: one card style, one avatar, one set of badges, so the
// workspace shell, the members page, the access admin and the overview read as a single design.

export const cardClass = 'bg-white border border-gray-200 rounded-xl shadow-sm';

const AVATAR_COLORS = [
  'bg-blue-100 text-blue-700',
  'bg-emerald-100 text-emerald-700',
  'bg-violet-100 text-violet-700',
  'bg-amber-100 text-amber-700',
  'bg-rose-100 text-rose-700',
  'bg-teal-100 text-teal-700',
];

const initials = (name) => {
  const parts = String(name || '?').trim().split(/\s+/).filter(Boolean);
  return ((parts[0]?.[0] || '?') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
};

const colorFor = (name) => {
  let hash = 0;
  for (const ch of String(name || '')) hash = (hash * 31 + ch.charCodeAt(0)) % 997;
  return AVATAR_COLORS[hash % AVATAR_COLORS.length];
};

const AVATAR_SIZES = { sm: 'h-7 w-7 text-[11px]', md: 'h-9 w-9 text-xs', lg: 'h-12 w-12 text-base' };

// Initials on a stable colour per name - decorative, so hidden from assistive technology (the name is always
// printed next to it).
export const Avatar = ({ name, size = 'md', square = false }) => (
  <span aria-hidden="true"
    className={`inline-flex flex-shrink-0 items-center justify-center font-semibold ${square ? 'rounded-lg' : 'rounded-full'} ${AVATAR_SIZES[size]} ${colorFor(name)}`}>
    {initials(name)}
  </span>
);

export const RoleChip = ({ role }) => (
  <span className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700">{roleLabel(role)}</span>
);

const ACCESS_STYLES = {
  read: { cls: 'bg-gray-100 text-gray-700', Icon: Eye },
  write: { cls: 'bg-blue-50 text-blue-700', Icon: Pencil },
  full: { cls: 'bg-violet-50 text-violet-700', Icon: ShieldCheck },
};

// What a person can actually do in a project, as one word: Read only / Read & write / Full control.
// `readOnly` is the person's module-level policy; it wins over any project role.
export const AccessBadge = ({ role, readOnly = false }) => {
  const level = accessLevel(role, readOnly);
  const { cls, Icon } = ACCESS_STYLES[level];
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}
      title={readOnly ? 'Read-only access was set for this person by an administrator' : undefined}>
      <Icon className="h-3 w-3" aria-hidden="true" />
      {ACCESS_LEVEL_LABELS[level]}
    </span>
  );
};

export const EmptyState = ({ icon: Icon, title, children }) => (
  <div className="flex flex-col items-center text-center bg-white border border-dashed border-gray-300 rounded-xl px-6 py-14">
    {Icon && <span className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-gray-100 text-gray-500"><Icon className="h-6 w-6" aria-hidden="true" /></span>}
    <h2 className="text-base font-semibold text-gray-900">{title}</h2>
    {children && <div className="mt-1 max-w-md text-sm text-gray-500">{children}</div>}
  </div>
);
