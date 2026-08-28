// src/components/Common/useModalA11y.js
// Wires standard modal-dialog accessibility onto an existing modal's outer content box: Escape
// closes it, Tab/Shift+Tab stay trapped inside it, focus moves into it on mount, and focus returns
// to whatever triggered it on close. Attach the returned ref to the element carrying
// role="dialog" aria-modal="true" (the content box, not the full-screen overlay behind it) - the
// modal's own visual markup/layout doesn't change, this only adds keyboard behavior around it.
//
// Returns a CALLBACK ref, not a plain object ref, deliberately: a plain `useRef` + `useEffect(fn, [])`
// only ever runs its setup once, right after the OWNING component's first render - if that owning
// component is an always-mounted parent that conditionally renders the modal inline (e.g. a single
// large page component showing/hiding a modal via a boolean flag, rather than mounting/unmounting a
// separate modal component), the effect fires before the modal ever opens, `ref.current` is null at
// that point, and the trap/Escape handling never actually attaches - a real bug this hook hit during
// its first rollout. A callback ref is invoked by React with the real DOM node every time it actually
// mounts or unmounts, independent of when the owning component's own hooks run, so it works correctly
// both for a modal that's its own mount/unmount component AND for one that's inline conditional JSX
// inside an always-mounted parent.
import { useCallback, useRef } from 'react';

const FOCUSABLE_SELECTOR = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

export default function useModalA11y(onClose) {
  const cleanupRef = useRef(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  const setRef = useCallback((node) => {
    if (cleanupRef.current) {
      cleanupRef.current();
      cleanupRef.current = null;
    }
    if (!node) return;

    const getFocusable = () =>
      Array.from(node.querySelectorAll(FOCUSABLE_SELECTOR)).filter((el) => el.offsetParent !== null);

    const previouslyFocused = document.activeElement;
    const first = getFocusable()[0];
    (first || node).focus({ preventScroll: true });

    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onCloseRef.current?.();
        return;
      }
      if (e.key !== 'Tab') return;
      const items = getFocusable();
      if (items.length === 0) return;
      const firstEl = items[0];
      const lastEl = items.at(-1);
      if (e.shiftKey && document.activeElement === firstEl) {
        e.preventDefault();
        lastEl.focus();
      } else if (!e.shiftKey && document.activeElement === lastEl) {
        e.preventDefault();
        firstEl.focus();
      }
    };

    node.addEventListener('keydown', handleKeyDown);
    cleanupRef.current = () => {
      node.removeEventListener('keydown', handleKeyDown);
      if (previouslyFocused instanceof HTMLElement) previouslyFocused.focus({ preventScroll: true });
    };
  }, []);

  return setRef;
}
