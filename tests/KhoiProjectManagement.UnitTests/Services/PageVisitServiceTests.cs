using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class PageVisitServiceTests
    {
        private readonly IRepository<PageVisitLog> _pageVisitRepo = Substitute.For<IRepository<PageVisitLog>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private PageVisitService CreateSut() => new(_pageVisitRepo, _unitOfWork);

        private static PageVisitLog Visit(int id, int userId, string userName, string tabKey, DateTime timestamp) => new()
        {
            Id = id,
            UserId = userId,
            User = new User { Id = userId, Name = userName, Email = $"{userName}@x.com" },
            TabKey = tabKey,
            Timestamp = timestamp
        };

        [Fact]
        public async Task LogAsync_AddsEntryAndSaves()
        {
            var sut = CreateSut();

            await sut.LogAsync(1, "vault");

            _pageVisitRepo.Received(1).Add(Arg.Is<PageVisitLog>(v => v.UserId == 1 && v.TabKey == "vault"));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetRecentAsync_OrdersNewestFirstAndRespectsTake()
        {
            var visits = new List<PageVisitLog>
            {
                Visit(1, 1, "Alice", "projects", new DateTime(2026, 1, 1)),
                Visit(2, 1, "Alice", "vault", new DateTime(2026, 1, 3)),
                Visit(3, 1, "Alice", "wiki", new DateTime(2026, 1, 2)),
            };
            _pageVisitRepo.Query().Returns(visits.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(take: 2);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.Equal(3, result[1].Id);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByUserId()
        {
            var visits = new List<PageVisitLog>
            {
                Visit(1, 1, "Alice", "projects", DateTime.UtcNow),
                Visit(2, 2, "Bob", "vault", DateTime.UtcNow),
            };
            _pageVisitRepo.Query().Returns(visits.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(userId: 2);

            Assert.Single(result);
            Assert.Equal("Bob", result[0].UserName);
        }

        [Fact]
        public async Task GetRecentAsync_FiltersByTabKey()
        {
            var visits = new List<PageVisitLog>
            {
                Visit(1, 1, "Alice", "projects", DateTime.UtcNow),
                Visit(2, 1, "Alice", "vault", DateTime.UtcNow),
            };
            _pageVisitRepo.Query().Returns(visits.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetRecentAsync(tabKey: "vault");

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
        }
    }
}
