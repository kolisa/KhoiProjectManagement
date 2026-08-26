// src/contexts/ToastContext.jsx
// App-wide toast notifications - the one place success/error feedback renders, so no component
// needs to reach for window.alert() (blocking, untestable, and impossible to style) again.
import React, { createContext, useCallback, useContext, useRef, useState } from 'react';
import { CheckCircle2, AlertCircle, Info, X } from 'lucide-react';

const ToastContext = createContext(null);

// Matches the color pairs already used for status pills elsewhere in the app (StatusBadge/
// InvoiceStatusBadge) - green for Paid/success, red for Overdue/error, amber for Sent/warning.
const VARIANTS = {
  success: { icon: CheckCircle2, iconClass: 'text-[#005F2E]', barClass: 'bg-[#005F2E]' },
  error: { icon: AlertCircle, iconClass: 'text-[#B71824]', barClass: 'bg-[#B71824]' },
  info: { icon: Info, iconClass: 'text-[#3752C4]', barClass: 'bg-[#3752C4]' },
};

const DEFAULT_DURATION_MS = 5000;

let nextId = 1;

export const ToastProvider = ({ children }) => {
  const [toasts, setToasts] = useState([]);
  const timers = useRef(new Map());

  const dismiss = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const timer = timers.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }, []);

  const show = useCallback((variant, message, { duration = DEFAULT_DURATION_MS } = {}) => {
    if (!message) return;
    const id = nextId++;
    setToasts((prev) => [...prev, { id, variant, message }]);
    if (duration > 0) {
      const timer = setTimeout(() => dismiss(id), duration);
      timers.current.set(id, timer);
    }
    return id;
  }, [dismiss]);

  const toast = {
    success: (message, opts) => show('success', message, opts),
    error: (message, opts) => show('error', message, { duration: 7000, ...opts }),
    info: (message, opts) => show('info', message, opts),
  };

  return (
    <ToastContext.Provider value={toast}>
      {children}
      <div
        className="fixed top-4 right-4 z-[100] flex flex-col gap-2 w-full max-w-sm pointer-events-none"
        aria-live="polite"
        aria-atomic="false"
      >
        {toasts.map((t) => {
          const { icon: Icon, iconClass, barClass } = VARIANTS[t.variant] || VARIANTS.info;
          return (
            <div
              key={t.id}
              role={t.variant === 'error' ? 'alert' : 'status'}
              className="pointer-events-auto relative overflow-hidden bg-white rounded-[10px] shadow-lg border border-gray-100 flex items-start gap-2.5 p-3.5 pl-4 animate-[toast-in_0.15s_ease-out]"
            >
              <span className={`absolute left-0 top-0 bottom-0 w-1 ${barClass}`} />
              <Icon className={`h-5 w-5 flex-shrink-0 mt-0.5 ${iconClass}`} />
              <p className="text-sm text-gray-800 leading-snug flex-1 pt-0.5">{t.message}</p>
              <button
                onClick={() => dismiss(t.id)}
                className="text-gray-300 hover:text-gray-500 transition-colors flex-shrink-0"
                aria-label="Dismiss notification"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          );
        })}
      </div>
      <style>{`
        @keyframes toast-in {
          from { opacity: 0; transform: translateY(-8px); }
          to { opacity: 1; transform: translateY(0); }
        }
      `}</style>
    </ToastContext.Provider>
  );
};

export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
};
