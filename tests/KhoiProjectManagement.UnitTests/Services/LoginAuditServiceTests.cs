using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class LoginAuditServiceTests
    {
        private readonly IRepository<LoginAuditLog> _loginAuditRepo = Substitute.For<IRepository<LoginAuditLog>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private LoginAuditService CreateSut() => new(_loginAuditRepo, _unitOfWork);

        private static LoginAuditLog Log(int id, string email, bool success, DateTime timestamp, int? userId = null) => new()
        {
            Id = id,
            UserId = userId,
            EmailAttempted = email,
            Success = success,
            FailureReason = success ? null : "Invalid credentials",
            Timestamp = timestamp
        };

        [Fact]
        public async Task LogAsync_AddsEntryAndSaves()
        {
            var sut = CreateSut();

            await sut.LogAsync(1, "a@x.com", true, null, "127.0.0.1");

            _loginAuditRepo.Received(1).Add(Arg.Is<LoginAuditLog>(l =>
                l.UserId == 1 && l.EmailAttempted == "a@x.com" && l.Success && l.IpAddress == "127.0.0.1"));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetRecentAsync_OrdersNewestFirstAndRespectsTake()
        {
            var logs = new List<LoginAuditLog>
            {
                Log(1, "a@x.com", true, new DateTime(2026, 1, 1)),
                Log(2, "b@x.com", true, new DateTime(2026, 1, 3)),
                Log(3, "c@x.com", true, new DateTime(2026, 1, 2)),
            };
            _loginAuditRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(take: 2);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.Equal(3, result[1].Id);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersBySuccess()
        {
            var logs = new List<LoginAuditLog>
            {
                Log(1, "a@x.com", true, DateTime.UtcNow),
                Log(2, "b@x.com", false, DateTime.UtcNow),
            };
            _loginAuditRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(success: false);

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
            Assert.Equal("Invalid credentials", result[0].FailureReason);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByEmailContains()
        {
            var logs = new List<LoginAuditLog>
            {
                Log(1, "someone@khoitech.africa", true, DateTime.UtcNow),
                Log(2, "other@example.com", true, DateTime.UtcNow),
            };
            _loginAuditRepo.Query().Returns(logs.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(emailContains: "khoitech");

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }
    }
}
