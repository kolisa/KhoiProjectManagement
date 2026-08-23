using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class Attachment : BaseEntity
    {
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

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
