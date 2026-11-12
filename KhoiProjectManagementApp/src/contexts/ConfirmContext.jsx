// src/contexts/ConfirmContext.jsx
// App-wide confirmation dialog - replaces window.confirm() (blocking, unstyled, and untestable)
// with a real modal that follows this app's own modal conventions (useModalA11y for Escape/focus-
// trap, matching visual language). useConfirm() returns an async function so call sites keep the
// exact same "if (!(await confirm(...))) return;" shape window.confirm's "if (!window.confirm(...))
// return;" already had - a minimal, low-risk swap at each site, not a redesign of the calling code.
import React, { createContext, useCallback, useContext, useRef, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Modal, Button } from '../components/ui';

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

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {request && (
        <Modal
          onClose={() => settle(false)}
          titleId="confirm-dialog-title"
          describedBy="confirm-dialog-message"
          zIndex="z-110"
          size="sm"
          closeOnBackdrop={false}
          padded={false}
          header={(
            <div className="px-6 pt-6 pb-2 flex items-start gap-3">
              {request.danger && (
                <div className="bg-red-50 rounded-lg p-2 shrink-0">
                  <AlertTriangle className="h-5 w-5 text-red-600" />
                </div>
              )}
              <div className="min-w-0">
                <h3 id="confirm-dialog-title" className="text-base font-semibold text-gray-900">{request.title}</h3>
                <p id="confirm-dialog-message" className="text-sm text-gray-600 mt-1">{request.message}</p>
              </div>
            </div>
          )}
        >
          <div className="px-6 py-4 flex justify-end gap-3 mt-2">
            {/* Cancel is first in DOM order, so Modal's useModalA11y "focus the first focusable
                element on mount" puts focus here, not on Confirm - a deliberate safety default for
                destructive confirms (an accidental Enter keypress cancels, not confirms). */}
            <Button variant="outline" onClick={() => settle(false)}>{request.cancelText}</Button>
            <Button variant={request.danger ? 'destructive' : 'default'} onClick={() => settle(true)}>{request.confirmText}</Button>
          </div>
        </Modal>
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
