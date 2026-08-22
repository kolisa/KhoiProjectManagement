using KhoiProjectManagementApi.Authorization;
using KhoiProjectManagementApi.Data;
using KhoiProjectManagementApi.Services;
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

            // Vault (first Space-scoped consumer)
            services.AddScoped<IVaultEncryptionService, VaultEncryptionService>();
            services.AddScoped<IVaultAuditService, VaultAuditService>();
            services.AddScoped<IVaultService, VaultService>();

            // Wiki (second Space-scoped consumer)
            services.AddScoped<IWikiService, WikiService>();

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
