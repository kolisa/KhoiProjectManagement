using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class User : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "member"; // admin, manager, member

        public string Position { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Set on any account created with a server-generated temp password; cleared once the person
        // completes a password reset (forced or self-service - same AuthService.ResetPasswordAsync path).
        public bool MustChangePassword { get; set; }

        // New PII for the company calendar feed (Phase 12) - the API only ever exposes month/day from
        // this, never the birth year, to anyone other than the user themselves or a users.edit caller.
        public DateTime? DateOfBirth { get; set; }

        // SHA-256 hex hash of an opaque per-user calendar-subscription token (never the raw token
        // itself - same "hashed, stored, revocable" convention as RefreshToken/PasswordResetToken).
        // Null until the user first generates a subscription link; regenerating replaces this and
        // invalidates the old link. See CalendarService.RegenerateFeedTokenAsync/GetIcsFeedAsync.
        public string? CalendarFeedTokenHash { get; set; }

        // Self-referencing "reports to" link for the team org chart - nullable (top of the chart has
        // no manager). Mutable after creation (unlike Space.ParentSpaceId), so UserService validates
        // against self-reference and cycles on every write - see UserService.ValidateManagerAsync.
        public int? ManagerId { get; set; }
        public virtual User? Manager { get; set; }

        // Navigation properties
        public virtual ICollection<ProjectUser> ProjectUsers { get; set; } = new List<ProjectUser>();
        public virtual ICollection<ProjectTask> AssignedTasks { get; set; } = new List<ProjectTask>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
