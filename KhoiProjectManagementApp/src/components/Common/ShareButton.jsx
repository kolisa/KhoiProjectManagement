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
        className="text-gray-400 hover:text-gray-600"
        aria-label="Share"
        title="Share"
      >
        <Share2 className="h-4 w-4" />
      </button>
      {open && (
        <div className="absolute right-0 mt-2 w-72 bg-white border rounded-lg shadow-lg p-3 z-10">
          <div className="text-xs text-gray-500 mb-2">Anyone with this link still needs to log in and have access.</div>
          <div className="flex items-center space-x-2 mb-2">
            <input
              readOnly
              value={url}
              onClick={(e) => e.target.select()}
              className="flex-1 border rounded px-2 py-1 text-xs text-gray-600"
            />
            <button
              onClick={handleCopy}
              className="flex items-center text-xs text-blue-600 hover:text-blue-800 flex-shrink-0"
            >
              {copied ? <Check className="h-3.5 w-3.5 mr-1" /> : <Copy className="h-3.5 w-3.5 mr-1" />}
              {copied ? 'Copied' : 'Copy'}
            </button>
          </div>
          <button
            onClick={handleWhatsApp}
            className="w-full text-sm bg-green-600 text-white py-1.5 rounded-lg hover:bg-green-700"
          >
            Share via WhatsApp
          </button>
        </div>
      )}
    </div>
  );
};

export default ShareButton;
