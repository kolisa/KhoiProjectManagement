using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;


namespace KhoiProjectManagement.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        // Capped per DispatchPendingEmailsAsync run (called every 15s by SendQueuedEmailsJob) so a
        // sudden burst can't make one run take unboundedly long - the next run picks up the rest.
        private const int MaxDispatchBatchSize = 50;

        private readonly IConfiguration _configuration;
        private readonly ProjectManagementContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ProjectManagementContext context, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        private string GetFrontendUrl(string path = "")
        {
            var frontendBaseUrl = (_configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
            return path.Length == 0 ? frontendBaseUrl + "/" : $"{frontendBaseUrl}/{path.TrimStart('/')}";
        }

        public async Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName, DateTime dueDate, string priority)
        {
            var subject = $"Task Assignment: {taskTitle}";
            var inner = "<p>You have been assigned to a new task.</p>";
            var detailRows = new List<(string, string)>
            {
                ("Project", projectName),
                ("Due", dueDate.ToString("yyyy-MM-dd")),
                ("Priority", priority)
            };
            var body = EmailTemplates.Wrap("Task assigned", taskTitle, inner, "View Task", GetFrontendUrl("?tab=tasks"), GetFrontendUrl(), detailRows);

            await EnqueueEmailAsync(toEmail, subject, body, "task_assignment");
        }

        public async Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate)
        {
            var subject = $"Overdue Task: {taskTitle}";
            var inner = "<p>This task is overdue and needs your attention. Please update its status as soon as possible.</p>";
            var detailRows = new List<(string, string)>
            {
                ("Due date", dueDate.ToString("yyyy-MM-dd")),
                ("Days overdue", (DateTime.Now - dueDate).Days.ToString())
            };
            var body = EmailTemplates.Wrap("Overdue task", taskTitle, inner, "View Task", GetFrontendUrl("?tab=tasks"), GetFrontendUrl(), detailRows);

            await EnqueueEmailAsync(toEmail, subject, body, "overdue_reminder");
        }

        public async Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt)
        {
            var subject = $"Reminder: {reminderTitle}";
            var inner = "<p>A reminder you're assigned to has reached its due time.</p>";
            var detailRows = new List<(string, string)> { ("Due", dueAt.ToString("yyyy-MM-dd HH:mm")) };
            var body = EmailTemplates.Wrap("Reminder due", reminderTitle, inner, "View Reminders", GetFrontendUrl("?tab=reminders"), GetFrontendUrl(), detailRows);

            await EnqueueEmailAsync(toEmail, subject, body, "reminder_due");
        }

        public async Task SendProjectCreatedEmailAsync(string toEmail, string projectName)
        {
            var subject = $"Project Created: {projectName}";
            var inner = "<p>A new project has been created and you have been added as a team member.</p>";
            var body = EmailTemplates.Wrap("New project", projectName, inner, "View Project", GetFrontendUrl("?tab=projects"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "project_created");
        }

        public async Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody, string? contextUrl = null)
        {
            var subject = $"{mentionedByName} mentioned you in a comment";
            // commentBody is free-form user input embedded in an HTML email - encode it, unlike the
            // other templates here which only ever interpolate system-controlled strings (titles/names).
            var encodedBody = System.Net.WebUtility.HtmlEncode(commentBody);
            var inner = $@"
                <p><strong>{mentionedByName}</strong> mentioned you in a comment on {contextLabel} <strong>{contextTitle}</strong>:</p>
                <blockquote style=""border-left: 3px solid #ccc; margin: 0; padding-left: 1em; color: #555;"">{encodedBody}</blockquote>
            ";
            var body = EmailTemplates.Wrap("Mention", "You were mentioned in a comment", inner, "View", contextUrl ?? GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "mention");
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Reset your KhoiHub password";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>We received a request to reset your KhoiHub password. Click the button below to choose a new one. This link expires in 1 hour.</p>
                <p>If you didn't request this, you can safely ignore this email - your password won't be changed.</p>
            ";
            var body = EmailTemplates.Wrap("Password reset", "Reset Your Password", inner, "Reset Password", resetLink, GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "password_reset");
        }

        public async Task SendTemporaryPasswordEmailAsync(string toEmail, string userName, string tempPassword)
        {
            var subject = "Your KhoiHub account";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>An account has been created for you on KhoiHub. Here's your temporary password:</p>
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin: 6px 0 18px;"">
                    <tr>
                        <td style=""background-color: #f1effb; border-radius: 10px; padding: 20px 16px; text-align: center;"">
                            <span style=""font-size: 24px; font-weight: 700; letter-spacing: 0.1em; color: #111827;"">{tempPassword}</span>
                        </td>
                    </tr>
                </table>
                <p>Log in with this password and you'll be asked to choose your own before you can continue.</p>
            ";
            var body = EmailTemplates.Wrap("Welcome", "Welcome to KhoiHub", inner, "Log In", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "temp_password");
        }

        public async Task SendLoginReminderEmailAsync(string toEmail, string userName, int daysSinceInvite)
        {
            var subject = "Is something stopping you from logging in?";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>Your KhoiHub account was set up {daysSinceInvite} day{(daysSinceInvite == 1 ? "" : "s")} ago, but you haven't logged in yet to choose your own password.</p>
                <p>If something's making that difficult - a lost temporary password, a question about how the system works, anything at all - please contact your administrator so they can help.</p>
                <p>If you've lost your temporary password, use &ldquo;Forgot password&rdquo; on the login screen to get a new link.</p>
            ";
            var body = EmailTemplates.Wrap("Account reminder", "Is something stopping you from logging in?", inner, "Log In Now", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "login_reminder");
        }

        public async Task SendWeeklyDigestEmailAsync(string toEmail, string userName, int tasksCompleted, int tasksOpen, int tasksOverdue, int projectsActive, int libraryUploads, DateTime weekStart, DateTime weekEnd)
        {
            var subject = $"Your weekly activity digest ({weekStart:MMM d} - {weekEnd:MMM d})";
            var inner = $@"<p>Hi {userName}, here's a summary of your activity for {weekStart:MMM d} - {weekEnd:MMM d}:</p>";
            var detailRows = new List<(string, string)>
            {
                ("Completed", $"{tasksCompleted} task{(tasksCompleted == 1 ? "" : "s")}"),
                ("Open", $"{tasksOpen} task{(tasksOpen == 1 ? "" : "s")}{(tasksOverdue > 0 ? $" ({tasksOverdue} overdue)" : "")}"),
                ("Active projects", projectsActive.ToString()),
                ("Library uploads", $"{libraryUploads} this week")
            };
            var body = EmailTemplates.Wrap("Weekly digest", "Your Weekly Digest", inner, "View Dashboard", GetFrontendUrl("?tab=dashboard"), GetFrontendUrl(), detailRows);

            await EnqueueEmailAsync(toEmail, subject, body, "weekly_digest");
        }

        public async Task SendNoDocumentsNudgeEmailAsync(string toEmail, string userName)
        {
            var subject = "Anything to upload to the Library?";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>We noticed you haven't uploaded any files to the Library yet. If there's nothing you need to upload, no action needed - just let us know.</p>
                <p>Otherwise, head to the Library tab to upload documents whenever you're ready.</p>
            ";
            var body = EmailTemplates.Wrap("Library reminder", "No Documents Uploaded Yet", inner, "Open Library", GetFrontendUrl("?tab=library"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "no_documents_nudge");
        }

        public async Task SendDormantUserNudgeEmailAsync(string toEmail, string userName, int daysSinceLastLogin)
        {
            var subject = "We miss you on KhoiHub";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>It's been {daysSinceLastLogin} days since you last logged in. Your projects and tasks are still waiting for you.</p>
                <p>If something's blocking you from using the system, let your manager or admin know - we'd like to help.</p>
            ";
            var body = EmailTemplates.Wrap("We miss you", "We Miss You", inner, "Log In Now", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "dormant_user_nudge");
        }

        public async Task SendBirthdayEmailAsync(string toEmail, string userName)
        {
            var subject = $"Happy Birthday, {userName}! ";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>Wishing you a very happy birthday from all of us at KhoiHub! Hope you have a fantastic day.</p>
            ";
            var body = EmailTemplates.Wrap("Birthday", "Happy Birthday!", inner, "Open KhoiHub", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "birthday_greeting");
        }

        public async Task SendTimesheetSubmittedEmailAsync(string toEmail, string submitterName, DateTime periodStart, DateTime periodEnd, decimal totalHours)
        {
            var subject = $"Timesheet submitted: {submitterName} ({periodStart:MMM d} - {periodEnd:MMM d})";
            var inner = $@"<p><strong>{submitterName}</strong> submitted a timesheet for your review.</p>";
            var detailRows = new List<(string, string)>
            {
                ("Period", $"{periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}"),
                ("Total hours", totalHours.ToString())
            };
            var body = EmailTemplates.Wrap("Timesheet submitted", "Timesheet Submitted", inner, "View Timesheets", GetFrontendUrl("?tab=timesheets"), GetFrontendUrl(), detailRows);

            await EnqueueEmailAsync(toEmail, subject, body, "timesheet_submitted");
        }

        public async Task SendBroadcastEmailAsync(string toEmail, string subject, string bodyHtml)
        {
            var body = EmailTemplates.Wrap("Announcement", subject, bodyHtml, "Open KhoiHub", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "broadcast");
        }

        // Label + one-line benefit for each of the 6 areas NotificationService.SendSystemOverviewEmailsAsync
        // checks usage of. Vault/Finance/Calendar-management/Reports are deliberately not tracked here -
        // they're permission-gated (Space-scoped or a flat permission), so "you haven't tried this" would
        // be wrong advice for anyone who simply can't access it.
        private static readonly Dictionary<string, (string Label, string Benefit)> TrackedFeatures = new()
        {
            ["tasks"] = ("Projects & Tasks", "See everything you're responsible for in one place, and get notified before anything slips."),
            ["timesheets"] = ("Timesheets", "Log your hours in a couple of clicks each period - or skip typing entirely and upload a CSV instead."),
            ["wiki"] = ("Wiki", "Write down anything worth remembering so your team stops re-explaining it in chat."),
            ["library"] = ("Library", "Keep files everyone needs in one shared, organized place instead of buried in email attachments."),
            ["ideas"] = ("Ideas", "Float a suggestion before it's a whole project and get feedback early."),
            ["reminders"] = ("Reminders", "Set a personal reminder - with recurrence and snooze - so nothing falls through the cracks.")
        };

        // Two variants depending on how much of KhoiHub this user has already touched (see
        // NotificationService.SendSystemOverviewEmailsAsync, which decides which branch applies and
        // supplies either unusedFeatureKeys or the weekly stats accordingly - never both meaningfully at
        // once). Replaces the old one-size-fits-all "here's everything KhoiHub covers" tour.
        public async Task SendSystemOverviewEmailAsync(string toEmail, string userName, IReadOnlyList<string> unusedFeatureKeys, int tasksCompletedThisWeek, int tasksOpen, int activeProjects, int libraryUploadsThisWeek)
        {
            if (unusedFeatureKeys.Count > 0)
            {
                var subject = "A few things you haven't tried yet";
                // The shell's fixed body -> detail rows -> CTA order means anything meant to read as a
                // closer (like pointing them to an admin for help) has to go in this one paragraph,
                // before the list, rather than after it - there's no "trailing text" slot below the rows.
                var inner = $@"
                    <p>Hi {userName},</p>
                    <p>There's a bit more to KhoiHub than what you've used so far. A few areas worth a look -
                    and if any of them aren't obvious to get started with, ask your manager or an admin:</p>
                ";
                var detailRows = unusedFeatureKeys
                    .Where(TrackedFeatures.ContainsKey)
                    .Select(key => (TrackedFeatures[key].Label, TrackedFeatures[key].Benefit))
                    .ToList();
                var body = EmailTemplates.Wrap("Try something new", "A few things you haven't tried yet", inner, "Open KhoiHub", GetFrontendUrl(), GetFrontendUrl(), detailRows);

                await EnqueueEmailAsync(toEmail, subject, body, "system_overview");
            }
            else
            {
                var subject = "Your KhoiHub highlights this week";
                var inner = $@"<p>Hi {userName},</p><p>You're getting the most out of KhoiHub already - nice work. Here's your week:</p>";
                var detailRows = new List<(string, string)>
                {
                    ("Completed", $"{tasksCompletedThisWeek} task{(tasksCompletedThisWeek == 1 ? "" : "s")}"),
                    ("Open", $"{tasksOpen} task{(tasksOpen == 1 ? "" : "s")}"),
                    ("Active projects", activeProjects.ToString()),
                    ("Library uploads", $"{libraryUploadsThisWeek} this week")
                };
                var body = EmailTemplates.Wrap("Weekly highlights", "Nice work this week", inner, "Open KhoiHub", GetFrontendUrl(), GetFrontendUrl(), detailRows);

                await EnqueueEmailAsync(toEmail, subject, body, "system_overview");
            }
        }

        public async Task SendScheduledReportEmailAsync(string toEmail, string reportTitle, byte[] attachmentContent, string attachmentFileName, string attachmentContentType)
        {
            // Not queued (see IEmailService's comment) - already runs off the request thread via
            // ScheduledReportJob, and the outbox table has no column for a byte[] attachment.
            var subject = $"Scheduled report: {reportTitle}";
            var inner = "<p>Your scheduled report is ready. It's attached to this email.</p>";
            var body = EmailTemplates.Wrap("Scheduled report", reportTitle, inner, "View Reports", GetFrontendUrl("?tab=reports"), GetFrontendUrl());

            var log = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                EmailType = "scheduled_report",
                Status = EmailLogStatus.Pending,
                SentAt = DateTime.UtcNow
            };
            _context.EmailLogs.Add(log);
            await _context.SaveChangesAsync();

            await SendAndRecordAsync(log, (attachmentContent, attachmentFileName, attachmentContentType));
        }

        // Fast path: every queued email type ends up here - a single EF insert, no SMTP. The actual
        // send happens later, off the request thread, in DispatchPendingEmailsAsync.
        private async Task EnqueueEmailAsync(string toEmail, string subject, string htmlBody, string emailType)
        {
            _context.EmailLogs.Add(new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = htmlBody,
                EmailType = emailType,
                Status = EmailLogStatus.Pending,
                SentAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // Called only by SendQueuedEmailsJob (Quartz, every 15s) - never awaited by an HTTP request.
        // FIFO by design: Id is a monotonically-increasing identity column assigned at enqueue time, so
        // ordering by it processes emails in exactly the order they were queued - including across
        // multiple runs if a backlog ever exceeds one batch (each run always takes the oldest
        // MaxDispatchBatchSize still Pending, never skips ahead to newer ones).
        public async Task DispatchPendingEmailsAsync()
        {
            var pending = await _context.EmailLogs
                .Where(e => e.Status == EmailLogStatus.Pending)
                .OrderBy(e => e.Id)
                .Take(MaxDispatchBatchSize)
                .ToListAsync();

            foreach (var log in pending)
            {
                await SendAndRecordAsync(log, attachment: null);
            }
        }

        // Shared by the immediate-send path (scheduled reports) and the queued-dispatch path - builds
        // the MimeMessage from an EmailLog row's already-rendered Subject/Body and updates that same
        // row's Status/ErrorMessage in place (never inserts a second row).
        private async Task SendAndRecordAsync(EmailLog log, (byte[] Content, string FileName, string ContentType)? attachment)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"]
                    ?? throw new InvalidOperationException("Email:FromAddress is not configured.");
                var smtpHost = _configuration["Email:SmtpHost"]
                    ?? throw new InvalidOperationException("Email:SmtpHost is not configured.");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_configuration["Email:FromName"], fromAddress));
                message.To.Add(new MailboxAddress("", log.ToEmail));
                message.Subject = log.Subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = log.Body
                };
                if (attachment.HasValue)
                {
                    bodyBuilder.Attachments.Add(attachment.Value.FileName, attachment.Value.Content, ContentType.Parse(attachment.Value.ContentType));
                }
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient
                {
                    // MailKit's default per-operation socket timeout is 100 seconds - bounded to a much
                    // shorter, still-generous timeout instead, so a slow/unreachable relay can't stall a
                    // dispatch batch (or, for the still-synchronous scheduled-report path, the calling
                    // Quartz job) for minutes.
                    Timeout = 20_000
                };
                await client.ConnectAsync(
                    smtpHost,
                    int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                var username = _configuration["Email:SmtpUsername"];
                var password = _configuration["Email:SmtpPassword"];

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await client.AuthenticateAsync(username, password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                log.Status = EmailLogStatus.Sent;
                log.IsSuccess = true;
                log.ErrorMessage = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Email sent: {EmailType} to {ToEmail}", log.EmailType, log.ToEmail);
            }
            catch (Exception ex)
            {
                log.Status = EmailLogStatus.Failed;
                log.IsSuccess = false;
                log.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
                _logger.LogError(ex, "Failed to send email: {EmailType} to {ToEmail}", log.EmailType, log.ToEmail);
            }
        }
    }
}
