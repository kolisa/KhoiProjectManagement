using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly ProjectManagementContext _context;

        public CalendarService(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<CalendarFeedDto> GetFeedAsync(DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;

            var usersWithBirthdays = await _context.Users
                .Where(u => u.DateOfBirth != null && u.IsActive)
                .Select(u => new { u.Id, u.Name, u.DateOfBirth })
                .ToListAsync();

            var birthdays = new List<BirthdayEntryDto>();
            for (var year = fromDate.Year; year <= toDate.Year; year++)
            {
                foreach (var u in usersWithBirthdays)
                {
                    var dob = u.DateOfBirth!.Value;
                    var candidate = SafeDate(year, dob.Month, dob.Day);
                    if (candidate >= fromDate && candidate <= toDate)
                    {
                        birthdays.Add(new BirthdayEntryDto
                        {
                            UserId = u.Id,
                            Name = u.Name,
                            Month = dob.Month,
                            Day = dob.Day
                        });
                    }
                }
            }

            var events = await _context.CompanyEvents
                .Include(e => e.Subject)
                .Include(e => e.Creator)
                .Where(e => e.EventDate >= fromDate && e.EventDate <= toDate)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return new CalendarFeedDto
            {
                Birthdays = birthdays,
                Events = events.Select(MapEvent).ToList()
            };
        }

        public async Task<CompanyEventDto> CreateEventAsync(CreateCompanyEventDto dto, int createdBy)
        {
            var companyEvent = new CompanyEvent
            {
                Title = dto.Title,
                Description = dto.Description,
                EventDate = dto.EventDate,
                EventType = dto.EventType,
                SubjectUserId = dto.SubjectUserId,
                CreatedBy = createdBy
            };

            _context.CompanyEvents.Add(companyEvent);
            await _context.SaveChangesAsync();

            var saved = await _context.CompanyEvents
                .Include(e => e.Subject)
                .Include(e => e.Creator)
                .FirstAsync(e => e.Id == companyEvent.Id);

            return MapEvent(saved);
        }

        public async Task<bool> UpdateEventAsync(int id, CreateCompanyEventDto dto)
        {
            var companyEvent = await _context.CompanyEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (companyEvent == null)
                return false;

            companyEvent.Title = dto.Title;
            companyEvent.Description = dto.Description;
            companyEvent.EventDate = dto.EventDate;
            companyEvent.EventType = dto.EventType;
            companyEvent.SubjectUserId = dto.SubjectUserId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteEventAsync(int id)
        {
            var companyEvent = await _context.CompanyEvents.FindAsync(id);
            if (companyEvent == null)
                return false;

            _context.CompanyEvents.Remove(companyEvent);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDateOfBirthAsync(int userId, DateTime dateOfBirth)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.DateOfBirth = dateOfBirth;
            await _context.SaveChangesAsync();
            return true;
        }

        // Clamps Feb 29 to Feb 28 in a non-leap year, so a leap-day birthday still resolves to a real
        // date every year instead of throwing.
        private static DateTime SafeDate(int year, int month, int day)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, Math.Min(day, daysInMonth));
        }

        private static CompanyEventDto MapEvent(CompanyEvent e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            EventDate = e.EventDate,
            EventType = e.EventType,
            SubjectUserId = e.SubjectUserId,
            SubjectName = e.Subject?.Name,
            CreatorName = e.Creator?.Name ?? "Unknown"
        };
    }
}
