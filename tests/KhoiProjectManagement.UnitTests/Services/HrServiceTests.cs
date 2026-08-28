using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class HrServiceTests
    {
        private readonly IRepository<OnboardingTemplate> _templateRepo = Substitute.For<IRepository<OnboardingTemplate>>();
        private readonly IRepository<OnboardingTemplateItem> _templateItemRepo = Substitute.For<IRepository<OnboardingTemplateItem>>();
        private readonly IRepository<OnboardingChecklist> _checklistRepo = Substitute.For<IRepository<OnboardingChecklist>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private HrService CreateSut() => new(_templateRepo, _templateItemRepo, _checklistRepo, _unitOfWork);

        private static ClaimsPrincipal CallerWithId(int userId, params string[] permissions)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        // ---- Templates ----

        [Fact]
        public async Task GetTemplatesAsync_ReturnsTemplateItemsOrderedBySortOrder()
        {
            var template = new OnboardingTemplate
            {
                Id = 1,
                Name = "Standard Onboarding",
                IsActive = true,
                Items = new List<OnboardingTemplateItem>
                {
                    new() { Id = 10, Title = "Third", SortOrder = 2 },
                    new() { Id = 11, Title = "First", SortOrder = 0 },
                    new() { Id = 12, Title = "Second", SortOrder = 1 },
                }
            };
            _templateRepo.Query().Returns(new List<OnboardingTemplate> { template }.BuildMock());

            var result = await CreateSut().GetTemplatesAsync();

            Assert.Single(result);
            Assert.Equal(new[] { "First", "Second", "Third" }, result[0].Items.Select(i => i.Title));
        }

        [Fact]
        public async Task CreateTemplateAsync_AddsTemplateWithItemsInGivenOrder()
        {
            OnboardingTemplate? added = null;
            _templateRepo.When(r => r.Add(Arg.Any<OnboardingTemplate>())).Do(ci => added = ci.Arg<OnboardingTemplate>());

            var dto = new CreateOnboardingTemplateDto
            {
                Name = "Standard Onboarding",
                ItemTitles = new List<string> { "Sign contract", "Laptop setup", "Meet the team" }
            };

            var result = await CreateSut().CreateTemplateAsync(dto);

            Assert.NotNull(added);
            Assert.Equal("Standard Onboarding", added!.Name);
            Assert.Equal(new[] { "Sign contract", "Laptop setup", "Meet the team" }, added.Items.Select(i => i.Title));
            Assert.Equal(new[] { 0, 1, 2 }, added.Items.Select(i => i.SortOrder));
            await _unitOfWork.Received(1).SaveChangesAsync();

            Assert.Equal("Standard Onboarding", result.Name);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task UpdateTemplateAsync_WhenTemplateDoesNotExist_ReturnsFalse()
        {
            _templateRepo.Query().Returns(new List<OnboardingTemplate>().BuildMock());

            var updated = await CreateSut().UpdateTemplateAsync(999, new UpdateOnboardingTemplateDto
            {
                Name = "X",
                IsActive = true,
                ItemTitles = new List<string>()
            });

            Assert.False(updated);
        }

        [Fact]
        public async Task UpdateTemplateAsync_WhenTemplateExists_ReplacesNameActiveFlagAndItems()
        {
            var oldItem1 = new OnboardingTemplateItem { Id = 1, Title = "Old 1", SortOrder = 0 };
            var oldItem2 = new OnboardingTemplateItem { Id = 2, Title = "Old 2", SortOrder = 1 };
            var template = new OnboardingTemplate
            {
                Id = 5,
                Name = "Old Name",
                IsActive = true,
                Items = new List<OnboardingTemplateItem> { oldItem1, oldItem2 }
            };
            _templateRepo.Query().Returns(new List<OnboardingTemplate> { template }.BuildMock());

            var dto = new UpdateOnboardingTemplateDto
            {
                Name = "New Name",
                IsActive = false,
                ItemTitles = new List<string> { "New 1", "New 2", "New 3" }
            };

            var updated = await CreateSut().UpdateTemplateAsync(5, dto);

            Assert.True(updated);
            Assert.Equal("New Name", template.Name);
            Assert.False(template.IsActive);
            Assert.Equal(new[] { "New 1", "New 2", "New 3" }, template.Items.Select(i => i.Title));
            Assert.All(template.Items, i => Assert.Equal(5, i.TemplateId));
            _templateItemRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<OnboardingTemplateItem>>(items =>
                items.Count() == 2 && items.Contains(oldItem1) && items.Contains(oldItem2)));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- Checklists: reads ----

        [Fact]
        public async Task GetChecklistsAsync_WhenUserIdNotSpecified_ReturnsCallersOwnChecklistsOnly()
        {
            var checklists = new List<OnboardingChecklist>
            {
                new()
                {
                    Id = 1, UserId = 7, User = new User { Id = 7, Name = "Jane" },
                    Template = new OnboardingTemplate { Id = 1, Name = "T" },
                    Items = new List<OnboardingChecklistItem>()
                },
                new()
                {
                    Id = 2, UserId = 8, User = new User { Id = 8, Name = "Bob" },
                    Template = new OnboardingTemplate { Id = 1, Name = "T" },
                    Items = new List<OnboardingChecklistItem>()
                },
            };
            _checklistRepo.Query().Returns(checklists.BuildMock());

            var result = await CreateSut().GetChecklistsAsync(null, CallerWithId(7));

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetChecklistsAsync_WhenRequestingAnotherUsersChecklistsWithoutPermission_Throws()
        {
            _checklistRepo.Query().Returns(new List<OnboardingChecklist>().BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateSut().GetChecklistsAsync(8, CallerWithId(7)));
        }

        [Fact]
        public async Task GetChecklistsAsync_WhenRequestingAnotherUsersChecklistsWithHrViewPermission_ReturnsThem()
        {
            var checklists = new List<OnboardingChecklist>
            {
                new()
                {
                    Id = 1, UserId = 8, User = new User { Id = 8, Name = "Bob" },
                    Template = new OnboardingTemplate { Id = 1, Name = "T" },
                    Items = new List<OnboardingChecklistItem>()
                },
            };
            _checklistRepo.Query().Returns(checklists.BuildMock());

            var result = await CreateSut().GetChecklistsAsync(8, CallerWithId(7, "hr.view"));

            Assert.Single(result);
        }

        [Fact]
        public async Task GetChecklistByIdAsync_WhenChecklistDoesNotExist_ReturnsNull()
        {
            _checklistRepo.Query().Returns(new List<OnboardingChecklist>().BuildMock());

            var result = await CreateSut().GetChecklistByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetChecklistByIdAsync_WhenCallerLacksAccess_Throws()
        {
            var checklist = new OnboardingChecklist
            {
                Id = 1, UserId = 8, User = new User { Id = 8, Name = "Bob" },
                Template = new OnboardingTemplate { Id = 1, Name = "T" },
                Items = new List<OnboardingChecklistItem>()
            };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateSut().GetChecklistByIdAsync(1, CallerWithId(7)));
        }

        [Fact]
        public async Task GetChecklistByIdAsync_WhenCallerOwnsChecklist_ReturnsIt()
        {
            var checklist = new OnboardingChecklist
            {
                Id = 1, UserId = 7, User = new User { Id = 7, Name = "Jane" },
                Template = new OnboardingTemplate { Id = 1, Name = "T" },
                Items = new List<OnboardingChecklistItem>()
            };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var result = await CreateSut().GetChecklistByIdAsync(1, CallerWithId(7));

            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
        }

        // ---- Checklists: create (template instantiation) ----

        [Fact]
        public async Task CreateChecklistAsync_WhenTemplateDoesNotExist_Throws()
        {
            _templateRepo.Query().Returns(new List<OnboardingTemplate>().BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().CreateChecklistAsync(new CreateOnboardingChecklistDto { UserId = 1, TemplateId = 99 }));
        }

        [Fact]
        public async Task CreateChecklistAsync_CopiesTemplateItemsInSortOrderAsFreshIncompleteChecklistItems()
        {
            var template = new OnboardingTemplate
            {
                Id = 3,
                Name = "New Hire",
                Items = new List<OnboardingTemplateItem>
                {
                    new() { Id = 1, Title = "Sign contract", SortOrder = 1 },
                    new() { Id = 2, Title = "Laptop setup", SortOrder = 0 },
                }
            };
            _templateRepo.Query().Returns(new List<OnboardingTemplate> { template }.BuildMock());

            var user = new User { Id = 7, Name = "Jane Doe" };

            OnboardingChecklist? added = null;
            _checklistRepo.When(r => r.Add(Arg.Any<OnboardingChecklist>())).Do(ci =>
            {
                added = ci.Arg<OnboardingChecklist>();
                added.Id = 10;
                added.User = user;
                added.Template = template;
                // Simulates the service's follow-up Query()...FirstAsync() re-fetch of the saved,
                // now-navigation-populated row.
                _checklistRepo.Query().Returns(new List<OnboardingChecklist> { added }.BuildMock());
            });

            var result = await CreateSut().CreateChecklistAsync(new CreateOnboardingChecklistDto { UserId = 7, TemplateId = 3 });

            Assert.Equal("Jane Doe", result.UserName);
            Assert.Equal("New Hire", result.TemplateName);
            // Copied in template SortOrder (Laptop setup=0 before Sign contract=1), not list order.
            Assert.Equal(new[] { "Laptop setup", "Sign contract" }, result.Items.Select(i => i.Title));
            Assert.All(result.Items, i => Assert.False(i.IsComplete));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- Checklists: item completion toggling ----

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenChecklistDoesNotExist_ReturnsFalse()
        {
            _checklistRepo.Query().Returns(new List<OnboardingChecklist>().BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                999, 1, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(1));

            Assert.False(updated);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenItemDoesNotExist_ReturnsFalse()
        {
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem>() };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 999, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(7));

            Assert.False(updated);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenCallerDoesNotOwnChecklistAndLacksHrManage_Throws()
        {
            var item = new OnboardingChecklistItem { Id = 1, Title = "Sign contract" };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateSut().UpdateChecklistItemAsync(1, 1, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(99)));
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenCallerHasHrManagePermission_CanUpdateAnotherUsersChecklistItem()
        {
            var item = new OnboardingChecklistItem { Id = 1, Title = "Sign contract" };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 1, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(99, "hr.manage"));

            Assert.True(updated);
            Assert.True(item.IsComplete);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenMarkingComplete_SetsCompletedAtAndCompletedByToCaller()
        {
            var item = new OnboardingChecklistItem { Id = 1, Title = "Sign contract", IsComplete = false };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 1, new UpdateChecklistItemDto { IsComplete = true, Notes = "Done" }, CallerWithId(7));

            Assert.True(updated);
            Assert.True(item.IsComplete);
            Assert.Equal("Done", item.Notes);
            Assert.NotNull(item.CompletedAt);
            Assert.Equal(7, item.CompletedBy);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenUncompletingAnItem_ClearsCompletedAtAndCompletedBy()
        {
            var item = new OnboardingChecklistItem
            {
                Id = 1, Title = "Sign contract", IsComplete = true,
                CompletedAt = DateTime.UtcNow, CompletedBy = 7
            };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 1, new UpdateChecklistItemDto { IsComplete = false }, CallerWithId(7));

            Assert.True(updated);
            Assert.False(item.IsComplete);
            Assert.Null(item.CompletedAt);
            Assert.Null(item.CompletedBy);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenAllItemsBecomeComplete_SetsChecklistCompletedAt()
        {
            var item1 = new OnboardingChecklistItem { Id = 1, Title = "A", IsComplete = true };
            var item2 = new OnboardingChecklistItem { Id = 2, Title = "B", IsComplete = false };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item1, item2 } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 2, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(7));

            Assert.True(updated);
            Assert.NotNull(checklist.CompletedAt);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenNotAllItemsAreComplete_ChecklistCompletedAtStaysNull()
        {
            var item1 = new OnboardingChecklistItem { Id = 1, Title = "A", IsComplete = false };
            var item2 = new OnboardingChecklistItem { Id = 2, Title = "B", IsComplete = false };
            var checklist = new OnboardingChecklist { Id = 1, UserId = 7, Items = new List<OnboardingChecklistItem> { item1, item2 } };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 1, new UpdateChecklistItemDto { IsComplete = true }, CallerWithId(7));

            Assert.True(updated);
            Assert.Null(checklist.CompletedAt);
        }

        [Fact]
        public async Task UpdateChecklistItemAsync_WhenUncompletingAnItemOnAPreviouslyCompleteChecklist_ClearsChecklistCompletedAt()
        {
            var item1 = new OnboardingChecklistItem { Id = 1, Title = "A", IsComplete = true };
            var item2 = new OnboardingChecklistItem { Id = 2, Title = "B", IsComplete = true };
            var checklist = new OnboardingChecklist
            {
                Id = 1, UserId = 7, CompletedAt = DateTime.UtcNow,
                Items = new List<OnboardingChecklistItem> { item1, item2 }
            };
            _checklistRepo.Query().Returns(new List<OnboardingChecklist> { checklist }.BuildMock());

            var updated = await CreateSut().UpdateChecklistItemAsync(
                1, 1, new UpdateChecklistItemDto { IsComplete = false }, CallerWithId(7));

            Assert.True(updated);
            Assert.Null(checklist.CompletedAt);
        }
    }
}
