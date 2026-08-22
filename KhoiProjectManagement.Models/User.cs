using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "member"; // admin, manager, member

        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // New PII for the company calendar feed (Phase 12) - the API only ever exposes month/day from
        // this, never the birth year, to anyone other than the user themselves or a users.edit caller.
        public DateTime? DateOfBirth { get; set; }

        // Navigation properties
        public virtual ICollection<ProjectUser> ProjectUsers { get; set; } = new List<ProjectUser>();
        public virtual ICollection<ProjectTask> AssignedTasks { get; set; } = new List<ProjectTask>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
