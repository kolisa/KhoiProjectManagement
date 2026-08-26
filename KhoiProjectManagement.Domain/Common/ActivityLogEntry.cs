namespace KhoiProjectManagement.Domain
{
    // Cross-feature activity feed - deliberately not scoped under Projects/Finance/Ideas the way
    // VaultAuditLog lives under Vault, since this one entity is written to by several unrelated
    // services. Curated emission points only (see ActivityLogService callers), not every possible
    // action - an unbounded "log everything" feed is noise, not a feature.
    public class ActivityLogEntry : BaseEntity
    {
        public string EntityType { get; set; } = string.Empty;

        // Nullable so the entry survives the subject being deleted, same reasoning as
        // VaultAuditLog.VaultEntryId.
        public int? EntityId { get; set; }
        public string EntityNameSnapshot { get; set; } = string.Empty;

        public int ActorUserId { get; set; }
        public virtual User ActorUser { get; set; } = null!;
        public string ActorNameSnapshot { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
