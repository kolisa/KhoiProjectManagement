using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Focused on SetSpacePermissionsAsync's grantee-count validation (User/Role/Group is a 3-way XOR)
    // - not a full SpaceService suite, which doesn't otherwise exist yet.
    public class SpaceServiceTests
    {
        private readonly IRepository<Project> _projectRepo = Substitute.For<IRepository<Project>>();
        private readonly IRepository<Space> _spaceRepo = Substitute.For<IRepository<Space>>();
        private readonly IRepository<SpacePermission> _spacePermissionRepo = Substitute.For<IRepository<SpacePermission>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<UserRole> _userRoleRepo = Substitute.For<IRepository<UserRole>>();
        private readonly IRepository<UserGroup> _userGroupRepo = Substitute.For<IRepository<UserGroup>>();
        private readonly ISpaceDeletionBlockersRepository _deletionBlockersRepo = Substitute.For<ISpaceDeletionBlockersRepository>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly ISpacePermissionResolver _resolver = Substitute.For<ISpacePermissionResolver>();

        private SpaceService CreateSut() => new(
            _projectRepo, _spaceRepo, _spacePermissionRepo, _userRepo, _userRoleRepo, _userGroupRepo,
            _deletionBlockersRepo, _unitOfWork, _resolver);

        private void SetUpExistingSpaceWithNoGrants(int spaceId)
        {
            _spaceRepo.FindAsync(spaceId).Returns(new Space { Id = spaceId, Name = "Test Space", CreatedBy = 1 });
            _spacePermissionRepo.Query().Returns(new List<SpacePermission>().BuildMock());
        }

        [Theory]
        [InlineData(1, null, null)] // UserId only - valid
        [InlineData(null, 2, null)] // RoleId only - valid
        [InlineData(null, null, 3)] // GroupId only - valid
        public async Task SetSpacePermissionsAsync_WhenExactlyOneGranteeIsSet_Succeeds(int? userId, int? roleId, int? groupId)
        {
            SetUpExistingSpaceWithNoGrants(1);

            var result = await CreateSut().SetSpacePermissionsAsync(1,
                new List<SetSpacePermissionDto> { new() { UserId = userId, RoleId = roleId, GroupId = groupId, Level = "Read" } },
                createdByUserId: 100);

            Assert.True(result);
        }

        [Theory]
        [InlineData(1, 2, null)]  // both User and Role set - invalid
        [InlineData(1, null, 3)]  // both User and Group set - invalid
        [InlineData(null, 2, 3)]  // both Role and Group set - invalid
        [InlineData(null, null, null)] // none set - invalid
        public async Task SetSpacePermissionsAsync_WhenGranteeCountIsNotExactlyOne_Throws(int? userId, int? roleId, int? groupId)
        {
            SetUpExistingSpaceWithNoGrants(1);

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetSpacePermissionsAsync(1,
                new List<SetSpacePermissionDto> { new() { UserId = userId, RoleId = roleId, GroupId = groupId, Level = "Read" } },
                createdByUserId: 100));
        }

        [Fact]
        public async Task SetSpacePermissionsAsync_PersistsAGroupGrantWithItsGroupId()
        {
            SetUpExistingSpaceWithNoGrants(1);
            SpacePermission? added = null;
            _spacePermissionRepo.When(r => r.AddRange(Arg.Any<IEnumerable<SpacePermission>>())).Do(ci =>
            {
                added = ci.Arg<IEnumerable<SpacePermission>>().First(sp => sp.GroupId.HasValue);
            });

            await CreateSut().SetSpacePermissionsAsync(1,
                new List<SetSpacePermissionDto> { new() { GroupId = 7, Level = "Write" } },
                createdByUserId: 100);

            Assert.NotNull(added);
            Assert.Equal(7, added!.GroupId);
            Assert.Null(added.UserId);
            Assert.Null(added.RoleId);
            Assert.Equal(PermissionLevel.Write, added.Level);
        }

        [Fact]
        public async Task DeleteSpaceAsync_WhenTheSpaceDoesNotExist_ReturnsFalseWithoutCheckingBlockers()
        {
            _spaceRepo.FindAsync(1).Returns((Space?)null);

            var result = await CreateSut().DeleteSpaceAsync(1);

            Assert.False(result);
            await _deletionBlockersRepo.DidNotReceive().HasBlockingChildrenAsync(Arg.Any<int>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteSpaceAsync_WhenSomethingStillReferencesTheSpace_ThrowsAndDoesNotSave()
        {
            var space = new Space { Id = 1, Name = "Test Space", IsActive = true, CreatedBy = 1 };
            _spaceRepo.FindAsync(1).Returns(space);
            _deletionBlockersRepo.HasBlockingChildrenAsync(1).Returns(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().DeleteSpaceAsync(1));

            Assert.True(space.IsActive);
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteSpaceAsync_WhenNothingReferencesTheSpace_SoftDeletesItInOneCombinedCheck()
        {
            var space = new Space { Id = 1, Name = "Test Space", IsActive = true, CreatedBy = 1 };
            _spaceRepo.FindAsync(1).Returns(space);
            _deletionBlockersRepo.HasBlockingChildrenAsync(1).Returns(false);

            var result = await CreateSut().DeleteSpaceAsync(1);

            Assert.True(result);
            Assert.False(space.IsActive);
            await _deletionBlockersRepo.Received(1).HasBlockingChildrenAsync(1);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}
