using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class EmailLogServiceTests
    {
        private readonly IRepository<EmailLog> _emailLogRepo = Substitute.For<IRepository<EmailLog>>();

        private EmailLogService CreateSut() => new(_emailLogRepo);

        private static EmailLog Log(int id, string toEmail, bool success, string type, DateTime sentAt) => new()
        {
            Id = id,
            ToEmail = toEmail,
            Subject = $"Subject {id}",
            EmailType = type,
            IsSuccess = success,
            Status = success ? EmailLogStatus.Sent : EmailLogStatus.Failed,
            SentAt = sentAt,
            ErrorMessage = success ? null : "SMTP unreachable"
        };

        [Fact]
        public async Task GetRecentAsync_OrdersNewestFirstAndRespectsTake()
        {
            var logs = new List<EmailLog>
            {
                Log(1, "a@x.com", true, "welcome", new DateTime(2026, 1, 1)),
                Log(2, "b@x.com", true, "welcome", new DateTime(2026, 1, 3)),
                Log(3, "c@x.com", true, "welcome", new DateTime(2026, 1, 2)),
            };
            _emailLogRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(take: 2);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id); // newest (Jan 3)
            Assert.Equal(3, result[1].Id); // next newest (Jan 2)
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByStatus()
        {
            var logs = new List<EmailLog>
            {
                Log(1, "a@x.com", true, "welcome", DateTime.UtcNow),
                Log(2, "b@x.com", false, "welcome", DateTime.UtcNow),
            };
            _emailLogRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(status: "Failed");

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
            Assert.Equal("SMTP unreachable", result[0].ErrorMessage);
            Assert.Equal("Failed", result[0].Status);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByPendingStatus()
        {
            var logs = new List<EmailLog>
            {
                Log(1, "a@x.com", true, "welcome", DateTime.UtcNow),
                new() { Id = 2, ToEmail = "b@x.com", Subject = "Subject 2", EmailType = "welcome", Status = EmailLogStatus.Pending, SentAt = DateTime.UtcNow },
            };
            _emailLogRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(status: "Pending");

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByToEmailContains()
        {
            var logs = new List<EmailLog>
            {
                Log(1, "someone@khoitech.africa", true, "welcome", DateTime.UtcNow),
                Log(2, "other@example.com", true, "welcome", DateTime.UtcNow),
            };
            _emailLogRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(toEmailContains: "khoitech");

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByEmailType()
        {
            var logs = new List<EmailLog>
            {
                Log(1, "a@x.com", true, "welcome", DateTime.UtcNow),
                Log(2, "b@x.com", true, "temp_password", DateTime.UtcNow),
            };
            _emailLogRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(emailType: "temp_password");

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
        }
    }
}
