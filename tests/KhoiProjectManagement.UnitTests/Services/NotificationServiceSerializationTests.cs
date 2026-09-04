using System.Text.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Regression test for a real production incident: GetUserNotificationsAsync 500'd with
    // "A possible object cycle was detected" once a user had both a task-linked notification and a
    // project-linked notification pointing at the same project (exactly what happens right after
    // creating a project you already have a task notification for). NSubstitute's mocked
    // IRepository<T>.Query() can't reproduce this - EF's automatic relationship fixup (Task.Project
    // <-> Project.Tasks) is a real ChangeTracker behavior, not something a mocked IQueryable performs -
    // so this uses a real ProjectManagementContext against EF Core's InMemory provider instead, same
    // technique as SpacePermissionResolverTests, wrapped in the real Repository<T> adapter so the
    // actual NotificationService code under test is exercised unmodified.
    public class NotificationServiceSerializationTests : IDisposable
    {
        private readonly ProjectManagementContext _context;

        public NotificationServiceSerializationTests()
        {
            var options = new DbContextOptionsBuilder<ProjectManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ProjectManagementContext(options);
        }

        public void Dispose() => _context.Dispose();

        private NotificationService CreateSut() => new(
            new Repository<Notification>(_context),
            new Repository<NotificationPreference>(_context),
            new Repository<ProjectTask>(_context),
            new Repository<User>(_context),
            new Repository<LibraryFile>(_context),
            new Repository<LibraryFileVersion>(_context),
            new Repository<ProjectUser>(_context),
            new Repository<Timesheet>(_context),
            new Repository<WikiPage>(_context),
            new Repository<Idea>(_context),
            new Repository<Reminder>(_context),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IEmailService>(),
            new ConfigurationBuilder().Build());

        [Fact]
        public async Task GetUserNotificationsAsync_WithATaskNotificationAndAProjectNotificationForTheSameProject_SerializesWithoutThrowing()
        {
            var project = new Project { Id = 1, Name = "New Project", CreatedBy = 1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1) };
            var task = new ProjectTask { Id = 1, ProjectId = 1, Title = "Existing task", DueDate = DateTime.UtcNow.AddDays(3) };
            _context.Projects.Add(project);
            _context.Tasks.Add(task);
            _context.Notifications.AddRange(
                new Notification { UserId = 42, Type = NotificationTypes.Assignment, Message = "Assigned to a task", TaskId = 1 },
                new Notification { UserId = 42, Type = NotificationTypes.ProjectCreated, Message = "Project created", ProjectId = 1 }
            );
            await _context.SaveChangesAsync();

            var result = await CreateSut().GetUserNotificationsAsync(42);

            // The bug manifested as System.Text.Json throwing here (the exact serialization path
            // ASP.NET Core's default output formatter takes for Ok(notifications)) - not as an
            // exception from GetUserNotificationsAsync itself, so the meaningful assertion is that
            // serializing the result succeeds, not just that the method returns.
            var json = JsonSerializer.Serialize(result);
            Assert.Contains("Assigned to a task", json);
            Assert.Contains("Project created", json);
        }
    }
}
