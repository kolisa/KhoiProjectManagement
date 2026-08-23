using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace KhoiProjectManagement.Infrastructure.Authorization
{
    // Synthesizes an authorization policy for any "resource.action"-shaped policy name on the fly, so
    // permissions never need hand-registering with AddAuthorization(options => options.AddPolicy(...))
    // in Program.cs - [Authorize(Policy = "projects.delete")] just works as soon as the Permission row
    // exists.
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.Contains('.'))
            {
                return await _fallbackPolicyProvider.GetPolicyAsync(policyName);
            }

            var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new PermissionRequirement(policyName));
            return policy.Build();
        }
    }
}
