using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Models
{
    public class ProjectUser
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
