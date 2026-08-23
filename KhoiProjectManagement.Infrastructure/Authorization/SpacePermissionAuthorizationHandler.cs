using System.Security.Claims;
using KhoiProjectManagement.Application.Authorization;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace KhoiProjectManagement.Infrastructure.Authorization
{
    // Resource-based authorization for Space-scoped entities (VaultEntry now, WikiPage/file-library
    // items later) - registered alongside PermissionAuthorizationHandler, not in place of it. Invoke
    // via IAuthorizationService.AuthorizeAsync(User, entity, new SpacePermissionRequirement(level))
    // from the service layer once the entity (and therefore its SpaceId) has been loaded; a
    // declarative [Authorize(Policy=...)] attribute can't see a specific row's SpaceId.
    public class SpacePermissionAuthorizationHandler : AuthorizationHandler<SpacePermissionRequirement, ISpaceScoped>
    {
        private readonly ISpacePermissionResolver _resolver;
        private readonly ILogger<SpacePermissionAuthorizationHandler> _logger;

        public SpacePermissionAuthorizationHandler(ISpacePermissionResolver resolver, ILogger<SpacePermissionAuthorizationHandler> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SpacePermissionRequirement requirement,
            ISpaceScoped resource)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return;
            }

            var roleIds = context.User.FindAll("roleId")
                .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value);

            var effectiveLevel = await _resolver.ResolveEffectiveLevelAsync(resource.SpaceId, userId, roleIds);

            if (effectiveLevel.HasValue && effectiveLevel.Value >= requirement.MinimumLevel)
            {
                context.Succeed(requirement);
            }
            else
            {
                // Space-scoped denials are otherwise invisible - no controller-level [Authorize] ever
                // fires for these, since the whole point of resource-based auth is that only the loaded
                // entity's SpaceId can decide. Worth a Warning: could be a misconfigured grant or a
                // caller probing access they don't have.
                _logger.LogWarning(
                    "Access denied: user {UserId} requested {RequiredLevel} on space {SpaceId}, effective level was {EffectiveLevel}",
                    userId, requirement.MinimumLevel, resource.SpaceId, effectiveLevel?.ToString() ?? "none");
            }
        }
    }
}
