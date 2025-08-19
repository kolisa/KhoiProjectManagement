using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Models
{
    public class Attachment
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public int? TaskId { get; set; }
        public virtual ProjectTask? Task { get; set; }

        public int UploadedBy { get; set; }
        public virtual User UploadedByUser { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
