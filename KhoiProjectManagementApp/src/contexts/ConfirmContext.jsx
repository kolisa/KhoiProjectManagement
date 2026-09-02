// src/contexts/ConfirmContext.jsx
// App-wide confirmation dialog - replaces window.confirm() (blocking, unstyled, and untestable)
// with a real modal that follows this app's own modal conventions (useModalA11y for Escape/focus-
// trap, matching visual language). useConfirm() returns an async function so call sites keep the
// exact same "if (!(await confirm(...))) return;" shape window.confirm's "if (!window.confirm(...))
// return;" already had - a minimal, low-risk swap at each site, not a redesign of the calling code.
import React, { createContext, useCallback, useContext, useRef, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import useModalA11y from '../components/Common/useModalA11y';

const ConfirmContext = createContext(null);

export const ConfirmProvider = ({ children }) => {
  const [request, setRequest] = useState(null); // { message, title, confirmText, cancelText, danger } | null
  const resolveRef = useRef(null);

  const settle = useCallback((result) => {
    resolveRef.current?.(result);
    resolveRef.current = null;
    setRequest(null);
  }, []);

  const confirm = useCallback((message, options = {}) => {
    return new Promise((resolve) => {
      resolveRef.current = resolve;
      setRequest({
        message,
        title: options.title ?? 'Please confirm',
        confirmText: options.confirmText ?? 'Confirm',
        cancelText: options.cancelText ?? 'Cancel',
        danger: options.danger ?? false,
      });
    });
  }, []);

  // Called unconditionally (same convention as every other modal ref in this app - see App.jsx) so
  // hook order never depends on whether a confirm is currently pending; the ref just has nothing to
  // attach to when `request` is null.
  const modalRef = useModalA11y(() => settle(false));

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {request && (
        <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-[110]">
          <div
            ref={modalRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-dialog-title"
            aria-describedby="confirm-dialog-message"
            tabIndex={-1}
            className="bg-white rounded-2xl shadow-xl max-w-sm w-full outline-none"
          >
            <div className="px-6 pt-6 pb-2 flex items-start gap-3">
              {request.danger && (
                <div className="bg-red-50 rounded-lg p-2 flex-shrink-0">
                  <AlertTriangle className="h-5 w-5 text-red-600" />
                </div>
              )}
              <div className="min-w-0">
                <h3 id="confirm-dialog-title" className="text-base font-semibold text-gray-900">{request.title}</h3>
                <p id="confirm-dialog-message" className="text-sm text-gray-600 mt-1">{request.message}</p>
              </div>
            </div>
            <div className="px-6 py-4 flex justify-end gap-3 mt-2">
              {/* Cancel is first in DOM order, so useModalA11y's own "focus the first focusable
                  element on mount" puts focus here, not on Confirm - a deliberate safety default for
                  destructive confirms (an accidental Enter keypress cancels, not confirms). */}
              <button
                type="button"
                onClick={() => settle(false)}
                className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
              >
                {request.cancelText}
              </button>
              <button
                type="button"
                onClick={() => settle(true)}
                className={`inline-flex items-center gap-2 px-4 py-2.5 rounded-[10px] text-sm font-semibold shadow-sm transition-colors text-white ${
                  request.danger ? 'bg-red-600 hover:bg-red-700' : 'bg-blue-600 hover:bg-blue-700'
                }`}
              >
                {request.confirmText}
              </button>
            </div>
          </div>
        </div>
      )}
    </ConfirmContext.Provider>
  );
};

export const useConfirm = () => {
  const context = useContext(ConfirmContext);
  if (!context) {
    throw new Error('useConfirm must be used within a ConfirmProvider');
  }
  return context;
};
