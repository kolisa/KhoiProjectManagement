using KhoiProjectManagement.Models;
using Microsoft.AspNetCore.Authorization;

namespace KhoiProjectManagementApi.Authorization
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
