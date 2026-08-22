using System.Security.Claims;
using KhoiProjectManagement.Models;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;

namespace KhoiProjectManagementApi.Authorization
{
    // Resource-based authorization for Space-scoped entities (VaultEntry now, WikiPage/file-library
    // items later) - registered alongside PermissionAuthorizationHandler, not in place of it. Invoke
    // via IAuthorizationService.AuthorizeAsync(User, entity, new SpacePermissionRequirement(level))
    // from the service layer once the entity (and therefore its SpaceId) has been loaded; a
    // declarative [Authorize(Policy=...)] attribute can't see a specific row's SpaceId.
    public class SpacePermissionAuthorizationHandler : AuthorizationHandler<SpacePermissionRequirement, ISpaceScoped>
    {
        private readonly ISpacePermissionResolver _resolver;

        public SpacePermissionAuthorizationHandler(ISpacePermissionResolver resolver)
        {
            _resolver = resolver;
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
        }
    }
}
