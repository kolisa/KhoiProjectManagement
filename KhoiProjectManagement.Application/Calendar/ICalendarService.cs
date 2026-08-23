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
    }
}
