---
name: ui-modernization
description: Modernize the visual design, layout, accessibility, or responsiveness of an existing Khoi Pro screen or component without changing what it does. Use for spacing/typography/consistency passes, fixing modal accessibility, responsive-table fixes, or design-system consolidation. Do not use this to add product features while touching UI.
---

# UI modernization (Khoi Pro frontend)

**Do not add product features while modernizing the UI.** This skill is for making an existing
screen look and behave better - not for adding new fields, new actions, or new workflows.

## Stack facts

React 18, plain JavaScript (`.jsx`, no TypeScript), Tailwind CSS, no component library (no
MUI/Chakra/Ant/shadcn). Design tokens are inline Tailwind classes, not a theme file - match the
existing palette (see `App.jsx`'s `statusColors`/`priorityColors` objects and
`components/Common/*Badge.jsx` for the color vocabulary already in use) rather than introducing a
new one.

## Fixed since the 2026-08-28 audit - use these, don't reintroduce the old gaps

- **Modal dialog semantics**: every modal now wires `role="dialog"`, `aria-modal="true"`, Escape-to-
  close, and a Tab focus-trap via the shared `useModalA11y(onClose)` hook
  (`components/Common/useModalA11y.js`) - it returns a callback ref you attach to the modal's content
  box (not the full-screen overlay), plus give the content box `aria-labelledby` pointing at the
  title's `id` (or `aria-label` if there's no in-file title to point at, e.g. when the modal wraps a
  detail component defined elsewhere - see `IdeasPage.jsx`'s idea-detail modal for that case). A new
  modal that skips this, or hand-rolls its own Escape/focus-trap logic instead of using the hook, is
  a regression.
- **`StatusBadge`/`RoleBadge`/`TagsList` deduped**: `App.jsx` now imports all six shared primitives
  (these three plus `LoadingSpinner`/`ErrorMessage`/`PriorityBadge`) from `components/Common/`
  instead of redefining them. The canonical style is `Common/`'s: `RoleBadge` has icons and a
  purple/blue/green palette (not `App.jsx`'s old plain indigo/green/gray), and `StatusBadge` takes a
  caller-supplied `colorMap` plus an optional `label` override (for display text that differs from
  the color-lookup key, e.g. `label={status.replace('-', ' ')}` while `status` stays the raw
  hyphenated value) - `App.jsx`'s task-status call sites are the reference example.
- **Code-splitting**: the six heavier feature-tab pages (Vault/Wiki/Library/Ideas/Reminders/Finance)
  and the five Settings panels are `React.lazy()`-loaded from `App.jsx`, each wrapped in its own
  `<Suspense fallback={<LoadingSpinner />}>` at the render site - main bundle dropped from ~628KB to
  ~280KB. A new heavy feature page/tab should follow the same pattern rather than being eagerly
  imported into `App.jsx`.

`Settings/AuditLog.jsx`'s table also now scrolls (`overflow-x-auto`, `title` tooltips on truncated
cells) rather than clipping content - `Reminders/ReminderList.jsx` (`hidden md:table` + a separate
mobile card layout) is still the reference for a *fuller* responsive-table treatment if a
higher-traffic table ever needs it; AuditLog's simpler scroll-based fix was proportionate for a
low-traffic admin screen, not every table needs the full card-layout treatment.

## Rules

- Preserve every existing interaction (click targets, keyboard shortcuts, form submit behavior)
  exactly - a modernization pass changes how something looks/reads, not what it does.
- Don't introduce a new spacing/color/radius scale piecemeal - if a change would require touching
  more than 2-3 files to stay consistent, that's a design-system decision to flag to the user
  first, not something to push through silently.
- Verify visually if you can (`npm run dev` and look at it) - a passing `npm run build` proves it
  compiles, not that it looks right.
- After changes: `cd KhoiProjectManagementApp && npm run build && npm run test:run` - both green.
