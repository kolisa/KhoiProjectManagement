using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;

namespace KhoiProjectManagement.Application.Authorization
{
    public class SpacePermissionRequirement : IAuthorizationRequirement
    {
        public PermissionLevel MinimumLevel { get; }

        public SpacePermissionRequirement(PermissionLevel minimumLevel)
        {
            MinimumLevel = minimumLevel;
        }
    }
}
