using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class DashboardWidgetServiceTests
    {
        private readonly IRepository<DashboardWidgetAllowlist> _allowlistRepo = Substitute.For<IRepository<DashboardWidgetAllowlist>>();
        private readonly IRepository<DashboardWidgetPreference> _preferenceRepo = Substitute.For<IRepository<DashboardWidgetPreference>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private DashboardWidgetService CreateSut() => new(_allowlistRepo, _preferenceRepo, _unitOfWork);

        private static void SetNoAllowlistRows(IRepository<DashboardWidgetAllowlist> allowlistRepo) =>
            allowlistRepo.Query().Returns(new List<DashboardWidgetAllowlist>().BuildMock());

        private static void SetNoPreferences(IRepository<DashboardWidgetPreference> preferenceRepo) =>
            preferenceRepo.Query().Returns(new List<DashboardWidgetPreference>().BuildMock());

        [Fact]
        public async Task GetCatalogAsync_WhenNoAllowlistRowsExist_AllCatalogWidgetsAreEnabledByDefault()
        {
            SetNoAllowlistRows(_allowlistRepo);

            var result = await CreateSut().GetCatalogAsync();

            Assert.Equal(DashboardWidgetTypes.Catalog.Count, result.Count);
            Assert.All(result, w => Assert.True(w.IsEnabled));
        }

        [Fact]
        public async Task GetCatalogAsync_ReflectsAnExplicitAllowlistOverride()
        {
            _allowlistRepo.Query().Returns(new List<DashboardWidgetAllowlist>
            {
                new() { WidgetKey = DashboardWidgetTypes.OverdueTasks, IsEnabled = false },
            }.BuildMock());

            var result = await CreateSut().GetCatalogAsync();

            Assert.False(result.Single(w => w.WidgetKey == DashboardWidgetTypes.OverdueTasks).IsEnabled);
            Assert.True(result.Single(w => w.WidgetKey == DashboardWidgetTypes.TotalProjects).IsEnabled);
        }

        [Fact]
        public async Task GetCatalogAsync_OrdersEntriesByCatalogOrder()
        {
            SetNoAllowlistRows(_allowlistRepo);

            var result = await CreateSut().GetCatalogAsync();

            var expectedOrder = DashboardWidgetTypes.Catalog.OrderBy(c => c.CatalogOrder).Select(c => c.Key).ToList();
            Assert.Equal(expectedOrder, result.Select(w => w.WidgetKey));
        }

        [Fact]
        public async Task SetAllowlistAsync_WhenWidgetKeyIsUnknown_ThrowsInvalidOperationException()
        {
            var updates = new List<SetWidgetAllowlistDto> { new() { WidgetKey = "not_a_real_widget", IsEnabled = true } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetAllowlistAsync(updates));
        }

        [Fact]
        public async Task SetAllowlistAsync_WhenNoExistingRowForTheWidget_AddsANewAllowlistRow()
        {
            SetNoAllowlistRows(_allowlistRepo);
            var updates = new List<SetWidgetAllowlistDto> { new() { WidgetKey = DashboardWidgetTypes.OverdueTasks, IsEnabled = false } };

            await CreateSut().SetAllowlistAsync(updates);

            _allowlistRepo.Received(1).Add(Arg.Is<DashboardWidgetAllowlist>(a =>
                a.WidgetKey == DashboardWidgetTypes.OverdueTasks && a.IsEnabled == false));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task SetAllowlistAsync_WhenARowAlreadyExistsForTheWidget_UpdatesItInPlaceRatherThanAddingADuplicate()
        {
            var existing = new DashboardWidgetAllowlist { Id = 1, WidgetKey = DashboardWidgetTypes.OverdueTasks, IsEnabled = true };
            _allowlistRepo.Query().Returns(new List<DashboardWidgetAllowlist> { existing }.BuildMock());
            var updates = new List<SetWidgetAllowlistDto> { new() { WidgetKey = DashboardWidgetTypes.OverdueTasks, IsEnabled = false } };

            await CreateSut().SetAllowlistAsync(updates);

            Assert.False(existing.IsEnabled);
            _allowlistRepo.DidNotReceive().Add(Arg.Any<DashboardWidgetAllowlist>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetMyPreferencesAsync_WhenUserHasNoPreferences_ReturnsAllEnabledWidgetsWithCatalogDefaults()
        {
            SetNoAllowlistRows(_allowlistRepo);
            SetNoPreferences(_preferenceRepo);

            var result = await CreateSut().GetMyPreferencesAsync(userId: 1);

            Assert.Equal(DashboardWidgetTypes.Catalog.Count, result.Count);
            Assert.All(result, w => Assert.True(w.IsVisible));
            var expectedOrder = DashboardWidgetTypes.Catalog.OrderBy(c => c.CatalogOrder).Select(c => c.Key).ToList();
            Assert.Equal(expectedOrder, result.Select(w => w.WidgetKey));
        }

        [Fact]
        public async Task GetMyPreferencesAsync_ExcludesAWidgetTheAllowlistHasSinceDisabled_EvenIfTheUserPreviouslySetAPreferenceForIt()
        {
            _allowlistRepo.Query().Returns(new List<DashboardWidgetAllowlist>
            {
                new() { WidgetKey = DashboardWidgetTypes.OverdueTasks, IsEnabled = false },
            }.BuildMock());
            _preferenceRepo.Query().Returns(new List<DashboardWidgetPreference>
            {
                new() { UserId = 1, WidgetKey = DashboardWidgetTypes.OverdueTasks, IsVisible = true, SortOrder = 0 },
            }.BuildMock());

            var result = await CreateSut().GetMyPreferencesAsync(userId: 1);

            Assert.DoesNotContain(result, w => w.WidgetKey == DashboardWidgetTypes.OverdueTasks);
        }

        [Fact]
        public async Task GetMyPreferencesAsync_AppliesTheUsersOwnVisibilityAndSortOrderPreference()
        {
            SetNoAllowlistRows(_allowlistRepo);
            _preferenceRepo.Query().Returns(new List<DashboardWidgetPreference>
            {
                new() { UserId = 1, WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = false, SortOrder = 99 },
            }.BuildMock());

            var result = await CreateSut().GetMyPreferencesAsync(userId: 1);

            var widget = result.Single(w => w.WidgetKey == DashboardWidgetTypes.TotalProjects);
            Assert.False(widget.IsVisible);
            Assert.Equal(99, widget.SortOrder);
            Assert.Equal(DashboardWidgetTypes.TotalProjects, result.Last().WidgetKey); // SortOrder 99 sorts to the end
        }

        [Fact]
        public async Task GetMyPreferencesAsync_OnlyAppliesPreferencesBelongingToTheRequestedUser()
        {
            SetNoAllowlistRows(_allowlistRepo);
            _preferenceRepo.Query().Returns(new List<DashboardWidgetPreference>
            {
                new() { UserId = 2, WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = false, SortOrder = 99 },
            }.BuildMock());

            var result = await CreateSut().GetMyPreferencesAsync(userId: 1);

            var widget = result.Single(w => w.WidgetKey == DashboardWidgetTypes.TotalProjects);
            Assert.True(widget.IsVisible); // user 1 has no preference row, so the other user's row must not leak in
            Assert.Equal(0, widget.SortOrder); // catalog default
        }

        [Fact]
        public async Task SetMyPreferencesAsync_WhenWidgetKeyIsUnknown_ThrowsInvalidOperationException()
        {
            var updates = new List<SetWidgetPreferenceDto> { new() { WidgetKey = "not_a_real_widget", IsVisible = true, SortOrder = 0 } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetMyPreferencesAsync(1, updates));
        }

        [Fact]
        public async Task SetMyPreferencesAsync_WhenNoExistingPreferenceForThisUserAndWidget_AddsANewPreference()
        {
            SetNoPreferences(_preferenceRepo);
            var updates = new List<SetWidgetPreferenceDto>
            {
                new() { WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = false, SortOrder = 3 },
            };

            await CreateSut().SetMyPreferencesAsync(userId: 1, updates);

            _preferenceRepo.Received(1).Add(Arg.Is<DashboardWidgetPreference>(p =>
                p.UserId == 1 && p.WidgetKey == DashboardWidgetTypes.TotalProjects && p.IsVisible == false && p.SortOrder == 3));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task SetMyPreferencesAsync_WhenAPreferenceAlreadyExistsForThisUserAndWidget_UpdatesItInPlaceRatherThanAddingADuplicate()
        {
            var existing = new DashboardWidgetPreference { Id = 1, UserId = 1, WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = true, SortOrder = 0 };
            _preferenceRepo.Query().Returns(new List<DashboardWidgetPreference> { existing }.BuildMock());
            var updates = new List<SetWidgetPreferenceDto>
            {
                new() { WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = false, SortOrder = 7 },
            };

            await CreateSut().SetMyPreferencesAsync(userId: 1, updates);

            Assert.False(existing.IsVisible);
            Assert.Equal(7, existing.SortOrder);
            _preferenceRepo.DidNotReceive().Add(Arg.Any<DashboardWidgetPreference>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task SetMyPreferencesAsync_ScopesTheUpdateToTheGivenUser_LeavingAnotherUsersPreferenceForTheSameWidgetUntouched()
        {
            // Same widget key, but the existing row belongs to a different user - the (UserId, WidgetKey)
            // uniqueness means this must not be matched, so a brand-new row is added for user 1 instead.
            var otherUsersPref = new DashboardWidgetPreference { Id = 1, UserId = 2, WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = true, SortOrder = 0 };
            _preferenceRepo.Query().Returns(new List<DashboardWidgetPreference> { otherUsersPref }.BuildMock());
            var updates = new List<SetWidgetPreferenceDto>
            {
                new() { WidgetKey = DashboardWidgetTypes.TotalProjects, IsVisible = false, SortOrder = 5 },
            };

            await CreateSut().SetMyPreferencesAsync(userId: 1, updates);

            _preferenceRepo.Received(1).Add(Arg.Is<DashboardWidgetPreference>(p => p.UserId == 1 && p.SortOrder == 5));
            Assert.True(otherUsersPref.IsVisible); // untouched
            Assert.Equal(0, otherUsersPref.SortOrder);
        }
    }
}
