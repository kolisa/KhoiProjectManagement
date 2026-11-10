using System.Security.Claims;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagementApi.Filters
{
    public static class PmCallerExtensions
    {
        // Who is calling a Project Management endpoint, from the JWT. An administrator is the Admin role (it carries the
        // "superadmin" claim); IsPmAdmin additionally covers the pm.admin permission. Being a Project Admin of some project
        // does NOT make anyone an administrator - that role only ever applies inside its own project.
        public static PmCaller ToPmCaller(this ClaimsPrincipal user)
        {
            var superAdmin = user.HasClaim("superadmin", "true");
            return new PmCaller(
                int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value),
                superAdmin,
                superAdmin || user.HasClaim("permission", "pm.admin"));
        }
    }
}
