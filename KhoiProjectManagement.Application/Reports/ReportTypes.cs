namespace KhoiProjectManagement.Application
{
    public static class ReportTypes
    {
        public const string ProjectSummary = "ProjectSummary";
        public const string TeamPerformance = "TeamPerformance";
        public const string OverdueTasks = "OverdueTasks";

        public static readonly string[] All = { ProjectSummary, TeamPerformance, OverdueTasks };

        public static bool IsValid(string reportType) => All.Contains(reportType);
    }

    public static class ReportFormats
    {
        public const string Csv = "Csv";
        public const string Pdf = "Pdf";

        public static readonly string[] All = { Csv, Pdf };

        public static bool IsValid(string format) => All.Contains(format);
    }
}
