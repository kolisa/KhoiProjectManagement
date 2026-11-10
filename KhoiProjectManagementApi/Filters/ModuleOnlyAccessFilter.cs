using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KhoiProjectManagementApi.Filters
{
    // Marks a controller/action that a Project-Management-module-only account may call (the PM API
    // itself, plus the auth endpoints needed to stay signed in).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AllowModuleOnlyAttribute : Attribute { }

    // Global filter confining accounts flagged User.PmOnly (carried as the "pm_only" JWT claim) to
    // [AllowModuleOnly] endpoints - every other controller answers 403 without needing a per-controller
    // change, so a newly added module can't accidentally leak to module-only users. Fail-closed by
    // design: the default for a flagged account is "denied unless explicitly allowed". The claim is
    // refreshed with each access token, so a change to the flag takes effect within the token lifetime.
    public class ModuleOnlyAccessFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true || !user.HasClaim("pm_only", "true"))
                return;

            var allowed = context.ActionDescriptor.EndpointMetadata.OfType<AllowModuleOnlyAttribute>().Any();
            if (!allowed)
                context.Result = new ObjectResult(new { message = "Your account only has access to the Project Management module." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
        }
    }
}
