namespace KhoiProjectManagement.Application
{
    // Maps to 403 in ErrorHandlingMiddleware. Distinct from UnauthorizedAccessException (401), which the
    // frontend treats as an expired session and answers by logging the user out.
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "You do not have permission to do that.") : base(message) { }
    }
}
