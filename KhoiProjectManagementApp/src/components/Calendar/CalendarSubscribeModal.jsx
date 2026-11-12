// src/components/Calendar/CalendarSubscribeModal.jsx
import React, { useState, useEffect } from 'react';
import { Copy, Check, RefreshCw } from 'lucide-react';
import { Button, Input, Modal } from '../ui';
import { useToast } from '../../contexts/ToastContext';

const CalendarSubscribeModal = ({ apiService, onClose }) => {
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
    <Modal
      open
      onClose={onClose}
      title="Subscribe to Calendar"
      footer={<Button variant="outline" onClick={onClose}>Done</Button>}
    >
        <div className="space-y-4">
          <p className="text-sm text-gray-600">
            Get company events, promotions, and birthdays in your own Outlook, Google, or Apple Calendar.
            Generate a link below and add it there as a calendar subscription (not an import) - it stays up to date on its own.
          </p>

          {!feedUrl ? (
            <Button onClick={handleGenerate} disabled={loading}>
              {loading ? 'Generating...' : hasToken ? 'Regenerate Link' : 'Generate Link'}
            </Button>
          ) : (
            <div className="space-y-2">
              <div className="flex items-stretch gap-2">
                <Input
                  type="text"
                  readOnly
                  value={feedUrl}
                  onFocus={(e) => e.target.select()}
                  className="flex-1 min-w-0 text-gray-600 bg-gray-50"
                />
                <Button variant="outline" onClick={handleCopy} className="flex-shrink-0">
                  {copied ? <Check className="h-4 w-4 text-green-600" /> : <Copy className="h-4 w-4" />}
                  {copied ? 'Copied' : 'Copy'}
                </Button>
              </div>
              <Button variant="ghost" size="sm" onClick={handleGenerate} disabled={loading} className="text-gray-500 hover:text-gray-700">
                <RefreshCw className="h-3.5 w-3.5" />
                Regenerate (invalidates this link)
              </Button>
            </div>
          )}

          {hasToken && !feedUrl && (
            <p className="text-xs text-gray-400">
              A link was already generated previously. Regenerating replaces it and invalidates the old one.
            </p>
          )}
        </div>
    </Modal>
  );
};

export default CalendarSubscribeModal;
