using System.Security.Claims;
using KhoiProjectManagement.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Authorization
{
    public class PermissionAuthorizationHandlerTests
    {
        private readonly ILogger<PermissionAuthorizationHandler> _logger = Substitute.For<ILogger<PermissionAuthorizationHandler>>();

        private PermissionAuthorizationHandler CreateSut() => new(_logger);

        private static AuthorizationHandlerContext CreateContext(PermissionRequirement requirement, params Claim[] claims)
        {
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            return new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
        }

        [Fact]
        public async Task HandleRequirementAsync_WhenUserHasMatchingPermissionClaim_Succeeds()
        {
            var requirement = new PermissionRequirement("projects.delete");
            var context = CreateContext(requirement, new Claim("permission", "projects.delete"));

            await CreateSut().HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_WhenUserLacksPermissionClaim_Fails()
        {
            var requirement = new PermissionRequirement("projects.delete");
            var context = CreateContext(requirement, new Claim("permission", "tasks.view"));

            await CreateSut().HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_WhenUserHasSuperAdminClaim_SucceedsRegardlessOfPermissionClaims()
        {
            var requirement = new PermissionRequirement("projects.delete");
            var context = CreateContext(requirement, new Claim("superadmin", "true"));

            await CreateSut().HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }
}
