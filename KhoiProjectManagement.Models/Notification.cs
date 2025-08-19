using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty; // overdue, assignment, completion, etc.

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public int? TaskId { get; set; }
        public virtual ProjectTask? Task { get; set; }

        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}
