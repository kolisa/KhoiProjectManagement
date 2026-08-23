using FluentValidation;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Authorization;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Infrastructure.Repositories;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using static System.Net.WebRequestMethods;

namespace KhoiProjectManagementApi.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<ProjectManagementContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            // Generic repository + unit of work - the persistence port Application depends on instead
            // of the concrete ProjectManagementContext, closing the DIP gap left by the earlier layering
            // pass. Open-generic registration means IRepository<T> resolves for any entity type without
            // per-entity registrations.
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IWikiSearchRepository, WikiSearchRepository>();

            // Request validation - every AbstractValidator<T> in Application/Validators/ is picked up
            // automatically by assembly scan; ValidationActionFilter (registered in Program.cs) resolves
            // IValidator<T> per action argument, so a new validator needs no controller changes.
            services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

            // Data Protection key ring persisted to the same Postgres DB (default is local filesystem,
            // which doesn't survive multi-instance deployment) - used by IVaultEncryptionService to
            // encrypt vault secrets at rest.
            services.AddDataProtection()
                .PersistKeysToDbContext<ProjectManagementContext>()
                .SetApplicationName("KhoiProjectManagement");

            // Authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudience = configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]!)),
                        ClockSkew = TimeSpan.Zero
                    };

                    // SignalR's browser transports (WebSockets/SSE) can't set an Authorization header, so
                    // the client sends the JWT as an "access_token" query param on the hub connection
                    // instead - this pulls it into the same bearer validation path used for normal API
                    // calls, scoped to just the hub path so it doesn't relax auth for REST endpoints.
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) &&
                                context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            // Authorization: flat, policy-based checks for fixed CRUD resources (PermissionPolicyProvider
            // synthesizes a policy for any "resource.action" name on the fly) plus resource-based checks
            // for Space-scoped items (SpacePermissionAuthorizationHandler) - both registered together,
            // one authorization pipeline serving two authorization styles.
            services.AddMemoryCache();
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, SpacePermissionAuthorizationHandler>();
            services.AddScoped<ISpacePermissionResolver, SpacePermissionResolver>();
            services.AddScoped<ISpaceService, SpaceService>();
            services.AddHttpContextAccessor();

            // Application-owned port over IHttpContextAccessor - VaultAuditService (and any future
            // consumer) depends on this instead of the ASP.NET Core hosting abstraction directly.
            services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();

            // Vault (first Space-scoped consumer)
            services.AddScoped<IVaultEncryptionService, VaultEncryptionService>();
            services.AddScoped<IVaultAuditService, VaultAuditService>();
            services.AddScoped<IVaultService, VaultService>();

            // Wiki (second Space-scoped consumer)
            services.AddScoped<IWikiService, WikiService>();

            // Wiki real-time presence + edit lock (WikiHub) - in-memory singleton, see
            // IWikiPresenceTracker for why this doesn't need to be distributed today.
            services.AddSignalR();
            services.AddSingleton<IWikiPresenceTracker, WikiPresenceTracker>();

            // Library (third Space-scoped consumer)
            services.AddScoped<ILibraryService, LibraryService>();

            // HR onboarding (flat, not Space-scoped)
            services.AddScoped<IHrService, HrService>();

            // Finance/invoicing (flat, Admin-only)
            services.AddScoped<IInvoiceService, InvoiceService>();

            // Runtime role/permission configuration
            services.AddScoped<IRoleService, RoleService>();

            // Timesheets (flat, ownership-based)
            services.AddScoped<ITimesheetService, TimesheetService>();

            // Ideas board (company-wide, flat)
            services.AddScoped<IIdeaService, IdeaService>();

            // Reminders (personal, ownership-based, flat)
            services.AddScoped<IReminderService, ReminderService>();

            // Company calendar (birthdays/events/promotions)
            services.AddScoped<ICalendarService, CalendarService>();

            // Configurable dashboard widgets
            services.AddScoped<IDashboardWidgetService, DashboardWidgetService>();

            // Services
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<ITaskService, TaskService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IAttachmentService, AttachmentService>();

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp", policy =>
                {
                    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                                       new[] { "http://localhost:3000", 
                                           "http://localhost:901", 
                                           "http://localhost:80", 
                                           "http://160.119.250.16/",
                                           "http://localhost",
                                           "http://localhost:905"};
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            return services;
        }
    }
}
