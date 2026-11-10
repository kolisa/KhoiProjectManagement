using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class PmWorkflowServiceTests
    {
        private static readonly PmCaller Sm = new(10, false);

        private readonly IRepository<WorkflowRestriction> _restrictions = Substitute.For<IRepository<WorkflowRestriction>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private PmWorkflowService CreateSut() => new(_restrictions, _access, _uow);
        private void SetRestrictions(params WorkflowRestriction[] r) => _restrictions.Query().Returns(_ => r.ToList().BuildMock());

        private static WorkflowTransitionDto T(string from, string to, bool allowed) => new() { From = from, To = to, Allowed = allowed };

        public PmWorkflowServiceTests() => SetRestrictions();

        [Fact]
        public async Task Get_WithNoRules_AllowsEveryMove()
        {
            var wf = await CreateSut().GetAsync(Sm, 1);

            Assert.False(wf.IsCustomised);
            Assert.Equal(12, wf.Transitions.Count); // 4 statuses x 3 targets
            Assert.All(wf.Transitions, t => Assert.True(t.Allowed));
        }

        [Fact]
        public async Task Get_ReflectsStoredRestrictions()
        {
            SetRestrictions(new WorkflowRestriction { ProjectId = 1, FromStatus = "todo", ToStatus = "completed" });
            var wf = await CreateSut().GetAsync(Sm, 1);

            Assert.True(wf.IsCustomised);
            Assert.False(wf.Transitions.Single(t => t.From == "todo" && t.To == "completed").Allowed);
            Assert.True(wf.Transitions.Single(t => t.From == "todo" && t.To == "in-progress").Allowed);
        }

        [Fact]
        public async Task Save_WithoutPermission_IsForbidden()
        {
            _access.RequireAsync(Sm, 1, PmAction.ManageBoard).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto()));
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task Save_StoresOnlyTheDisallowedPairs_ReplacingOldRules()
        {
            var old = new WorkflowRestriction { ProjectId = 1, FromStatus = "blocked", ToStatus = "todo" };
            SetRestrictions(old);
            var added = new List<WorkflowRestriction>();
            _restrictions.When(r => r.Add(Arg.Any<WorkflowRestriction>())).Do(ci => added.Add(ci.Arg<WorkflowRestriction>()));

            await CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto
            {
                Transitions = { T("todo", "completed", false), T("todo", "in-progress", true), T("todo", "todo", false) },
            });

            _restrictions.Received(1).RemoveRange(Arg.Is<IEnumerable<WorkflowRestriction>>(e => e.Contains(old)));
            var rule = Assert.Single(added);
            Assert.Equal(("todo", "completed"), (rule.FromStatus, rule.ToStatus));
            Assert.Equal(1, rule.ProjectId);
        }

        [Fact]
        public async Task Save_UnknownStatus_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto { Transitions = { T("todo", "archived", false) } }));

        [Fact]
        public async Task Save_ThatStrandsWorkFromReachingDone_IsRejected()
        {
            // in-progress -> done and in-progress -> todo both blocked, but blocked -> done is open and
            // in-progress -> blocked is open, so it still reaches Done: allowed.
            await CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto
            {
                Transitions = { T("in-progress", "completed", false), T("in-progress", "todo", false) },
            });

            // Now also close in-progress -> blocked: nothing out of in-progress can reach Done.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto
            {
                Transitions = { T("in-progress", "completed", false), T("in-progress", "todo", false), T("in-progress", "blocked", false) },
            }));
            Assert.Contains("In progress", ex.Message);
        }

        [Fact]
        public async Task EnsureTransitionAllowed_BlockedPair_Throws_ButSameStatusAndOtherPairsPass()
        {
            SetRestrictions(new WorkflowRestriction { ProjectId = 1, FromStatus = "todo", ToStatus = "completed" });
            var sut = CreateSut();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnsureTransitionAllowedAsync(1, "todo", "completed"));
            Assert.Contains("To do", ex.Message);
            Assert.Contains("Done", ex.Message);
            await sut.EnsureTransitionAllowedAsync(1, "todo", "in-progress");
            await sut.EnsureTransitionAllowedAsync(1, "todo", "todo");
            await sut.EnsureTransitionAllowedAsync(2, "todo", "completed"); // a rule belongs to its own project
        }

        [Fact]
        public async Task Save_OfAnUnchangedRule_NeitherDeletesNorReinsertsIt()
        {
            SetRestrictions(new WorkflowRestriction { ProjectId = 1, FromStatus = "todo", ToStatus = "completed" });

            await CreateSut().SaveAsync(Sm, 1, new SaveWorkflowDto { Transitions = { T("todo", "completed", false) } });

            _restrictions.Received(1).RemoveRange(Arg.Is<IEnumerable<WorkflowRestriction>>(e => !e.Any()));
            _restrictions.DidNotReceiveWithAnyArgs().Add(default!);
        }
    }

    public class PmRetroServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<RetroItem> _items = Substitute.For<IRepository<RetroItem>>();
        private readonly IRepository<Sprint> _sprints = Substitute.For<IRepository<Sprint>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        public PmRetroServiceTests()
        {
            SetSprint(SprintStates.Active);
            SetItems();
            _users.Query().Returns(_ => new List<User> { new() { Id = 10, Name = "Dana" } }.BuildMock());
            _access.GetRoleAsync(default!, default).ReturnsForAnyArgs(ProjectRoles.Developer);
        }

        private PmRetroService CreateSut() => new(_items, _sprints, _users, _access, _uow);
        private void SetSprint(string state) => _sprints.Query().Returns(_ => new List<Sprint> { new() { Id = 1, ProjectId = 1, State = state } }.BuildMock());
        private void SetItems(params RetroItem[] i) => _items.Query().Returns(_ => i.ToList().BuildMock());

        [Fact]
        public async Task Add_ToAPlannedSprint_IsRejected()
        {
            SetSprint(SprintStates.Planned);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAsync(Dev, 1, new SaveRetroItemDto { Text = "x" }));
            _items.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task Add_AsViewer_IsForbidden()
        {
            _access.RequireAsync(Dev, 1, PmAction.Collaborate).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().AddAsync(Dev, 1, new SaveRetroItemDto { Text = "x" }));
        }

        [Fact]
        public async Task Add_OnACompletedSprint_IsAllowed_AndTrimsText()
        {
            SetSprint(SprintStates.Completed);
            RetroItem? added = null;
            _items.When(r => r.Add(Arg.Any<RetroItem>())).Do(ci => added = ci.Arg<RetroItem>());

            var dto = await CreateSut().AddAsync(Dev, 1, new SaveRetroItemDto { Category = RetroCategories.Action, Text = "  Pair more  " });

            Assert.Equal("Pair more", added!.Text);
            Assert.Equal(10, added.AuthoredBy);
            Assert.Equal("Dana", dto.AuthorName);
            Assert.True(dto.CanEdit);
        }

        [Fact]
        public async Task Get_FlagsWhichCardsTheCallerMayEditOrDelete()
        {
            SetItems(
                new RetroItem { Id = 1, SprintId = 1, AuthoredBy = 10, Category = "WentWell", Text = "mine", Author = new User { Name = "Dana" } },
                new RetroItem { Id = 2, SprintId = 1, AuthoredBy = 99, Category = "ToImprove", Text = "theirs", Author = new User { Name = "Sam" } });

            var rows = await CreateSut().GetAsync(Dev, 1);

            Assert.True(rows[0].CanEdit && rows[0].CanDelete);
            Assert.False(rows[1].CanEdit);
            Assert.False(rows[1].CanDelete); // a Developer cannot moderate
        }

        [Fact]
        public async Task Get_ScrumMasterCanDeleteOthersCards()
        {
            _access.GetRoleAsync(default!, default).ReturnsForAnyArgs(ProjectRoles.ScrumMaster);
            SetItems(new RetroItem { Id = 2, SprintId = 1, AuthoredBy = 99, Category = "ToImprove", Text = "theirs", Author = new User { Name = "Sam" } });

            var row = Assert.Single(await CreateSut().GetAsync(Dev, 1));

            Assert.False(row.CanEdit);
            Assert.True(row.CanDelete);
        }

        [Fact]
        public async Task Update_OthersCard_IsForbidden()
        {
            SetItems(new RetroItem { Id = 1, SprintId = 1, AuthoredBy = 99, Text = "x" });
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().UpdateAsync(Dev, 1, new SaveRetroItemDto { Text = "y" }));
        }

        [Fact]
        public async Task SetDone_OnlyForActions_ByAnyCollaborator()
        {
            var action = new RetroItem { Id = 1, SprintId = 1, AuthoredBy = 99, Category = RetroCategories.Action, Text = "a" };
            SetItems(action);
            await CreateSut().SetDoneAsync(Dev, 1, true);
            Assert.True(action.IsDone);

            SetItems(new RetroItem { Id = 2, SprintId = 1, AuthoredBy = 99, Category = RetroCategories.WentWell, Text = "b" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetDoneAsync(Dev, 2, true));
        }

        [Fact]
        public async Task Delete_OthersCard_NeedsManageSprint()
        {
            SetItems(new RetroItem { Id = 1, SprintId = 1, AuthoredBy = 99, Text = "x" });
            _access.RequireAsync(Dev, 1, PmAction.ManageSprint).Returns(Task.FromException(new ForbiddenException()));

            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().DeleteAsync(Dev, 1));
            _items.DidNotReceiveWithAnyArgs().Remove(default!);
        }

        [Fact]
        public async Task Delete_OwnCard_Removes()
        {
            SetItems(new RetroItem { Id = 1, SprintId = 1, AuthoredBy = 10, Text = "x" });
            await CreateSut().DeleteAsync(Dev, 1);
            _items.Received(1).Remove(Arg.Any<RetroItem>());
        }
    }
}
