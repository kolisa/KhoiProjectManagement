using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace KhoiProjectManagement.Application
{
    public class CalendarService : ICalendarService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<CompanyEvent> _eventRepo;
        private readonly IUnitOfWork _unitOfWork;

        public CalendarService(IRepository<User> userRepo, IRepository<CompanyEvent> eventRepo, IUnitOfWork unitOfWork)
        {
            _userRepo = userRepo;
            _eventRepo = eventRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<CalendarFeedDto> GetFeedAsync(DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;

            var usersWithBirthdays = await _userRepo.Query()
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

            var events = await _eventRepo.Query()
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

            _eventRepo.Add(companyEvent);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _eventRepo.Query()
                .Include(e => e.Subject)
                .Include(e => e.Creator)
                .FirstAsync(e => e.Id == companyEvent.Id);

            return MapEvent(saved);
        }

        public async Task<bool> UpdateEventAsync(int id, CreateCompanyEventDto dto)
        {
            var companyEvent = await _eventRepo.Query().FirstOrDefaultAsync(e => e.Id == id);
            if (companyEvent == null)
                return false;

            companyEvent.Title = dto.Title;
            companyEvent.Description = dto.Description;
            companyEvent.EventDate = dto.EventDate;
            companyEvent.EventType = dto.EventType;
            companyEvent.SubjectUserId = dto.SubjectUserId;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteEventAsync(int id)
        {
            var companyEvent = await _eventRepo.FindAsync(id);
            if (companyEvent == null)
                return false;

            _eventRepo.Remove(companyEvent);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetDateOfBirthAsync(int userId, DateTime dateOfBirth)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user == null)
                return false;

            user.DateOfBirth = dateOfBirth;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        // Clamps Feb 29 to Feb 28 in a non-leap year, so a leap-day birthday still resolves to a real
        // date every year instead of throwing.
        private static DateTime SafeDate(int year, int month, int day)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, Math.Min(day, daysInMonth));
        }

        public async Task<string> RegenerateFeedTokenAsync(int userId)
        {
            var user = await _userRepo.FindAsync(userId) ?? throw new InvalidOperationException("User not found.");

            // Hex, not base64 - this token sits directly in a URL query string that gets pasted into
            // Outlook/Google/Apple Calendar, so it needs to be URL-safe with zero encoding required.
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.CalendarFeedTokenHash = Hash(rawToken);
            await _unitOfWork.SaveChangesAsync();

            return rawToken;
        }

        public async Task<bool> HasFeedTokenAsync(int userId)
        {
            var user = await _userRepo.FindAsync(userId);
            return user?.CalendarFeedTokenHash != null;
        }

        public async Task<string?> GetIcsFeedAsync(string rawToken)
        {
            var hash = Hash(rawToken);
            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.CalendarFeedTokenHash == hash);
            if (user == null)
                return null;

            var now = DateTime.UtcNow.Date;
            var from = now.AddDays(-30);
            var to = now.AddDays(365);

            var companyEvents = await _eventRepo.Query()
                .Where(e => e.EventDate >= from && e.EventDate <= to)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            var icsEvents = companyEvents.Select(e => new IcsFeedBuilder.IcsEvent(
                Uid: $"event-{e.Id}",
                Date: e.EventDate,
                Summary: $"[{e.EventType}] {e.Title}",
                Description: e.Description
            ));

            var usersWithBirthdays = await _userRepo.Query()
                .Where(u => u.IsActive && u.DateOfBirth != null)
                .Select(u => new { u.Id, u.Name, u.DateOfBirth })
                .ToListAsync();

            var icsBirthdays = usersWithBirthdays.Select(u =>
            {
                var dob = u.DateOfBirth!.Value;
                // Next occurrence (this year if it hasn't passed yet, else next year) - only affects
                // which DTSTART the RRULE:YEARLY starts counting from, never exposes the real birth
                // year (see IcsFeedBuilder's comment).
                var thisYear = SafeDate(now.Year, dob.Month, dob.Day);
                var nextOccurrence = thisYear >= now ? thisYear : SafeDate(now.Year + 1, dob.Month, dob.Day);
                return new IcsFeedBuilder.IcsBirthday(Uid: $"birthday-{u.Id}", NextOccurrence: nextOccurrence, Summary: $"{u.Name}'s birthday");
            });

            return IcsFeedBuilder.Build(icsEvents, icsBirthdays);
        }

        private static string Hash(string value)
        {
            var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
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
