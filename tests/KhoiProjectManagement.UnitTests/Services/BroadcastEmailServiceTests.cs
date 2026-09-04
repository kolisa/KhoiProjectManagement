using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class BroadcastEmailServiceTests
    {
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<UserRole> _userRoleRepo = Substitute.For<IRepository<UserRole>>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private BroadcastEmailService CreateSut() => new(_userRepo, _userRoleRepo, _emailService);

        [Fact]
        public async Task SendBroadcastAsync_SendsOnlyToActiveUsersHoldingASelectedRole()
        {
            _userRoleRepo.Query().Returns(new List<UserRole>
            {
                new() { UserId = 1, RoleId = 2 }, // Manager - selected
                new() { UserId = 2, RoleId = 3 }, // Member - not selected
                new() { UserId = 3, RoleId = 2 }, // Manager - selected, but inactive user
            }.BuildMock());
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Active Manager", Email = "manager@khoitech.africa", IsActive = true },
                new() { Id = 2, Name = "Member", Email = "member@khoitech.africa", IsActive = true },
                new() { Id = 3, Name = "Inactive Manager", Email = "inactive@khoitech.africa", IsActive = false },
            }.BuildMock());

            var dto = new BroadcastEmailDto { Subject = "Heads up", Body = "New feature.", RoleIds = new List<int> { 2 } };
            var count = await CreateSut().SendBroadcastAsync(dto);

            Assert.Equal(1, count);
            await _emailService.Received(1).SendBroadcastEmailAsync("manager@khoitech.africa", "Heads up", Arg.Any<string>());
            await _emailService.DidNotReceive().SendBroadcastEmailAsync("member@khoitech.africa", Arg.Any<string>(), Arg.Any<string>());
            await _emailService.DidNotReceive().SendBroadcastEmailAsync("inactive@khoitech.africa", Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task SendBroadcastAsync_DoesNotDoubleSendToAUserHoldingMultipleSelectedRoles()
        {
            _userRoleRepo.Query().Returns(new List<UserRole>
            {
                new() { UserId = 1, RoleId = 1 },
                new() { UserId = 1, RoleId = 2 },
            }.BuildMock());
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Dual Role", Email = "dual@khoitech.africa", IsActive = true },
            }.BuildMock());

            var dto = new BroadcastEmailDto { Subject = "Heads up", Body = "New feature.", RoleIds = new List<int> { 1, 2 } };
            var count = await CreateSut().SendBroadcastAsync(dto);

            Assert.Equal(1, count);
            await _emailService.Received(1).SendBroadcastEmailAsync("dual@khoitech.africa", Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task SendBroadcastAsync_HtmlEncodesAndLineBreaksThePlainTextBody()
        {
            _userRoleRepo.Query().Returns(new List<UserRole> { new() { UserId = 1, RoleId = 1 } }.BuildMock());
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Someone", Email = "someone@khoitech.africa", IsActive = true },
            }.BuildMock());

            var dto = new BroadcastEmailDto { Subject = "Heads up", Body = "Line one <b>bold</b>\nLine two", RoleIds = new List<int> { 1 } };
            await CreateSut().SendBroadcastAsync(dto);

            await _emailService.Received(1).SendBroadcastEmailAsync(
                "someone@khoitech.africa",
                "Heads up",
                Arg.Is<string>(html => html.Contains("&lt;b&gt;bold&lt;/b&gt;") && html.Contains("Line one") && html.Contains("<br>Line two")));
        }

        [Fact]
        public async Task SendBroadcastAsync_WhenNoUsersHoldTheSelectedRoles_ReturnsZeroAndSendsNothing()
        {
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userRepo.Query().Returns(new List<User>().BuildMock());

            var dto = new BroadcastEmailDto { Subject = "Heads up", Body = "New feature.", RoleIds = new List<int> { 99 } };
            var count = await CreateSut().SendBroadcastAsync(dto);

            Assert.Equal(0, count);
            await _emailService.DidNotReceive().SendBroadcastEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}
