using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    // EmailLog doubles as the send outbox, not just an audit trail: EmailService.EnqueueEmailAsync
    // inserts a Pending row (fast - no SMTP call), and the SendQueuedEmailsJob background job flips it
    // to Sent/Failed once actually dispatched. IsSuccess/ErrorMessage stay as the audit-facing fields
    // (derived from Status at write time) so the existing Audit page's shape doesn't change.
    public enum EmailLogStatus
    {
        Pending,
        Sent,
        Failed
    }

    public class EmailLog : BaseEntity
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string EmailType { get; set; } = string.Empty;
        public int? TaskId { get; set; }
        public int? ProjectId { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public EmailLogStatus Status { get; set; }
    }
}
