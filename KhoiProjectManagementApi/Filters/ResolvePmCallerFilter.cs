using System.Security.Claims;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KhoiProjectManagementApi.Filters
{
    // Works out WHO is calling a Project Management endpoint once per request - including whether they are an
    // administrator because they are a Project Admin somewhere (a database fact, so it cannot live in the JWT) - and
    // hands it to the controller through HttpContext.Items. Apply with [TypeFilter(typeof(ResolvePmCallerFilter))].
    public sealed class ResolvePmCallerFilter : IAsyncActionFilter
    {
        public const string ItemKey = "pm.caller";

        private readonly IProjectAccessService _access;

        public ResolvePmCallerFilter(IProjectAccessService access) => _access = access;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && int.TryParse(idClaim.Value, out var userId))
            {
                context.HttpContext.Items[ItemKey] = await _access.ResolveCallerAsync(
                    userId, user.HasClaim("superadmin", "true"), user.HasClaim("permission", "pm.admin"));
            }
            await next();
        }
    }

    public static class PmCallerHttpContextExtensions
    {
        // The caller resolved by ResolvePmCallerFilter. Only valid on an action that carries the filter and an
        // authenticated user - which every PM action does.
        public static PmCaller GetPmCaller(this HttpContext context) =>
            context.Items[ResolvePmCallerFilter.ItemKey] as PmCaller
            ?? throw new InvalidOperationException("The Project Management caller was not resolved for this request.");
    }
}
