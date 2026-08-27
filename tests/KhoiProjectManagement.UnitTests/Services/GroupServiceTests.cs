using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class GroupServiceTests
    {
        private readonly IRepository<Group> _groupRepo = Substitute.For<IRepository<Group>>();
        private readonly IRepository<UserGroup> _userGroupRepo = Substitute.For<IRepository<UserGroup>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private GroupService CreateSut() => new(_groupRepo, _userGroupRepo, _userRepo, _unitOfWork);

        [Fact]
        public async Task GetGroupsAsync_ReturnsEachGroupWithItsMemberCount()
        {
            _groupRepo.Query().Returns(new List<Group>
            {
                new() { Id = 1, Name = "Marketing", Description = "Marketing team" },
                new() { Id = 2, Name = "Empty Group" },
            }.BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>
            {
                new() { GroupId = 1, UserId = 10 },
                new() { GroupId = 1, UserId = 11 },
            }.BuildMock());

            var result = await CreateSut().GetGroupsAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result.Single(g => g.Id == 1).MemberCount);
            Assert.Equal(0, result.Single(g => g.Id == 2).MemberCount);
        }

        [Fact]
        public async Task CreateGroupAsync_AddsAGroupWithZeroMembers()
        {
            Group? added = null;
            _groupRepo.When(r => r.Add(Arg.Any<Group>())).Do(ci =>
            {
                added = ci.Arg<Group>();
                added.Id = 5;
            });

            var sut = CreateSut();
            var result = await sut.CreateGroupAsync(new CreateGroupDto { Name = "Q3 Launch Team", Description = "Ad-hoc" });

            Assert.Equal("Q3 Launch Team", result.Name);
            Assert.Equal(0, result.MemberCount);
            Assert.NotNull(added);
            Assert.Equal("Q3 Launch Team", added!.Name);
        }

        [Fact]
        public async Task UpdateGroupAsync_WhenGroupDoesNotExist_ReturnsFalse()
        {
            _groupRepo.Query().Returns(new List<Group>().BuildMock());

            var updated = await CreateSut().UpdateGroupAsync(999, new UpdateGroupDto { Name = "New Name" });

            Assert.False(updated);
        }

        [Fact]
        public async Task UpdateGroupAsync_WhenGroupExists_UpdatesNameAndDescription()
        {
            var group = new Group { Id = 1, Name = "Old Name", Description = "Old" };
            _groupRepo.Query().Returns(new List<Group> { group }.BuildMock());

            var updated = await CreateSut().UpdateGroupAsync(1, new UpdateGroupDto { Name = "New Name", Description = "New" });

            Assert.True(updated);
            Assert.Equal("New Name", group.Name);
            Assert.Equal("New", group.Description);
        }

        [Fact]
        public async Task GetGroupMembersAsync_WhenGroupDoesNotExist_ReturnsNull()
        {
            _groupRepo.Query().Returns(new List<Group>().BuildMock());

            var result = await CreateSut().GetGroupMembersAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetGroupMembersAsync_ReturnsMemberUserIds()
        {
            _groupRepo.Query().Returns(new List<Group> { new() { Id = 1, Name = "Marketing" } }.BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>
            {
                new() { GroupId = 1, UserId = 10 },
                new() { GroupId = 1, UserId = 11 },
                new() { GroupId = 2, UserId = 99 }, // different group - must not leak in
            }.BuildMock());

            var result = await CreateSut().GetGroupMembersAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new[] { 10, 11 }, result);
        }

        [Fact]
        public async Task SetGroupMembersAsync_WhenGroupDoesNotExist_ReturnsFalse()
        {
            _groupRepo.Query().Returns(new List<Group>().BuildMock());

            var updated = await CreateSut().SetGroupMembersAsync(999, new List<int> { 1 });

            Assert.False(updated);
        }

        [Fact]
        public async Task SetGroupMembersAsync_WhenAUserIdDoesNotExist_ReturnsFalseAndNeverReplacesMembership()
        {
            _groupRepo.Query().Returns(new List<Group> { new() { Id = 1, Name = "Marketing" } }.BuildMock());
            _userRepo.Query().Returns(new List<User> { new() { Id = 10, Name = "Real User" } }.BuildMock());

            var updated = await CreateSut().SetGroupMembersAsync(1, new List<int> { 10, 999 });

            Assert.False(updated);
            _userGroupRepo.DidNotReceive().RemoveRange(Arg.Any<IEnumerable<UserGroup>>());
            _userGroupRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<UserGroup>>());
        }

        [Fact]
        public async Task SetGroupMembersAsync_FullyReplacesExistingMembershipWithTheNewSet()
        {
            _groupRepo.Query().Returns(new List<Group> { new() { Id = 1, Name = "Marketing" } }.BuildMock());
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 10, Name = "A" },
                new() { Id = 11, Name = "B" },
            }.BuildMock());
            var existing = new List<UserGroup> { new() { GroupId = 1, UserId = 99 } };
            _userGroupRepo.Query().Returns(existing.BuildMock());

            var updated = await CreateSut().SetGroupMembersAsync(1, new List<int> { 10, 11 });

            Assert.True(updated);
            _userGroupRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<UserGroup>>(rows => rows.Count() == 1 && rows.First().UserId == 99));
            _userGroupRepo.Received(1).AddRange(Arg.Is<IEnumerable<UserGroup>>(rows =>
                rows.Count() == 2 && rows.All(ug => ug.GroupId == 1) && rows.Select(ug => ug.UserId).OrderBy(id => id).SequenceEqual(new[] { 10, 11 })));
        }
    }
}
