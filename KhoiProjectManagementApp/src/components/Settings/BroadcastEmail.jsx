// src/components/Settings/BroadcastEmail.jsx
// Admin-only: compose a subject/body and send it to every active user holding at least one of the
// selected roles. Gated by email.broadcast, same permission the API endpoint requires. Plain
// textarea for the body, not a rich-text editor - no such dependency exists anywhere in this
// codebase (matches the CSV-not-Excel precedent from the Timesheets upload feature).
import React, { useState, useEffect } from 'react';
import { Megaphone, Send } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';

const BroadcastEmail = ({ apiService }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [roles, setRoles] = useState([]);
  const [selectedRoleIds, setSelectedRoleIds] = useState([]);
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [sending, setSending] = useState(false);

  useEffect(() => {
    apiService.getRoles().then((list) => setRoles(list || [])).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const toggleRole = (roleId) => {
    setSelectedRoleIds((prev) =>
      prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId]
    );
  };

  const handleSend = async () => {
    if (!subject.trim() || !body.trim()) {
      toast.error('Subject and body are both required.');
      return;
    }
    if (selectedRoleIds.length === 0) {
      toast.error('Select at least one role.');
      return;
    }

    const roleNames = roles.filter((r) => selectedRoleIds.includes(r.id)).map((r) => r.name).join(', ');
    const confirmed = await confirm(
      `This will email everyone with the role${selectedRoleIds.length === 1 ? '' : 's'}: ${roleNames}. This can't be undone.`,
      { title: 'Send broadcast email?', confirmText: 'Send', danger: true }
    );
    if (!confirmed) return;

    setSending(true);
    try {
      const result = await apiService.sendBroadcastEmail({ subject: subject.trim(), body, roleIds: selectedRoleIds });
      toast.success(`Sent to ${result.recipientCount} recipient${result.recipientCount === 1 ? '' : 's'}.`);
      setSubject('');
      setBody('');
      setSelectedRoleIds([]);
    } catch (err) {
      reportApiError(toast, err, 'Could not send the broadcast email.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 flex items-center">
          <Megaphone className="h-5 w-5 mr-2 text-gray-700" />
          Broadcast Email
        </h3>
        <p className="text-sm text-gray-500">Send an email to everyone holding one or more selected roles.</p>
      </div>

      <div className="space-y-1.5">
        <label className="block text-sm font-medium text-gray-700">Send to</label>
        <div className="flex flex-wrap gap-3">
          {roles.map((role) => (
            <label key={role.id} className="inline-flex items-center gap-1.5 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={selectedRoleIds.includes(role.id)}
                onChange={() => toggleRole(role.id)}
                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              {role.name}
            </label>
          ))}
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="broadcast-subject">Subject</label>
        <input
          id="broadcast-subject"
          type="text"
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
          maxLength={200}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="broadcast-body">Message</label>
        <textarea
          id="broadcast-body"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={6}
          maxLength={10000}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
      </div>

      <div className="flex justify-end">
        <button
          type="button"
          onClick={handleSend}
          disabled={sending}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
        >
          <Send className="h-4 w-4" />
          {sending ? 'Sending...' : 'Send'}
        </button>
      </div>
    </div>
  );
};

export default BroadcastEmail;
