using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection.Emit;
using Attachment = KhoiProjectManagement.Domain.Attachment;

namespace KhoiProjectManagement.Infrastructure.Data
{
    public class ProjectManagementContext : DbContext, IDataProtectionKeyContext
    {
        public ProjectManagementContext(DbContextOptions<ProjectManagementContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTask> Tasks { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProjectUser> ProjectUsers { get; set; }
        public DbSet<ProjectTag> ProjectTags { get; set; }
        public DbSet<TaskTag> TaskTags { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }

        // Cross-feature activity feed (dashboard's "Activity" widget) - see ActivityLogService.
        public DbSet<ActivityLogEntry> ActivityLogEntries { get; set; }

        // Flat, resource+action permission system for the app's fixed CRUD resources.
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        // Hierarchical, Space-scoped permission system for dynamic containers (vault, future docs/wiki).
        public DbSet<Space> Spaces { get; set; }
        public DbSet<SpacePermission> SpacePermissions { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        public DbSet<VaultEntry> VaultEntries { get; set; }
        public DbSet<VaultAuditLog> VaultAuditLogs { get; set; }

        public DbSet<WikiPage> WikiPages { get; set; }
        public DbSet<WikiPageVersion> WikiPageVersions { get; set; }
        public DbSet<WikiPageComment> WikiPageComments { get; set; }
        public DbSet<WikiPageTag> WikiPageTags { get; set; }

        public DbSet<LibraryFile> LibraryFiles { get; set; }
        public DbSet<LibraryFileVersion> LibraryFileVersions { get; set; }

        // Flat HR onboarding - not Space-scoped, a checklist belongs to exactly one User.
        public DbSet<OnboardingTemplate> OnboardingTemplates { get; set; }
        public DbSet<OnboardingTemplateItem> OnboardingTemplateItems { get; set; }
        public DbSet<OnboardingChecklist> OnboardingChecklists { get; set; }
        public DbSet<OnboardingChecklistItem> OnboardingChecklistItems { get; set; }

        // Flat finance module - Admin-only, no natural "or self"/"manager" carve-out for money.
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
        public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }

        // Flat, ownership-based timesheets - a user always manages their own, no permission needed.
        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<TimesheetEntry> TimesheetEntries { get; set; }

        // Company-wide, flat ideas board - not Space-scoped.
        public DbSet<Idea> Ideas { get; set; }
        public DbSet<IdeaComment> IdeaComments { get; set; }
        public DbSet<IdeaAttachment> IdeaAttachments { get; set; }
        public DbSet<IdeaAttachmentAnnotation> IdeaAttachmentAnnotations { get; set; }

        // Company calendar - birthdays are computed from User.DateOfBirth at read time, not stored here.
        public DbSet<CompanyEvent> CompanyEvents { get; set; }

        // Per-user, per-notification-type email opt-outs (opt-out model - see NotificationPreference).
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }

        // Configurable dashboard: admin-controlled widget allow-list + per-user visibility/order.
        public DbSet<DashboardWidgetAllowlist> DashboardWidgetAllowlists { get; set; }
        public DbSet<DashboardWidgetPreference> DashboardWidgetPreferences { get; set; }

        // Daily snapshot of dashboard stats, read back for the KPI cards' trend deltas.
        public DbSet<DashboardStatsSnapshot> DashboardStatsSnapshots { get; set; }

        // Personal reminders - own by AssignedTo/CreatedBy, no Space involved (flat, like Timesheets/HR).
        public DbSet<Reminder> Reminders { get; set; }

        // Reports' first Domain entities - persisted export history + recurring schedules.
        public DbSet<ReportExportHistory> ReportExportHistories { get; set; }
        public DbSet<ScheduledReport> ScheduledReports { get; set; }

        // Required by IDataProtectionKeyContext - lets the Data Protection key ring live in this same
        // Postgres database, shared across instances by construction (see ServiceCollectionExtensions).
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Postgres' timestamptz columns require DateTime values to be UTC-kinded. Several DateTime
            // properties (e.g. Project.StartDate/EndDate, ProjectTask.DueDate) are set straight from API
            // request DTOs and arrive as DateTimeKind.Unspecified from JSON deserialization. Every DateTime
            // in this schema is either a server timestamp (already UtcNow) or a date-only business field
            // where time-of-day doesn't matter, so treating unspecified values as UTC (not converting from
            // local time) is the correct semantic here. Applying this via ConfigureConventions (rather than
            // looping over properties in OnModelCreating) keeps the compiled model deterministic across
            // builds, which EF Core's migration-drift check requires.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Column constraints - moved here from [Required]/[StringLength] DataAnnotations that used
            // to live directly on the Domain entities (a persistence concern that had leaked into Domain
            // by convention: EF Core honored them for schema generation even though OnModelCreating never
            // referenced them). Domain now carries zero persistence attributes; every NOT NULL/varchar(n)
            // constraint EF actually enforces is declared explicitly here instead. [EmailAddress] on
            // User.Email was dropped outright rather than translated - it's a format check, not a schema
            // constraint, and RegisterRequestDto/LoginRequestDto/CreateUserDto/UpdateUserProfileDto's
            // FluentValidation rules already enforce it on every write path.
            modelBuilder.Entity<User>(e =>
            {
                e.Property(u => u.Name).IsRequired().HasMaxLength(100);
                e.Property(u => u.Email).IsRequired();
                e.Property(u => u.Role).IsRequired();
                e.Property(u => u.Position).HasMaxLength(100);
                e.Property(u => u.PasswordHash).IsRequired();
                // Defaulting true retroactively forces a reset on every pre-existing real account when
                // this column is added - the 6 documented demo accounts below are explicitly pinned back
                // to false so they keep working exactly as documented.
                e.Property(u => u.MustChangePassword).HasDefaultValue(true);
            });

            modelBuilder.Entity<Project>(e =>
            {
                e.Property(p => p.Name).IsRequired().HasMaxLength(200);
                e.Property(p => p.Description).HasMaxLength(1000);
                e.Property(p => p.Status).IsRequired();
                e.Property(p => p.Priority).IsRequired();
            });

            modelBuilder.Entity<ProjectTask>(e =>
            {
                e.Property(t => t.Title).IsRequired().HasMaxLength(200);
                e.Property(t => t.Description).HasMaxLength(1000);
                e.Property(t => t.Status).IsRequired();
                e.Property(t => t.Priority).IsRequired();
            });

            modelBuilder.Entity<Tag>(e =>
            {
                e.Property(t => t.Name).IsRequired().HasMaxLength(50);
                e.Property(t => t.Color).HasMaxLength(7);
            });

            modelBuilder.Entity<Attachment>(e =>
            {
                e.Property(a => a.FileName).IsRequired().HasMaxLength(255);
                e.Property(a => a.FilePath).IsRequired().HasMaxLength(255);
                e.Property(a => a.ContentType).HasMaxLength(100);
            });

            modelBuilder.Entity<Notification>(e =>
            {
                e.Property(n => n.Type).IsRequired().HasMaxLength(50);
                e.Property(n => n.Message).IsRequired().HasMaxLength(500);
            });

            modelBuilder.Entity<Permission>(e =>
            {
                e.Property(p => p.Resource).IsRequired().HasMaxLength(50);
                e.Property(p => p.Action).IsRequired().HasMaxLength(50);
                e.Property(p => p.Name).IsRequired().HasMaxLength(100);
                e.Property(p => p.Description).HasMaxLength(500);
            });

            modelBuilder.Entity<Role>(e =>
            {
                e.Property(r => r.Name).IsRequired().HasMaxLength(50);
                e.Property(r => r.Description).HasMaxLength(500);
            });

            modelBuilder.Entity<Space>(e =>
            {
                e.Property(s => s.Name).IsRequired().HasMaxLength(200);
                e.Property(s => s.Description).HasMaxLength(1000);
            });

            modelBuilder.Entity<RefreshToken>()
                .Property(rt => rt.TokenHash).IsRequired().HasMaxLength(200);

            modelBuilder.Entity<PasswordResetToken>()
                .Property(prt => prt.TokenHash).IsRequired().HasMaxLength(200);

            modelBuilder.Entity<VaultEntry>(e =>
            {
                e.Property(v => v.Name).IsRequired().HasMaxLength(200);
                e.Property(v => v.SystemOrUrl).HasMaxLength(500);
                e.Property(v => v.Username).HasMaxLength(200);
                e.Property(v => v.EncryptedSecret).IsRequired();
            });

            modelBuilder.Entity<VaultAuditLog>(e =>
            {
                e.Property(a => a.EntryNameSnapshot).IsRequired().HasMaxLength(200);
                e.Property(a => a.IpAddress).HasMaxLength(45);
                e.Property(a => a.Details).HasMaxLength(500);
            });

            modelBuilder.Entity<ActivityLogEntry>(e =>
            {
                e.Property(a => a.EntityType).IsRequired().HasMaxLength(50);
                e.Property(a => a.EntityNameSnapshot).IsRequired().HasMaxLength(200);
                e.Property(a => a.ActorNameSnapshot).IsRequired().HasMaxLength(200);
                e.Property(a => a.Action).IsRequired().HasMaxLength(50);
                e.Property(a => a.Details).HasMaxLength(500);
            });

            modelBuilder.Entity<ReportExportHistory>(e =>
            {
                e.Property(r => r.ReportType).IsRequired().HasMaxLength(50);
                e.Property(r => r.Format).IsRequired().HasMaxLength(20);
            });

            modelBuilder.Entity<ScheduledReport>(e =>
            {
                e.Property(s => s.ReportType).IsRequired().HasMaxLength(50);
                e.Property(s => s.Format).IsRequired().HasMaxLength(20);
            });

            modelBuilder.Entity<WikiPage>()
                .Property(w => w.Title).IsRequired().HasMaxLength(200);

            modelBuilder.Entity<LibraryFile>()
                .Property(f => f.FileName).IsRequired().HasMaxLength(255);

            modelBuilder.Entity<LibraryFileVersion>(e =>
            {
                e.Property(v => v.StoredPath).IsRequired().HasMaxLength(255);
                e.Property(v => v.ContentType).HasMaxLength(100);
            });

            modelBuilder.Entity<OnboardingTemplate>()
                .Property(t => t.Name).IsRequired().HasMaxLength(200);

            modelBuilder.Entity<OnboardingTemplateItem>()
                .Property(i => i.Title).IsRequired().HasMaxLength(300);

            modelBuilder.Entity<OnboardingChecklistItem>()
                .Property(i => i.Title).IsRequired().HasMaxLength(300);

            modelBuilder.Entity<Invoice>(e =>
            {
                e.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
                e.Property(i => i.ClientName).IsRequired().HasMaxLength(200);
                e.Property(i => i.Status).HasMaxLength(20);
                e.Property(i => i.OriginalFileName).HasMaxLength(255);
                e.Property(i => i.StoredFileName).HasMaxLength(255);
                e.Property(i => i.FileContentType).HasMaxLength(100);
            });

            modelBuilder.Entity<InvoiceLineItem>()
                .Property(li => li.Description).IsRequired().HasMaxLength(300);

            modelBuilder.Entity<InvoiceTemplate>(e =>
            {
                e.Property(t => t.Name).IsRequired().HasMaxLength(200);
                e.Property(t => t.ClientName).HasMaxLength(200);
                e.Property(t => t.OriginalFileName).IsRequired().HasMaxLength(255);
                e.Property(t => t.StoredFileName).IsRequired().HasMaxLength(255);
                e.Property(t => t.ContentType).HasMaxLength(100);
            });

            modelBuilder.Entity<Timesheet>()
                .Property(t => t.Status).HasMaxLength(20);

            modelBuilder.Entity<TimesheetEntry>()
                .Property(e => e.Description).HasMaxLength(500);

            modelBuilder.Entity<Idea>(e =>
            {
                e.Property(i => i.Title).IsRequired().HasMaxLength(200);
                e.Property(i => i.Description).IsRequired();
                e.Property(i => i.Status).HasMaxLength(20);
            });

            modelBuilder.Entity<IdeaAttachment>(e =>
            {
                e.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(255);
                e.Property(a => a.StoredFileName).IsRequired().HasMaxLength(255);
                e.Property(a => a.ContentType).HasMaxLength(100);
            });

            modelBuilder.Entity<CompanyEvent>(e =>
            {
                e.Property(ev => ev.Title).IsRequired().HasMaxLength(200);
                e.Property(ev => ev.EventType).IsRequired().HasMaxLength(20);
            });

            modelBuilder.Entity<NotificationPreference>()
                .Property(p => p.NotificationType).IsRequired().HasMaxLength(50);

            modelBuilder.Entity<DashboardWidgetAllowlist>()
                .Property(a => a.WidgetKey).IsRequired().HasMaxLength(50);

            modelBuilder.Entity<DashboardWidgetPreference>()
                .Property(p => p.WidgetKey).IsRequired().HasMaxLength(50);

            // Configure composite keys
            modelBuilder.Entity<ProjectUser>()
                .HasKey(pu => new { pu.ProjectId, pu.UserId });

            modelBuilder.Entity<ProjectTag>()
                .HasKey(pt => new { pt.ProjectId, pt.TagId });

            modelBuilder.Entity<TaskTag>()
                .HasKey(tt => new { tt.TaskId, tt.TagId });

            modelBuilder.Entity<WikiPageTag>()
                .HasKey(wt => new { wt.WikiPageId, wt.TagId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // Configure relationships
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Space)
                .WithMany()
                .HasForeignKey(p => p.SpaceId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProjectTask>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Space nests under itself - permission checks walk this chain upward (see
            // ISpacePermissionResolver). Restrict, not Cascade: deleting a parent Space should not
            // silently delete an entire subtree.
            modelBuilder.Entity<Space>()
                .HasOne(s => s.ParentSpace)
                .WithMany(s => s.ChildSpaces)
                .HasForeignKey(s => s.ParentSpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Space>()
                .HasOne(s => s.Creator)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // A grant's Space, however, cascades: deleting a Space should delete its own grant rows.
            modelBuilder.Entity<SpacePermission>()
                .HasOne(sp => sp.Space)
                .WithMany(s => s.Permissions)
                .HasForeignKey(sp => sp.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SpacePermission>()
                .HasOne(sp => sp.Role)
                .WithMany()
                .HasForeignKey(sp => sp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SpacePermission>()
                .HasOne(sp => sp.User)
                .WithMany()
                .HasForeignKey(sp => sp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SpacePermission>()
                .HasIndex(sp => new { sp.SpaceId, sp.RoleId, sp.UserId })
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(prt => prt.User)
                .WithMany()
                .HasForeignKey(prt => prt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: deleting a Space with live entries should be blocked or require an
            // explicit move in SpaceService, not a silent cascade of secret rows.
            modelBuilder.Entity<VaultEntry>()
                .HasOne(v => v.Space)
                .WithMany()
                .HasForeignKey(v => v.SpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VaultEntry>()
                .HasOne(v => v.Creator)
                .WithMany()
                .HasForeignKey(v => v.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VaultEntry>()
                .HasOne(v => v.Updater)
                .WithMany()
                .HasForeignKey(v => v.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // VaultEntryId is nullable and SetNull on delete so the audit trail survives entry deletion -
            // EntryNameSnapshot preserves what the entry was called even after the FK goes null.
            modelBuilder.Entity<VaultAuditLog>()
                .HasOne(a => a.VaultEntry)
                .WithMany()
                .HasForeignKey(a => a.VaultEntryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<VaultAuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ActorNameSnapshot means this survives the actor being deleted, same reasoning as
            // VaultAuditLog - but the FK itself still restricts, matching VaultAuditLog.UserId above.
            modelBuilder.Entity<ActivityLogEntry>()
                .HasOne(a => a.ActorUser)
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportExportHistory>()
                .HasOne(r => r.GeneratedByUser)
                .WithMany()
                .HasForeignKey(r => r.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScheduledReport>()
                .HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict, not Cascade: mirrors VaultEntry.Space - don't silently cascade-delete content
            // when a Space goes away.
            modelBuilder.Entity<WikiPage>()
                .HasOne(w => w.Space)
                .WithMany()
                .HasForeignKey(w => w.SpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ParentPageId nests pages within one Space for navigation only, not a permission boundary.
            modelBuilder.Entity<WikiPage>()
                .HasOne(w => w.ParentPage)
                .WithMany(w => w.ChildPages)
                .HasForeignKey(w => w.ParentPageId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WikiPage>()
                .HasOne(w => w.Creator)
                .WithMany()
                .HasForeignKey(w => w.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WikiPage>()
                .HasOne(w => w.Updater)
                .WithMany()
                .HasForeignKey(w => w.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Versions are owned by their page - deleting a page's history along with it is correct,
            // unlike VaultAuditLog which deliberately survives entry deletion.
            modelBuilder.Entity<WikiPageVersion>()
                .HasOne(v => v.WikiPage)
                .WithMany(w => w.Versions)
                .HasForeignKey(v => v.WikiPageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WikiPageVersion>()
                .HasOne(v => v.Editor)
                .WithMany()
                .HasForeignKey(v => v.EditedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WikiPageComment>()
                .HasOne(c => c.WikiPage)
                .WithMany(w => w.Comments)
                .HasForeignKey(c => c.WikiPageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WikiPageComment>()
                .HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthoredBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict, not Cascade: don't cascade-delete a whole reply thread from one row.
            modelBuilder.Entity<WikiPageComment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Labels are owned by their page (Cascade); the shared Tag row itself is never deleted by
            // this cascade (Restrict on the Tag side matches the ProjectTag/TaskTag convention).
            modelBuilder.Entity<WikiPageTag>()
                .HasOne(wt => wt.WikiPage)
                .WithMany(w => w.WikiPageTags)
                .HasForeignKey(wt => wt.WikiPageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WikiPageTag>()
                .HasOne(wt => wt.Tag)
                .WithMany(t => t.WikiPageTags)
                .HasForeignKey(wt => wt.TagId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict, not Cascade: mirrors VaultEntry.Space/WikiPage.Space - don't silently
            // cascade-delete files when a Space goes away.
            modelBuilder.Entity<LibraryFile>()
                .HasOne(f => f.Space)
                .WithMany()
                .HasForeignKey(f => f.SpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LibraryFile>()
                .HasOne(f => f.Creator)
                .WithMany()
                .HasForeignKey(f => f.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Versions are owned by their file - deleting a file's version history along with it is
            // correct, same reasoning as WikiPageVersion.
            modelBuilder.Entity<LibraryFileVersion>()
                .HasOne(v => v.LibraryFile)
                .WithMany(f => f.Versions)
                .HasForeignKey(v => v.LibraryFileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LibraryFileVersion>()
                .HasOne(v => v.Uploader)
                .WithMany()
                .HasForeignKey(v => v.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Template items are owned by their template.
            modelBuilder.Entity<OnboardingTemplateItem>()
                .HasOne(i => i.Template)
                .WithMany(t => t.Items)
                .HasForeignKey(i => i.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OnboardingChecklist>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: a template that already has checklists shouldn't be deletable out from under
            // them (checklist items already copied the titles, but the template link itself must stay).
            modelBuilder.Entity<OnboardingChecklist>()
                .HasOne(c => c.Template)
                .WithMany()
                .HasForeignKey(c => c.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OnboardingChecklistItem>()
                .HasOne(i => i.Checklist)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.ChecklistId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OnboardingChecklistItem>()
                .HasOne(i => i.Completer)
                .WithMany()
                .HasForeignKey(i => i.CompletedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Creator)
                .WithMany()
                .HasForeignKey(i => i.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvoiceLineItem>()
                .HasOne(li => li.Invoice)
                .WithMany(i => i.LineItems)
                .HasForeignKey(li => li.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceTemplate>()
                .HasOne(t => t.Creator)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict: a template that already seeded invoices shouldn't be deletable out from under
            // them (each invoice already copied the template's file onto its own disk path).
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.CreatedFromTemplate)
                .WithMany()
                .HasForeignKey(i => i.CreatedFromTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Timesheet>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Timesheet>()
                .HasOne(t => t.Approver)
                .WithMany()
                .HasForeignKey(t => t.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TimesheetEntry>()
                .HasOne(e => e.Timesheet)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: don't let a project deletion silently corrupt historical time entries.
            modelBuilder.Entity<TimesheetEntry>()
                .HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Idea>()
                .HasOne(i => i.Submitter)
                .WithMany()
                .HasForeignKey(i => i.SubmittedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict: don't let deleting the converted project orphan the idea's own history.
            modelBuilder.Entity<Idea>()
                .HasOne(i => i.ConvertedProject)
                .WithMany()
                .HasForeignKey(i => i.ConvertedProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IdeaComment>()
                .HasOne(c => c.Idea)
                .WithMany(i => i.Comments)
                .HasForeignKey(c => c.IdeaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IdeaComment>()
                .HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthoredBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Cascade: attachments are owned by their idea, unlike VaultEntry/WikiPage/LibraryFile's
            // Space (which is Restrict) - an idea's prototype files have nowhere else to live.
            modelBuilder.Entity<IdeaAttachment>()
                .HasOne(a => a.Idea)
                .WithMany()
                .HasForeignKey(a => a.IdeaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IdeaAttachment>()
                .HasOne(a => a.Uploader)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IdeaAttachmentAnnotation>()
                .HasOne(a => a.IdeaAttachment)
                .WithMany(f => f.Annotations)
                .HasForeignKey(a => a.IdeaAttachmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IdeaAttachmentAnnotation>()
                .HasOne(a => a.Author)
                .WithMany()
                .HasForeignKey(a => a.AuthoredBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyEvent>()
                .HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyEvent>()
                .HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Sparse-per-feature FK on Notification, mirroring TaskId/ProjectId - SetNull so a
            // notification survives deletion of the wiki page/idea it links to (matches VaultAuditLog's
            // reasoning: the notification's own text already captures what happened).
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.WikiPage)
                .WithMany()
                .HasForeignKey(n => n.WikiPageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Idea)
                .WithMany()
                .HasForeignKey(n => n.IdeaId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Reminder)
                .WithMany()
                .HasForeignKey(n => n.ReminderId)
                .OnDelete(DeleteBehavior.SetNull);

            // AssignedTo/Creator Restrict (mirrors ProjectTask.AssignedTo/Project.Creator) - deleting a
            // user shouldn't silently orphan their reminders. RelatedProject SetNull (mirrors
            // Attachment's optional project link) - a deleted project shouldn't block reminder deletion.
            // RecurrenceParent Restrict (self-ref, mirrors WikiPage.ParentPage) - occurrences reference
            // their origin; deleting the origin while occurrences still exist is blocked, not cascaded.
            modelBuilder.Entity<Reminder>()
                .HasOne(r => r.AssignedTo)
                .WithMany()
                .HasForeignKey(r => r.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reminder>()
                .HasOne(r => r.Creator)
                .WithMany()
                .HasForeignKey(r => r.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reminder>()
                .HasOne(r => r.RelatedProject)
                .WithMany()
                .HasForeignKey(r => r.RelatedProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Reminder>()
                .HasOne(r => r.RecurrenceParent)
                .WithMany()
                .HasForeignKey(r => r.RecurrenceParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NotificationPreference>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NotificationPreference>()
                .HasIndex(p => new { p.UserId, p.NotificationType })
                .IsUnique();

            modelBuilder.Entity<DashboardWidgetAllowlist>()
                .HasIndex(a => a.WidgetKey)
                .IsUnique();

            modelBuilder.Entity<DashboardWidgetPreference>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DashboardWidgetPreference>()
                .HasIndex(p => new { p.UserId, p.WidgetKey })
                .IsUnique();

            // Seed data
            // PasswordHash values below are pre-computed BCrypt hashes, not BCrypt.HashPassword(...) calls.
            // BCrypt generates a new random salt on every call, so calling it inline here would make this
            // HasData a "dynamic value" per EF Core's migration-drift check - the hash baked into the
            // migration would never match what OnModelCreating recomputes at each app startup, causing
            // 'PendingModelChangesWarning' to fail every run. These hashes correspond to admin123 (user 1),
            // manager123 (user 2), and member123 (users 3-6) respectively - dev-only seed credentials.
            const string adminPasswordHash = "$2a$11$Mg5gaJc5mAg7.lr7Nn6ire/RyTfOGkwGc3uid3jUmn43hN4NOBvbe";
            const string managerPasswordHash = "$2a$11$yaq0/sKRX14GeyRfr8CMrehqRat5ufDxjqz9wPdTaAIwzsBBBsb7C";
            const string memberPasswordHash = "$2a$11$Gz1fjWb8BDrfP21Iq2g9tO2fJjSJRLqMUO2N7/v0oboJNPiQVAkZy";

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Name = "Kolisa Mjobo",
                    Email = "kholisa@khoitech.Africa",
                    Role = "admin",
                    Position = "Full stack Developer",
                    PasswordHash = adminPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                },
                new User
                {
                    Id = 2,
                    Name = "Seati Moloi",
                    Email = "seati@khoitech.Africa",
                    Role = "manager",
                    Position = "Business Analyst",
                    PasswordHash = managerPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                },
                new User
                {
                    Id = 3,
                    Name = "Kenneth Mothobi",
                    Email = "kenneth@khoitech.Africa",
                    Role = "member",
                    Position = "System Analyst",
                    PasswordHash = memberPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                },
                new User
                {
                    Id = 4,
                    Name = "Thato",
                    Email = "thato@khoitech.Africa",
                    Role = "member",
                    Position = "Marketing Manager",
                    PasswordHash = memberPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                },
                new User
                {
                    Id = 5,
                    Name = "Metsing",
                    Email = "metsing@khoitech.Africa",
                    Role = "member",
                    Position = "Finance Manager",
                    PasswordHash = memberPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                },
                new User
                {
                    Id = 6,
                    Name = "Lebo",
                    Email = "Relebohile@khoitech.Africa",
                    Role = "member",
                    Position = "Client Support Manager",
                    PasswordHash = memberPasswordHash,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    MustChangePassword = false
                }
            );

            // Add seed data for Tags
            modelBuilder.Entity<Tag>().HasData(
                new Tag { Id = 1, Name = "web", Color = "#3B82F6", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Tag { Id = 2, Name = "mobile", Color = "#10B981", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Tag { Id = 3, Name = "design", Color = "#8B5CF6", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Tag { Id = 4, Name = "development", Color = "#F59E0B", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Tag { Id = 5, Name = "urgent", Color = "#EF4444", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Permission/Role/RolePermission/UserRole seed data reproduces today's [Authorize(Roles=...)]
            // behavior exactly (see the controller attributes this replaces, listed per permission below),
            // so swapping to policy-based authorization (2.5) is behavior-preserving on day one.
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Resource = "projects", Action = "create", Name = "projects.create", Description = "Create a project" },
                new Permission { Id = 2, Resource = "projects", Action = "edit", Name = "projects.edit", Description = "Edit a project" },
                new Permission { Id = 3, Resource = "projects", Action = "delete", Name = "projects.delete", Description = "Delete a project" },
                new Permission { Id = 4, Resource = "tasks", Action = "delete", Name = "tasks.delete", Description = "Delete a task" },
                new Permission { Id = 5, Resource = "attachments", Action = "delete", Name = "attachments.delete", Description = "Delete an attachment" },
                new Permission { Id = 6, Resource = "notifications", Action = "check_overdue", Name = "notifications.check_overdue", Description = "Manually trigger the overdue-task check" },
                new Permission { Id = 7, Resource = "reports", Action = "view", Name = "reports.view", Description = "View reports" },
                new Permission { Id = 8, Resource = "users", Action = "create", Name = "users.create", Description = "Create a user" },
                new Permission { Id = 9, Resource = "users", Action = "edit", Name = "users.edit", Description = "Edit a user's profile" },
                new Permission { Id = 10, Resource = "users", Action = "delete", Name = "users.delete", Description = "Deactivate a user" },
                new Permission { Id = 11, Resource = "users", Action = "manage_roles", Name = "users.manage_roles", Description = "Assign roles to a user" },
                new Permission { Id = 12, Resource = "dashboard", Action = "view", Name = "dashboard.view", Description = "View the dashboard" },
                new Permission { Id = 13, Resource = "spaces", Action = "manage", Name = "spaces.manage", Description = "Create root-level Spaces (vault categories, project areas, etc.)" },
                new Permission { Id = 14, Resource = "hr", Action = "view", Name = "hr.view", Description = "View any user's onboarding checklists" },
                new Permission { Id = 15, Resource = "hr", Action = "manage", Name = "hr.manage", Description = "Manage onboarding templates and any user's checklist items" },
                new Permission { Id = 16, Resource = "finance", Action = "view", Name = "finance.view", Description = "View invoices" },
                new Permission { Id = 17, Resource = "finance", Action = "manage", Name = "finance.manage", Description = "Create, edit, delete invoices and change their status" },
                new Permission { Id = 18, Resource = "timesheets", Action = "approve", Name = "timesheets.approve", Description = "Approve or reject another user's submitted timesheet" },
                new Permission { Id = 19, Resource = "timesheets", Action = "view_all", Name = "timesheets.view_all", Description = "View any user's timesheets" },
                new Permission { Id = 20, Resource = "ideas", Action = "manage", Name = "ideas.manage", Description = "Move an idea through review, delete any comment, convert an idea to a project" },
                new Permission { Id = 21, Resource = "calendar", Action = "manage", Name = "calendar.manage", Description = "Create, edit, delete company calendar events (birthdays are computed automatically)" },
                new Permission { Id = 22, Resource = "dashboard", Action = "manage", Name = "dashboard.manage", Description = "Control which dashboard widgets are available company-wide" },
                new Permission { Id = 23, Resource = "reminders", Action = "view_all", Name = "reminders.view_all", Description = "View and filter every user's reminders" },
                new Permission { Id = 24, Resource = "reminders", Action = "manage", Name = "reminders.manage", Description = "Assign a reminder to another user, bulk-act on any reminder" }
            );

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin", IsSystemRole = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Role { Id = 2, Name = "Manager", IsSystemRole = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Role { Id = 3, Name = "Member", IsSystemRole = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Admin: all permissions.
            modelBuilder.Entity<RolePermission>().HasData(
                Enumerable.Range(1, 24).Select(permissionId => new RolePermission { RoleId = 1, PermissionId = permissionId }).ToArray()
            );

            // Manager: projects.create/edit, tasks.delete, attachments.delete, notifications.check_overdue,
            // reports.view, users.edit, dashboard.view - matches every "admin,manager" attribute above.
            // Plus hr.view (14) - viewing onboarding checklists company-wide is a normal manager duty,
            // not admin-only (see plan Phase 9.3's "cover everyone" pass); hr.manage stays Admin-only.
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { RoleId = 2, PermissionId = 1 },
                new RolePermission { RoleId = 2, PermissionId = 2 },
                new RolePermission { RoleId = 2, PermissionId = 4 },
                new RolePermission { RoleId = 2, PermissionId = 5 },
                new RolePermission { RoleId = 2, PermissionId = 6 },
                new RolePermission { RoleId = 2, PermissionId = 7 },
                new RolePermission { RoleId = 2, PermissionId = 9 },
                new RolePermission { RoleId = 2, PermissionId = 12 },
                new RolePermission { RoleId = 2, PermissionId = 14 },
                new RolePermission { RoleId = 2, PermissionId = 18 },
                new RolePermission { RoleId = 2, PermissionId = 19 },
                new RolePermission { RoleId = 2, PermissionId = 20 },
                new RolePermission { RoleId = 2, PermissionId = 21 },
                new RolePermission { RoleId = 2, PermissionId = 23 }
            );

            // Member: dashboard.view only - members never held any role-restricted action before.
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { RoleId = 3, PermissionId = 12 }
            );

            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 1, RoleId = 1, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, // admin
                new UserRole { UserId = 2, RoleId = 2, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, // manager
                new UserRole { UserId = 3, RoleId = 3, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, // member
                new UserRole { UserId = 4, RoleId = 3, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, // member
                new UserRole { UserId = 5, RoleId = 3, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, // member
                new UserRole { UserId = 6, RoleId = 3, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }  // member
            );
        }
    }
}
