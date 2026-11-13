// Class-name joiner for the UI kit. Uses tailwind-merge so a caller's className can override a kit default
// (e.g. `w-auto` over an Input's `w-full`) instead of both classes fighting by stylesheet order.
import { twMerge } from 'tailwind-merge';

export const cn = (...parts) => twMerge(parts.filter(Boolean).join(' '));
