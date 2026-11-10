namespace KhoiProjectManagement.Application
{
    public class PmProjectSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        // "SuperAdmin" for a SuperAdmin viewing a project they are not a member of.
        public string MyRole { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int OpenItems { get; set; }
    }

    public class PmMemberDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class SetProjectMemberDto
    {
        public string Role { get; set; } = ProjectRoles.Developer;
    }

    public class PmUserLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class WorkItemDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? StoryPoints { get; set; }
        public DateTime? StartedAt { get; set; }
        public int? SprintId { get; set; }
        public string? SprintName { get; set; }
        public int? AssignedToId { get; set; }
        public string? AssignedToName { get; set; }
        public int? ParentId { get; set; }
        public string? ParentTitle { get; set; }
        public int ChildCount { get; set; }
        // Progress over the leaf work underneath this item (zero when it has no children).
        public int RollupDone { get; set; }
        public int RollupTotal { get; set; }
        public int RollupPointsDone { get; set; }
        public int RollupPointsTotal { get; set; }
        public List<LabelDto> Labels { get; set; } = new();
        public DateTime DueDate { get; set; }
        // False when DueDate is only the project-end placeholder given to items created without a due date.
        public bool HasDueDate { get; set; }
        public double Rank { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // The few fields a picker needs - id and title, not the description and everything else on WorkItemDto.
    public class WorkItemLookupDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class WorkItemFilterDto
    {
        public string? Type { get; set; }
        public string? Status { get; set; }
        public int? AssigneeId { get; set; }
        public int? ParentId { get; set; }
        public int? SprintId { get; set; }
        // True restricts the list to items not in any sprint (the unplanned backlog).
        public bool? Unplanned { get; set; }
        public int? LabelId { get; set; }
        public string? Search { get; set; }
    }

    public class SaveWorkItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "Story";
        public string Status { get; set; } = "todo";
        public string Priority { get; set; } = "medium";
        public int? StoryPoints { get; set; }
        public int? AssignedToId { get; set; }
        public int? ParentId { get; set; }
        // Defaults to the project's end date when omitted (ProjectTask.DueDate is non-nullable).
        public DateTime? DueDate { get; set; }
    }

    public class RankWorkItemDto
    {
        // Place the item directly after this one; null moves it to the top of the backlog.
        public int? AfterItemId { get; set; }
    }

    public class PmAccessUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool PmAccess { get; set; }
        public bool PmOnly { get; set; }
    }

    public class SetPmAccessDto
    {
        public bool PmAccess { get; set; }
        public bool PmOnly { get; set; }
    }
}
