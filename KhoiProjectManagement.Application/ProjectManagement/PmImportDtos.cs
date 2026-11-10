namespace KhoiProjectManagement.Application
{
    public class ImportMemberRowDto
    {
        public string Email { get; set; } = string.Empty;
        // Free text such as "Scrum Master", "dev" or "QA" - see ProjectRoles.Parse. Blank means Developer.
        public string? Role { get; set; }
    }

    public class ImportMembersDto
    {
        public List<ImportMemberRowDto> Rows { get; set; } = new();
        // SuperAdmin / pm.admin only: also switch on Project Management module access for people who lack it.
        public bool GrantModuleAccess { get; set; }
        // With GrantModuleAccess: confine newly granted accounts to the PM module.
        public bool ModuleOnly { get; set; }
        // Report what would happen without changing anything.
        public bool DryRun { get; set; }
    }

    public static class ImportOutcomes
    {
        public const string Added = "Added";
        public const string Updated = "Updated";
        public const string Unchanged = "Unchanged";
        public const string NotFound = "NotFound";
        public const string Inactive = "Inactive";
        public const string InvalidEmail = "InvalidEmail";
        public const string InvalidRole = "InvalidRole";
        public const string NoModuleAccess = "NoModuleAccess";
        public const string Duplicate = "Duplicate";
    }

    public class ImportMemberResultRowDto
    {
        public int Row { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        // Whether this row changes (or would change) the roster.
        public bool Applied { get; set; }
    }

    public class ImportMembersResultDto
    {
        public bool DryRun { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int ModuleAccessGranted { get; set; }
        public List<ImportMemberResultRowDto> Rows { get; set; } = new();
    }
}
