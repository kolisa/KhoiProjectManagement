// Tiny class-name joiner for the UI kit (no clsx dependency needed).
export const cn = (...parts) => parts.filter(Boolean).join(' ');
