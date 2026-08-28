using KhoiProjectManagement.Infrastructure;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Quartz;
using Quartz;
using KhoiProjectManagementApi.Extensions;
using KhoiProjectManagementApi.Filters;
using KhoiProjectManagementApi.Hubs;
using KhoiProjectManagementApi.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Bootstrap logger: active only until the host is built, so startup failures (bad config, DB
// unreachable during migration, etc.) are never lost before the real Serilog pipeline is wired up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// Required once at startup or every QuestPDF.Fluent.Document.GeneratePdf() call throws. Community is
// free for organizations under $1M USD annual gross revenue - fine for this internal tool.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

try
{
    Log.Information("Starting KhoiProjectManagementApi");

    var builder = WebApplication.CreateBuilder(args);

    // Reconfigures Log.Logger from the "Serilog" section in appsettings*.json (sinks, levels,
    // enrichers - see appsettings.json) plus anything registered in the DI container via ReadFrom.Services.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddScoped<ValidationActionFilter>();
    builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>());
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Project Management API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
            }
        });
    });
    // Background jobs (Quartz.NET) - see KhoiProjectManagement.Quartz for the IJob implementations.
    // Recurring triggers are deliberately started 1 hour out rather than "now" - the trigger's start
    // time is computed eagerly right here (not lazily at actual scheduler startup), and this app's
    // migration+seed step can take upwards of ten seconds. A trigger whose nominal start time has
    // already passed by the time the scheduler actually starts gets raced between normal trigger
    // acquisition and Quartz's misfire recovery pass, and was observed firing twice as a result. An
    // hour of headroom makes that race impossible; the "check on boot" behavior the old
    // BackgroundServices had is reproduced deliberately below via TriggerJob, not via trigger timing.
    var overdueJobKey = new JobKey("OverdueTaskCheck");
    var reminderJobKey = new JobKey("ReminderDueCheck");
    var dashboardSnapshotJobKey = new JobKey("DashboardSnapshot");
    var scheduledReportJobKey = new JobKey("ScheduledReport");
    var firstRecurrence = DateBuilder.FutureDate(1, IntervalUnit.Hour);

    builder.Services.AddQuartz(q =>
    {
        q.AddJob<OverdueTaskCheckJob>(opts => opts.WithIdentity(overdueJobKey));
        q.AddTrigger(opts => opts
            .ForJob(overdueJobKey)
            .WithIdentity("OverdueTaskCheck-trigger")
            .StartAt(firstRecurrence)
            .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));

        q.AddJob<ReminderDueCheckJob>(opts => opts.WithIdentity(reminderJobKey));
        q.AddTrigger(opts => opts
            .ForJob(reminderJobKey)
            .WithIdentity("ReminderDueCheck-trigger")
            .StartAt(firstRecurrence)
            .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));

        q.AddJob<DashboardSnapshotJob>(opts => opts.WithIdentity(dashboardSnapshotJobKey));
        q.AddTrigger(opts => opts
            .ForJob(dashboardSnapshotJobKey)
            .WithIdentity("DashboardSnapshot-trigger")
            .StartAt(firstRecurrence)
            .WithSimpleSchedule(s => s.WithIntervalInHours(24).RepeatForever()));

        q.AddJob<ScheduledReportJob>(opts => opts.WithIdentity(scheduledReportJobKey));
        q.AddTrigger(opts => opts
            .ForJob(scheduledReportJobKey)
            .WithIdentity("ScheduledReport-trigger")
            .StartAt(firstRecurrence)
            .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
    });
    builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

    // Needed once the API sits behind a reverse proxy (nginx/Caddy) on the Linux host - without this,
    // HttpContext.Connection.RemoteIpAddress/Request.Scheme reflect the proxy hop, not the real client,
    // which breaks UseHttpsRedirection()'s correctness and any IP-based logging. KnownNetworks/
    // KnownProxies are cleared because the proxy's address isn't fixed/known ahead of time here - this
    // is only safe because Kestrel is expected to be reachable exclusively through that proxy, never
    // directly from the public internet.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    // Configure middleware
    if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        // This is an API-only project - nothing else renders at "/", so send anyone who hits the bare
        // host URL (typing it in by hand, an IDE's "open in browser" on the launch profile, etc.)
        // straight to Swagger instead of a blank 404.
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }

    app.UseForwardedHeaders();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors("AllowReactApp");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<WikiHub>("/hubs/wiki");

    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ProjectManagementContext>();

        // Defaults to true (appsettings.json) so local dev/docker-compose keeps auto-migrating and
        // seeding exactly as before with zero setup. appsettings.Production.json.example sets this to
        // false: a shared production database shouldn't get schema changes applied - or, worse, the
        // documented seeded demo accounts (kholisa@khoitech.Africa/admin123 etc. - see README.md)
        // inserted - just because an API instance happened to (re)start. Multiple instances starting
        // concurrently would also race to apply migrations against the same database. Production
        // should run `dotnet ef database update` as its own deliberate, reviewed release step instead.
        if (builder.Configuration.GetValue("App:AutoMigrateOnStartup", true))
        {
            await context.Database.MigrateAsync();
            await DatabaseSeeder.SeedAsync(context);
            Log.Information("Database initialized and seeded successfully");
        }
        else
        {
            Log.Information("Skipping automatic migration/seed - App:AutoMigrateOnStartup is false. " +
                "Run 'dotnet ef database update' as a separate step before starting the API against this database.");
        }

        // Ad-hoc immediate run, queued before the scheduler starts (see the AddQuartz comment above for
        // why this isn't done via the trigger's start time) - reproduces the old BackgroundServices'
        // "check immediately on boot" behavior deliberately and exactly once.
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.TriggerJob(overdueJobKey);
        await scheduler.TriggerJob(reminderJobKey);
        await scheduler.TriggerJob(dashboardSnapshotJobKey);
        await scheduler.TriggerJob(scheduledReportJobKey);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Top-level statements generate an internal Program class by default - WebApplicationFactory<Program>
// (used by the Integration/Functional test projects) needs it public to bootstrap the app in-process.
// No behavior change; this is Microsoft's documented pattern for testing minimal-hosting apps.
public partial class Program { }
