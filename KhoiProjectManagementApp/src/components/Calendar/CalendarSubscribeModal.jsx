// src/components/Calendar/CalendarSubscribeModal.jsx
import React, { useState, useEffect } from 'react';
import { X, Copy, Check, RefreshCw } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';
import { useToast } from '../../contexts/ToastContext';

const CalendarSubscribeModal = ({ apiService, onClose }) => {
  const modalRef = useModalA11y(onClose);
  const toast = useToast();
  const [hasToken, setHasToken] = useState(null);
  const [feedUrl, setFeedUrl] = useState(null);
  const [loading, setLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    apiService.getCalendarFeedTokenStatus()
      .then((s) => setHasToken(!!s?.hasToken))
      .catch(() => setHasToken(false));
  }, [apiService]);

  const handleGenerate = async () => {
    setLoading(true);
    try {
      const { token } = await apiService.regenerateCalendarFeedToken();
      setFeedUrl(apiService.getIcsFeedUrl(token));
      setHasToken(true);
    } catch {
      toast.error('Could not generate a subscription link.');
    } finally {
      setLoading(false);
    }
  };

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(feedUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error('Could not copy the link - select and copy it manually.');
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="calendar-subscribe-modal-title"
        tabIndex={-1}
        className="bg-white rounded-2xl shadow-xl w-full max-w-lg outline-none"
      >
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 id="calendar-subscribe-modal-title" className="text-lg font-semibold text-gray-900">Subscribe to Calendar</h3>
          <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4">
          <p className="text-sm text-gray-600">
            Get company events, promotions, and birthdays in your own Outlook, Google, or Apple Calendar.
            Generate a link below and add it there as a calendar subscription (not an import) - it stays up to date on its own.
          </p>

          {!feedUrl ? (
            <button
              onClick={handleGenerate}
              disabled={loading}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
            >
              {loading ? 'Generating...' : hasToken ? 'Regenerate Link' : 'Generate Link'}
            </button>
          ) : (
            <div className="space-y-2">
              <div className="flex items-stretch gap-2">
                <input
                  type="text"
                  readOnly
                  value={feedUrl}
                  onFocus={(e) => e.target.select()}
                  className="flex-1 min-w-0 border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm text-gray-600 bg-gray-50 focus:outline-none"
                />
                <button
                  onClick={handleCopy}
                  className="inline-flex items-center gap-1.5 bg-white text-gray-700 border border-gray-300 px-3.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors flex-shrink-0"
                >
                  {copied ? <Check className="h-4 w-4 text-green-600" /> : <Copy className="h-4 w-4" />}
                  {copied ? 'Copied' : 'Copy'}
                </button>
              </div>
              <button
                onClick={handleGenerate}
                disabled={loading}
                className="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors disabled:opacity-50"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                Regenerate (invalidates this link)
              </button>
            </div>
          )}

          {hasToken && !feedUrl && (
            <p className="text-xs text-gray-400">
              A link was already generated previously. Regenerating replaces it and invalidates the old one.
            </p>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
            Done
          </button>
        </div>
      </div>
    </div>
  );
};

export default CalendarSubscribeModal;
