namespace KhoiProjectManagement.Application
{
    public enum PmAction { View, CreateItem, EditItem, DeleteItem, RankItem, ManageMembers, ManageBoard, ManageSprint }

    // The caller of a PM-module operation; IsSuperAdmin comes from the "superadmin" JWT claim.
    public sealed record PmCaller(int UserId, bool IsSuperAdmin);

    public static class ProjectRoles
    {
        public const string ProjectAdmin = "ProjectAdmin";
        public const string ProjectManager = "ProjectManager";
        public const string ScrumMaster = "ScrumMaster";
        public const string Developer = "Developer";
        public const string Tester = "Tester";
        public const string Viewer = "Viewer";

        public static readonly string[] All = { ProjectAdmin, ProjectManager, ScrumMaster, Developer, Tester, Viewer };

        private static readonly Dictionary<string, PmAction[]> Matrix = new()
        {
            [ProjectAdmin] = Enum.GetValues<PmAction>(),
            [ProjectManager] = new[] { PmAction.View, PmAction.CreateItem, PmAction.EditItem, PmAction.DeleteItem, PmAction.RankItem, PmAction.ManageBoard, PmAction.ManageSprint },
            [ScrumMaster] = new[] { PmAction.View, PmAction.CreateItem, PmAction.EditItem, PmAction.DeleteItem, PmAction.RankItem, PmAction.ManageBoard, PmAction.ManageSprint },
            [Developer] = new[] { PmAction.View, PmAction.CreateItem, PmAction.EditItem, PmAction.RankItem },
            [Tester] = new[] { PmAction.View, PmAction.CreateItem, PmAction.EditItem },
            [Viewer] = new[] { PmAction.View },
        };

        public static bool Allows(string? role, PmAction action) =>
            role != null && Matrix.TryGetValue(role, out var actions) && actions.Contains(action);
    }

    // Epic > Feature > Story > Task/Bug > Subtask. A parent must sit at a strictly higher level than its
    // child, which both enforces the hierarchy and makes cycles impossible. Legacy Meeting/Milestone/
    // Review items stay standalone (level -1: no parent, no children).
    public static class WorkItemHierarchy
    {
        public static readonly string[] Types =
            { "Epic", "Feature", "Story", "Task", "Bug", "Subtask", "Meeting", "Milestone", "Review" };

        public static int Level(string type) => type switch
        {
            "Epic" => 0,
            "Feature" => 1,
            "Story" => 2,
            "Task" or "Bug" => 3,
            "Subtask" => 4,
            _ => -1,
        };

        // Returns an error message, or null when the (type, parent type) pairing is allowed.
        public static string? ValidateParent(string childType, string? parentType)
        {
            var childLevel = Level(childType);
            if (parentType == null)
                return childType == "Subtask" ? "A Subtask must have a parent item." : null;
            if (childLevel < 0)
                return $"{childType} items cannot have a parent.";
            var parentLevel = Level(parentType);
            if (parentLevel < 0 || parentLevel >= childLevel)
                return $"A {childType} cannot be placed under a {parentType}.";
            return null;
        }
    }
}
