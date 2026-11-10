namespace KhoiProjectManagement.Domain
{
    // A status transition a project has switched OFF (e.g. todo -> completed). Absence of a row means
    // the transition is allowed, so a project with no rows behaves exactly as before the workflow
    // feature existed.
    public class WorkflowRestriction : BaseEntity
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;
        public string FromStatus { get; set; } = string.Empty;
        public string ToStatus { get; set; } = string.Empty;
    }
}
