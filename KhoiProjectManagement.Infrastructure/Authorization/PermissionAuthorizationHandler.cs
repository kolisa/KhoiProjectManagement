using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace KhoiProjectManagement.Infrastructure.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ILogger<PermissionAuthorizationHandler> _logger;

        public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            // Unconditional bypass for the seeded Admin role (Role.IsSuperAdmin, carried as a JWT claim
            // at login) - covers every flat [Authorize(Policy="resource.action")] gate regardless of
            // that role's actual RolePermission grants, so admin access can't be edited away.
            if (context.User.HasClaim("superadmin", "true"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (context.User.HasClaim("permission", requirement.Permission))
            {
                context.Succeed(requirement);
            }
            else
            {
                // Debug, not Warning: this fires on every routine [Authorize(Policy="x.y")] miss (e.g. a
                // disabled UI affordance whose action still gets attempted) - high volume, low signal
                // individually. Kept available for troubleshooting a specific permission issue without
                // flooding Information-level logs in normal operation.
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                _logger.LogDebug("Permission denied: user {UserId} lacks {Permission}", userId, requirement.Permission);
            }

            return Task.CompletedTask;
        }
    }
}
