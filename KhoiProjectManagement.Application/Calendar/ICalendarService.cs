using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface ICalendarService
    {
        Task<CalendarFeedDto> GetFeedAsync(DateTime from, DateTime to);
        Task<CompanyEventDto> CreateEventAsync(CreateCompanyEventDto dto, int createdBy);
        Task<bool> UpdateEventAsync(int id, CreateCompanyEventDto dto);
        Task<bool> DeleteEventAsync(int id);

        // Self always allowed; a different userId requires users.edit - checked by the controller via
        // the existing users.edit policy for "anyone else", same pattern as UpdateUserProfileDto (2.3).
        Task<bool> SetDateOfBirthAsync(int userId, DateTime dateOfBirth);

        // Issues a fresh opaque subscription token for this user (invalidating any previous one) and
        // returns the RAW token - the only time it's ever returned; only its hash is persisted (see
        // User.CalendarFeedTokenHash). The caller builds the full .ics subscribe URL around it.
        Task<string> RegenerateFeedTokenAsync(int userId);

        Task<bool> HasFeedTokenAsync(int userId);

        // Looks up the user by hashing the presented raw token and comparing against the stored hash;
        // returns the rendered iCalendar (RFC 5545) text, or null if the token doesn't match anyone.
        Task<string?> GetIcsFeedAsync(string rawToken);
    }
}
