using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class NotificationServiceTests
    {
        private readonly IRepository<Notification> _notificationRepo = Substitute.For<IRepository<Notification>>();
        private readonly IRepository<NotificationPreference> _preferenceRepo = Substitute.For<IRepository<NotificationPreference>>();
        private readonly IRepository<ProjectTask> _taskRepo = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private NotificationService CreateSut(int thresholdDays = 3, int repeatDays = 7)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Notifications:LoginReminderThresholdDays"] = thresholdDays.ToString(),
                    ["Notifications:LoginReminderRepeatDays"] = repeatDays.ToString(),
                })
                .Build();
            return new NotificationService(_notificationRepo, _preferenceRepo, _taskRepo, _userRepo, _unitOfWork, _emailService, config);
        }

        private static User PendingUser(int id, int createdDaysAgo) => new()
        {
            Id = id,
            Name = $"User {id}",
            Email = $"user{id}@khoitech.africa",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo)
        };

        [Fact]
        public async Task CheckInactiveUsersAsync_WhenPastThresholdAndNeverReminded_CreatesNotificationAndSendsEmail()
        {
            var user = PendingUser(1, createdDaysAgo: 5);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());

            await CreateSut().CheckInactiveUsersAsync();

            _notificationRepo.Received(1).Add(Arg.Is<Notification>(n => n.UserId == 1 && n.Type == NotificationTypes.LoginReminder));
            await _emailService.Received(1).SendLoginReminderEmailAsync(user.Email, user.Name, 5);
        }

        [Fact]
        public async Task CheckInactiveUsersAsync_WhenRemindedWithinRepeatWindow_DoesNotRemindAgain()
        {
            var user = PendingUser(1, createdDaysAgo: 5);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.LoginReminder, CreatedAt = DateTime.UtcNow.AddDays(-2) }
            }.BuildMock());

            await CreateSut(repeatDays: 7).CheckInactiveUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
            await _emailService.DidNotReceive().SendLoginReminderEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task CheckInactiveUsersAsync_WhenPreviousReminderIsOutsideRepeatWindow_RemindsAgain()
        {
            var user = PendingUser(1, createdDaysAgo: 20);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.LoginReminder, CreatedAt = DateTime.UtcNow.AddDays(-10) }
            }.BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());

            await CreateSut(repeatDays: 7).CheckInactiveUsersAsync();

            _notificationRepo.Received(1).Add(Arg.Any<Notification>());
            await _emailService.Received(1).SendLoginReminderEmailAsync(user.Email, user.Name, 20);
        }

        [Fact]
        public async Task CheckInactiveUsersAsync_SkipsUsersNotYetPastTheThreshold()
        {
            var user = PendingUser(1, createdDaysAgo: 1); // threshold is 3 days
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());

            await CreateSut(thresholdDays: 3).CheckInactiveUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
            await _emailService.DidNotReceive().SendLoginReminderEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task CheckInactiveUsersAsync_SkipsDeactivatedUsers()
        {
            var user = PendingUser(1, createdDaysAgo: 10);
            user.IsActive = false;
            // The service's own query filters IsActive - a deactivated user simply isn't returned.
            _userRepo.Query().Returns(new List<User>().BuildMock());

            await CreateSut().CheckInactiveUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
            await _emailService.DidNotReceive().SendLoginReminderEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task CheckInactiveUsersAsync_SkipsUsersWhoAlreadyFinishedSetup()
        {
            var user = PendingUser(1, createdDaysAgo: 10);
            user.MustChangePassword = false;
            // The service's own query filters MustChangePassword - a fully-onboarded user isn't returned.
            _userRepo.Query().Returns(new List<User>().BuildMock());

            await CreateSut().CheckInactiveUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }
    }
}
