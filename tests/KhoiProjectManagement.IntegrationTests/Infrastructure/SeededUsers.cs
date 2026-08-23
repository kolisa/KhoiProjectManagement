namespace KhoiProjectManagement.IntegrationTests.Infrastructure
{
    // Mirrors KhoiProjectManagement.Infrastructure.DatabaseSeeder - reusing the seed data Program.cs
    // already creates on every ApiWebApplicationFactory startup instead of duplicating fixture data.
    public static class SeededUsers
    {
        // Admin role: all 24 seeded permissions (DatabaseSeeder/ProjectManagementContext.OnModelCreating).
        public static readonly (string Email, string Password) Admin = ("kholisa@khoitech.Africa", "admin123");

        // Member role: none of the projects.*/vault-relevant permissions - used to prove authorization
        // actually denies, not just that the happy path is wired up.
        public static readonly (string Email, string Password) Member = ("kenneth@khoitech.Africa", "member123");
    }
}
