using KhoiProjectManagement.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace KhoiProjectManagement.Infrastructure.Services
{
    // The only thing in this codebase allowed to touch IHttpContextAccessor directly - Application
    // depends on ICurrentRequestContext instead, matching the ICurrentUser pattern for hosting-framework
    // abstractions (see VaultAuditService, the one consumer today).
    public class HttpCurrentRequestContext : ICurrentRequestContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpCurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? RemoteIpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }
}
