namespace KhoiProjectManagement.Application.Abstractions
{
    // Narrow port for the one piece of ambient HTTP-request data a service needs without it being
    // threaded through every method signature (VaultAuditLog.IpAddress) - everything else (the caller's
    // identity) already arrives explicitly as a ClaimsPrincipal/userId parameter. Kept deliberately
    // smaller than a general-purpose ICurrentUser: add members here only when another Application
    // service needs them, rather than pre-building one out.
    public interface ICurrentRequestContext
    {
        // Null when there is no active HTTP request (e.g. a Quartz job, a unit test).
        string? RemoteIpAddress { get; }
    }
}
