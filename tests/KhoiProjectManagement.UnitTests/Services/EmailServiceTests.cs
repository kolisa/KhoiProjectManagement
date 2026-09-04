using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // EmailService takes a concrete ProjectManagementContext (not IRepository<T>), same reasoning as
    // SpacePermissionResolverTests - seeded with EF Core's InMemory provider. Only the outbox side
    // (EnqueueEmailAsync, reached via any Send*EmailAsync) is unit-testable this way - the actual SMTP
    // dispatch in DispatchPendingEmailsAsync/SendAndRecordAsync needs a real or fake SMTP server, which
    // doesn't exist in this codebase, so that path is verified manually/via Playwright instead.
    public class EmailServiceTests : IDisposable
    {
        private readonly ProjectManagementContext _context;
        private readonly ILogger<EmailService> _logger = Substitute.For<ILogger<EmailService>>();

        public EmailServiceTests()
        {
            var options = new DbContextOptionsBuilder<ProjectManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ProjectManagementContext(options);
        }

        public void Dispose() => _context.Dispose();

        private EmailService CreateSut(Dictionary<string, string?>? configOverrides = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configOverrides ?? new Dictionary<string, string?>
                {
                    ["App:FrontendBaseUrl"] = "https://app.example.com",
                })
                .Build();
            return new EmailService(config, _context, _logger);
        }

        [Fact]
        public async Task SendTaskAssignmentEmailAsync_EnqueuesAPendingRowWithoutAttemptingToSend()
        {
            var sut = CreateSut();

            await sut.SendTaskAssignmentEmailAsync("dev@khoitech.africa", "Fix login bug", "Q3 Launch", new DateTime(2026, 9, 11), "high");

            var log = Assert.Single(_context.EmailLogs);
            Assert.Equal(EmailLogStatus.Pending, log.Status);
            Assert.Equal("dev@khoitech.africa", log.ToEmail);
            Assert.Equal("task_assignment", log.EmailType);
            Assert.Contains("https://app.example.com/?tab=tasks", log.Body);
            // Project/Due/Priority render as detail rows now, not inline prose - see EmailTemplates.
            Assert.Contains("Q3 Launch", log.Body);
            Assert.Contains("2026-09-11", log.Body);
            Assert.Contains("high", log.Body);
        }

        [Fact]
        public async Task SendPasswordResetEmailAsync_UsesTheProvidedResetLinkAsTheCta()
        {
            var sut = CreateSut();

            await sut.SendPasswordResetEmailAsync("user@khoitech.africa", "Jane", "https://app.example.com/reset-password?token=abc");

            var log = Assert.Single(_context.EmailLogs);
            Assert.Contains("https://app.example.com/reset-password?token=abc", log.Body);
        }

        [Fact]
        public async Task SendMentionEmailAsync_WhenContextUrlProvided_UsesItInsteadOfTheDefaultAppLink()
        {
            var sut = CreateSut();

            await sut.SendMentionEmailAsync("user@khoitech.africa", "Alice", "wiki page", "Onboarding", "check this out", "https://app.example.com/?tab=wiki&spaceId=1&pageId=2");

            var log = Assert.Single(_context.EmailLogs);
            Assert.Contains("https://app.example.com/?tab=wiki&spaceId=1&pageId=2", log.Body);
        }

        [Fact]
        public async Task MultipleEnqueuedEmails_GetSequentiallyIncreasingIdsPreservingFifoOrder()
        {
            var sut = CreateSut();

            await sut.SendTaskAssignmentEmailAsync("a@x.com", "Task A", "Project", DateTime.Today, "medium");
            await sut.SendTaskAssignmentEmailAsync("b@x.com", "Task B", "Project", DateTime.Today, "medium");
            await sut.SendTaskAssignmentEmailAsync("c@x.com", "Task C", "Project", DateTime.Today, "medium");

            var ids = _context.EmailLogs.OrderBy(e => e.Id).Select(e => e.ToEmail).ToList();
            Assert.Equal(new[] { "a@x.com", "b@x.com", "c@x.com" }, ids);
        }
    }
}
