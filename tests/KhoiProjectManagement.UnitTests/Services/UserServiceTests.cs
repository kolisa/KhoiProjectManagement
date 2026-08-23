using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class UserServiceTests
    {
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<Role> _roleRepo = Substitute.For<IRepository<Role>>();
        private readonly IRepository<UserRole> _userRoleRepo = Substitute.For<IRepository<UserRole>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private UserService CreateSut() => new(_userRepo, _roleRepo, _userRoleRepo, _unitOfWork);

        [Fact]
        public async Task CreateUserAsync_WhenEmailAlreadyExists_ThrowsAndNeverAddsAUser()
        {
            var existing = new User { Id = 1, Email = "taken@khoitech.africa", Name = "Existing" };
            _userRepo.Query().Returns(new List<User> { existing }.BuildMock());

            var sut = CreateSut();
            var dto = new CreateUserDto { Name = "New Guy", Email = "taken@khoitech.africa", Role = "member", Position = "QA", Password = "SomeLongPassword1!" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateUserAsync(dto));
            _userRepo.DidNotReceive().Add(Arg.Any<User>());
        }

        [Fact]
        public async Task CreateUserAsync_WhenEmailIsNew_HashesThePasswordAndAssignsTheMatchingRole()
        {
            _userRepo.Query().Returns(new List<User>().BuildMock());
            var memberRole = new Role { Id = 3, Name = "Member" };
            _roleRepo.Query().Returns(new List<Role> { memberRole }.BuildMock());

            User? added = null;
            _userRepo.When(r => r.Add(Arg.Any<User>())).Do(ci =>
            {
                added = ci.Arg<User>();
                added.Id = 42;
            });

            var sut = CreateSut();
            var dto = new CreateUserDto { Name = "New Guy", Email = "new@khoitech.africa", Role = "member", Position = "QA", Password = "SomeLongPassword1!" };
            var result = await sut.CreateUserAsync(dto);

            Assert.Equal("New Guy", result.Name);
            Assert.NotEqual("SomeLongPassword1!", added!.PasswordHash); // never store plaintext
            Assert.True(BCrypt.Net.BCrypt.Verify("SomeLongPassword1!", added.PasswordHash));
            _userRoleRepo.Received(1).Add(Arg.Is<UserRole>(ur => ur.UserId == 42 && ur.RoleId == 3));
        }

        [Fact]
        public async Task CreateUserAsync_WhenRoleNameDoesNotMatchAnySeededRole_StillCreatesTheUserWithoutAUserRoleRow()
        {
            _userRepo.Query().Returns(new List<User>().BuildMock());
            _roleRepo.Query().Returns(new List<Role>().BuildMock());
            _userRepo.When(r => r.Add(Arg.Any<User>())).Do(ci => ci.Arg<User>().Id = 1);

            var sut = CreateSut();
            var dto = new CreateUserDto { Name = "Odd Role", Email = "odd@khoitech.africa", Role = "not-a-real-role", Position = "QA", Password = "SomeLongPassword1!" };
            var result = await sut.CreateUserAsync(dto);

            Assert.Equal("Odd Role", result.Name);
            _userRoleRepo.DidNotReceive().Add(Arg.Any<UserRole>());
        }

        [Fact]
        public async Task AssignRolesAsync_WhenAnyRoleIdDoesNotExist_ReturnsFalseWithoutChangingAnything()
        {
            var user = new User { Id = 1, Name = "Someone" };
            _userRepo.FindAsync(1).Returns(user);
            _roleRepo.Query().Returns(new List<Role> { new() { Id = 3, Name = "Member" } }.BuildMock());

            var sut = CreateSut();
            var result = await sut.AssignRolesAsync(1, new List<int> { 3, 999 }); // 999 doesn't exist

            Assert.False(result);
            _userRoleRepo.DidNotReceive().RemoveRange(Arg.Any<IEnumerable<UserRole>>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task AssignRolesAsync_WhenAllRoleIdsExist_ReplacesRolesAndSetsLegacyRoleToTheHighestPrivilegeOne()
        {
            var user = new User { Id = 1, Name = "Someone", Role = "member" };
            _userRepo.FindAsync(1).Returns(user);
            _roleRepo.Query().Returns(new List<Role>
            {
                new() { Id = 1, Name = "Admin" },
                new() { Id = 3, Name = "Member" }
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole> { new() { UserId = 1, RoleId = 3 } }.BuildMock());

            var sut = CreateSut();
            var result = await sut.AssignRolesAsync(1, new List<int> { 1, 3 });

            Assert.True(result);
            Assert.Equal("admin", user.Role); // lowest seeded Id (Admin=1) wins as the legacy display role
            _userRoleRepo.Received(1).AddRange(Arg.Is<IEnumerable<UserRole>>(rs => rs.Count() == 2));
        }

        [Theory]
        [InlineData("correct-password", true)]
        [InlineData("wrong-password", false)]
        public async Task ValidateUserCredentialsAsync_ChecksThePasswordAgainstTheStoredHash(string attempt, bool expected)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");
            var user = new User { Id = 1, Email = "user@khoitech.africa", PasswordHash = hash, IsActive = true };
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());

            var sut = CreateSut();
            var result = await sut.ValidateUserCredentialsAsync("user@khoitech.africa", attempt);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ValidateUserCredentialsAsync_WhenUserIsDeactivated_ReturnsFalseEvenWithTheCorrectPassword()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");
            var user = new User { Id = 1, Email = "gone@khoitech.africa", PasswordHash = hash, IsActive = false };
            // The service's own query filters IsActive - an inactive user simply isn't found.
            _userRepo.Query().Returns(new List<User>().BuildMock());

            var sut = CreateSut();
            var result = await sut.ValidateUserCredentialsAsync("gone@khoitech.africa", "correct-password");

            Assert.False(result);
        }

        [Fact]
        public async Task DeactivateUserAsync_SetsIsActiveFalse()
        {
            var user = new User { Id = 1, Name = "Someone", IsActive = true };
            _userRepo.FindAsync(1).Returns(user);

            var sut = CreateSut();
            var result = await sut.DeactivateUserAsync(1);

            Assert.True(result);
            Assert.False(user.IsActive);
        }

        [Fact]
        public async Task DeactivateUserAsync_WhenUserDoesNotExist_ReturnsFalse()
        {
            _userRepo.FindAsync(999).Returns((User?)null);

            var sut = CreateSut();
            var result = await sut.DeactivateUserAsync(999);

            Assert.False(result);
        }
    }
}
