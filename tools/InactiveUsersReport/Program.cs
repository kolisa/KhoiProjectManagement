using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// One-off report: which active users haven't logged in recently, and do they even have anything
// to do in the system (projects/tasks assigned, documents uploaded)? See tools/InactiveUsersReport
// project comment for usage. Read-only - never writes anything.

var connectionStringArg = args.Length > 0 ? args[0] : null;
var thresholdDays = args.Length > 1 && int.TryParse(args[1], out var d) ? d : 14;

string connectionString;
if (!string.IsNullOrWhiteSpace(connectionStringArg))
{
    connectionString = connectionStringArg;
}
else
{
    // Same lookup ProjectManagementContextFactory uses for `dotnet ef` - the repo-committed
    // KhoiProjectManagementApi/appsettings.json (local dev Postgres by default).
    var apiSettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "KhoiProjectManagementApi");
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Path.GetFullPath(apiSettingsPath))
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();
    connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No connection string found - pass one explicitly as the first argument.");
}

var optionsBuilder = new DbContextOptionsBuilder<ProjectManagementContext>();
optionsBuilder.UseNpgsql(connectionString);
using var context = new ProjectManagementContext(optionsBuilder.Options);

var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

var candidates = await context.Users
    .Where(u => u.IsActive && (u.LastLoginAt == null || u.LastLoginAt < cutoff))
    .OrderBy(u => u.LastLoginAt)
    .ToListAsync();

Console.WriteLine($"Inactive-user report (threshold: {thresholdDays} days, cutoff: {cutoff:yyyy-MM-dd}, connection: {new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Host}/{new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database})");
Console.WriteLine($"Found {candidates.Count} candidate(s) out of {await context.Users.CountAsync(u => u.IsActive)} active users.");
Console.WriteLine();
Console.WriteLine($"{"Name",-25}{"Email",-35}{"LastLogin",-12}{"Onboarded?",-12}{"Projects",-10}{"Tasks",-8}{"Uploads",-9}Likely reason");
Console.WriteLine(new string('-', 140));

foreach (var user in candidates)
{
    var projectCount = await context.ProjectUsers.CountAsync(pu => pu.UserId == user.Id);
    var taskCount = await context.Tasks.CountAsync(t => t.AssignedToId == user.Id);
    var uploadCount = await context.LibraryFiles.CountAsync(f => f.CreatedBy == user.Id);

    var reason = user.MustChangePassword
        ? "Never finished onboarding (still on temp password)"
        : projectCount == 0 && taskCount == 0
            ? "No projects or tasks assigned - nothing to log in for"
            : uploadCount == 0
                ? "Has work assigned but never uploaded a document"
                : "Has work and documents - worth a direct check-in";

    var lastLogin = user.LastLoginAt?.ToString("yyyy-MM-dd") ?? "never";
    var onboarded = user.MustChangePassword ? "no" : "yes";

    Console.WriteLine($"{user.Name,-25}{user.Email,-35}{lastLogin,-12}{onboarded,-12}{projectCount,-10}{taskCount,-8}{uploadCount,-9}{reason}");
}
