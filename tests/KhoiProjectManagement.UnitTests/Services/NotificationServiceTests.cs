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
        private readonly IRepository<LibraryFile> _libraryFileRepo = Substitute.For<IRepository<LibraryFile>>();
        private readonly IRepository<LibraryFileVersion> _libraryFileVersionRepo = Substitute.For<IRepository<LibraryFileVersion>>();
        private readonly IRepository<ProjectUser> _projectUserRepo = Substitute.For<IRepository<ProjectUser>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private NotificationService CreateSut(
            int thresholdDays = 3, int repeatDays = 7,
            int weeklyDigestRepeatDays = 6,
            int noDocumentsThresholdDays = 14, int noDocumentsRepeatDays = 30,
            int dormantThresholdDays = 21, int dormantRepeatDays = 14)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Notifications:LoginReminderThresholdDays"] = thresholdDays.ToString(),
                    ["Notifications:LoginReminderRepeatDays"] = repeatDays.ToString(),
                    ["Notifications:WeeklyDigestRepeatDays"] = weeklyDigestRepeatDays.ToString(),
                    ["Notifications:NoDocumentsThresholdDays"] = noDocumentsThresholdDays.ToString(),
                    ["Notifications:NoDocumentsRepeatDays"] = noDocumentsRepeatDays.ToString(),
                    ["Notifications:DormantUserThresholdDays"] = dormantThresholdDays.ToString(),
                    ["Notifications:DormantUserRepeatDays"] = dormantRepeatDays.ToString(),
                })
                .Build();
            return new NotificationService(
                _notificationRepo, _preferenceRepo, _taskRepo, _userRepo,
                _libraryFileRepo, _libraryFileVersionRepo, _projectUserRepo,
                _unitOfWork, _emailService, config);
        }

        private static User OnboardedUser(int id, DateTime? lastLoginAt = null) => new()
        {
            Id = id,
            Name = $"User {id}",
            Email = $"user{id}@khoitech.africa",
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            LastLoginAt = lastLoginAt
        };

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

        [Fact]
        public async Task GenerateWeeklyDigestsAsync_WhenOnboardedAndNotRecentlyDigested_CreatesNotificationAndSendsEmail()
        {
            var user = OnboardedUser(1);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());
            _taskRepo.Query().Returns(new List<ProjectTask>().BuildMock());
            _projectUserRepo.Query().Returns(new List<ProjectUser>().BuildMock());
            _libraryFileVersionRepo.Query().Returns(new List<LibraryFileVersion>().BuildMock());

            await CreateSut().GenerateWeeklyDigestsAsync();

            _notificationRepo.Received(1).Add(Arg.Is<Notification>(n => n.UserId == 1 && n.Type == NotificationTypes.WeeklyDigest));
            await _emailService.Received(1).SendWeeklyDigestEmailAsync(
                user.Email, user.Name, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        }

        [Fact]
        public async Task GenerateWeeklyDigestsAsync_WhenDigestedWithinRepeatWindow_DoesNotDigestAgain()
        {
            var user = OnboardedUser(1);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.WeeklyDigest, CreatedAt = DateTime.UtcNow.AddDays(-2) }
            }.BuildMock());

            await CreateSut(weeklyDigestRepeatDays: 6).GenerateWeeklyDigestsAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
            await _emailService.DidNotReceive().SendWeeklyDigestEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        }

        [Fact]
        public async Task GenerateWeeklyDigestsAsync_SkipsUsersStillMustChangePassword()
        {
            var user = OnboardedUser(1);
            user.MustChangePassword = true;
            // The service's own query filters MustChangePassword - a pending-onboarding user isn't returned.
            _userRepo.Query().Returns(new List<User>().BuildMock());

            await CreateSut().GenerateWeeklyDigestsAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckUsersWithNoDocumentsAsync_WhenNeverUploadedAndPastThreshold_NudgesUser()
        {
            var user = OnboardedUser(1);
            user.CreatedAt = DateTime.UtcNow.AddDays(-30);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _libraryFileRepo.Query().Returns(new List<LibraryFile>().BuildMock());
            _libraryFileVersionRepo.Query().Returns(new List<LibraryFileVersion>().BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());

            await CreateSut(noDocumentsThresholdDays: 14).CheckUsersWithNoDocumentsAsync();

            _notificationRepo.Received(1).Add(Arg.Is<Notification>(n => n.UserId == 1 && n.Type == NotificationTypes.NoDocumentsNudge));
            await _emailService.Received(1).SendNoDocumentsNudgeEmailAsync(user.Email, user.Name);
        }

        [Fact]
        public async Task CheckUsersWithNoDocumentsAsync_WhenUserHasCreatedAFile_DoesNotNudge()
        {
            var user = OnboardedUser(1);
            user.CreatedAt = DateTime.UtcNow.AddDays(-30);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _libraryFileRepo.Query().Returns(new List<LibraryFile> { new() { CreatedBy = 1 } }.BuildMock());

            await CreateSut().CheckUsersWithNoDocumentsAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckUsersWithNoDocumentsAsync_WhenUserHasUploadedAVersionOnly_DoesNotNudge()
        {
            var user = OnboardedUser(1);
            user.CreatedAt = DateTime.UtcNow.AddDays(-30);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _libraryFileRepo.Query().Returns(new List<LibraryFile>().BuildMock());
            _libraryFileVersionRepo.Query().Returns(new List<LibraryFileVersion> { new() { UploadedBy = 1 } }.BuildMock());

            await CreateSut().CheckUsersWithNoDocumentsAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckUsersWithNoDocumentsAsync_WhenNudgedWithinRepeatWindow_DoesNotNudgeAgain()
        {
            var user = OnboardedUser(1);
            user.CreatedAt = DateTime.UtcNow.AddDays(-30);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _libraryFileRepo.Query().Returns(new List<LibraryFile>().BuildMock());
            _libraryFileVersionRepo.Query().Returns(new List<LibraryFileVersion>().BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.NoDocumentsNudge, CreatedAt = DateTime.UtcNow.AddDays(-5) }
            }.BuildMock());

            await CreateSut(noDocumentsRepeatDays: 30).CheckUsersWithNoDocumentsAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckDormantUsersAsync_WhenPastThresholdAndOnboarded_NudgesUser()
        {
            var user = OnboardedUser(1, lastLoginAt: DateTime.UtcNow.AddDays(-25));
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());

            await CreateSut(dormantThresholdDays: 21).CheckDormantUsersAsync();

            _notificationRepo.Received(1).Add(Arg.Is<Notification>(n => n.UserId == 1 && n.Type == NotificationTypes.DormantUserNudge));
            await _emailService.Received(1).SendDormantUserNudgeEmailAsync(user.Email, user.Name, 25);
        }

        [Fact]
        public async Task CheckDormantUsersAsync_DoesNotOverlapWithStillPendingOnboardingUsers()
        {
            // A user still on MustChangePassword is CheckInactiveUsersAsync's population, not this one's -
            // even if somehow LastLoginAt were set, CheckDormantUsersAsync's own query excludes them.
            var user = OnboardedUser(1, lastLoginAt: DateTime.UtcNow.AddDays(-25));
            user.MustChangePassword = true;
            _userRepo.Query().Returns(new List<User>().BuildMock());

            await CreateSut().CheckDormantUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckDormantUsersAsync_WhenNudgedWithinRepeatWindow_DoesNotNudgeAgain()
        {
            var user = OnboardedUser(1, lastLoginAt: DateTime.UtcNow.AddDays(-25));
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.DormantUserNudge, CreatedAt = DateTime.UtcNow.AddDays(-3) }
            }.BuildMock());

            await CreateSut(dormantRepeatDays: 14).CheckDormantUsersAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckBirthdaysAsync_WhenTodayIsUsersBirthday_SendsGreetingAndEmail()
        {
            var today = DateTime.UtcNow.Date;
            var user = OnboardedUser(1);
            user.DateOfBirth = new DateTime(1990, today.Month, today.Day);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _preferenceRepo.Query().Returns(new List<NotificationPreference>().BuildMock());

            await CreateSut().CheckBirthdaysAsync();

            _notificationRepo.Received(1).Add(Arg.Is<Notification>(n => n.UserId == 1 && n.Type == NotificationTypes.BirthdayGreeting));
            await _emailService.Received(1).SendBirthdayEmailAsync(user.Email, user.Name);
        }

        [Fact]
        public async Task CheckBirthdaysAsync_WhenTodayIsNotUsersBirthday_DoesNotGreet()
        {
            var today = DateTime.UtcNow.Date;
            var otherDay = today.AddDays(10);
            var user = OnboardedUser(1);
            user.DateOfBirth = new DateTime(1990, otherDay.Month, otherDay.Day);
            _userRepo.Query().Returns(new List<User>().BuildMock()); // service's own Where excludes non-matches

            await CreateSut().CheckBirthdaysAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
        }

        [Fact]
        public async Task CheckBirthdaysAsync_WhenAlreadyGreetedToday_DoesNotGreetAgain()
        {
            var today = DateTime.UtcNow.Date;
            var user = OnboardedUser(1);
            user.DateOfBirth = new DateTime(1990, today.Month, today.Day);
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { UserId = 1, Type = NotificationTypes.BirthdayGreeting, CreatedAt = today.AddHours(1) }
            }.BuildMock());

            await CreateSut().CheckBirthdaysAsync();

            _notificationRepo.DidNotReceive().Add(Arg.Any<Notification>());
            await _emailService.DidNotReceive().SendBirthdayEmailAsync(Arg.Any<string>(), Arg.Any<string>());
        }
    }
}
