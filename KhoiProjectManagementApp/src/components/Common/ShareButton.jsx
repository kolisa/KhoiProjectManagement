// src/components/Common/ShareButton.js
// Copies a direct link to the item and offers a WhatsApp share intent. The link is a shortcut into
// the app, not a public bypass - opening it still requires login and still goes through the normal
// Space permission checks (see App.js's deepLink handling), same as browsing there manually.
import React, { useState, useRef, useEffect } from 'react';
import { Share2, Copy, Check } from 'lucide-react';

const ShareButton = ({ url, label }) => {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const containerRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (containerRef.current && !containerRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API can be unavailable (insecure context, permissions) - the link is still visible
      // in the popover for manual copy, so this isn't a dead end.
    }
  };

  const handleWhatsApp = () => {
    const message = `${label ? label + ': ' : ''}${url}`;
    window.open(`https://wa.me/?text=${encodeURIComponent(message)}`, '_blank', 'noopener,noreferrer');
  };

  return (
    <div className="relative" ref={containerRef}>
      <button
        onClick={() => setOpen(!open)}
        className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
        aria-label="Share"
        title="Share"
      >
        <Share2 className="h-4 w-4" />
      </button>
      {open && (
        <div className="absolute right-0 mt-2 w-72 bg-white border border-gray-100 rounded-xl shadow-lg p-3 z-10">
          <div className="text-xs text-gray-500 mb-2">Anyone with this link still needs to log in and have access.</div>
          <div className="flex items-center space-x-2 mb-2">
            <input
              readOnly
              value={url}
              onClick={(e) => e.target.select()}
              className="flex-1 border border-gray-300 rounded-md px-2 py-1.5 text-xs text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
            <button
              onClick={handleCopy}
              className="inline-flex items-center gap-1 text-xs font-medium text-blue-600 hover:text-blue-800 transition-colors flex-shrink-0"
            >
              {copied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
              {copied ? 'Copied' : 'Copy'}
            </button>
          </div>
          <button
            onClick={handleWhatsApp}
            className="w-full inline-flex items-center justify-center text-sm font-semibold bg-green-600 text-white py-2 rounded-lg hover:bg-green-700 shadow-sm transition-colors"
          >
            Share via WhatsApp
          </button>
        </div>
      )}
    </div>
  );
};

export default ShareButton;
