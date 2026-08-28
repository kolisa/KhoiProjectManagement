using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class WikiServiceTests
    {
        private readonly IRepository<WikiPage> _pageRepo = Substitute.For<IRepository<WikiPage>>();
        private readonly IRepository<UserRole> _userRoleRepo = Substitute.For<IRepository<UserRole>>();
        private readonly IRepository<UserGroup> _userGroupRepo = Substitute.For<IRepository<UserGroup>>();
        private readonly IRepository<WikiPageVersion> _versionRepo = Substitute.For<IRepository<WikiPageVersion>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<WikiPageTag> _pageTagRepo = Substitute.For<IRepository<WikiPageTag>>();
        private readonly IRepository<Tag> _tagRepo = Substitute.For<IRepository<Tag>>();
        private readonly IRepository<WikiPageComment> _commentRepo = Substitute.For<IRepository<WikiPageComment>>();
        private readonly IWikiSearchRepository _wikiSearchRepo = Substitute.For<IWikiSearchRepository>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly ISpacePermissionResolver _spacePermissionResolver = Substitute.For<ISpacePermissionResolver>();
        private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();

        private WikiService CreateSut() => new(
            _pageRepo,
            _userRoleRepo,
            _userGroupRepo,
            _versionRepo,
            _userRepo,
            _pageTagRepo,
            _tagRepo,
            _commentRepo,
            _wikiSearchRepo,
            _unitOfWork,
            _authorizationService,
            _spacePermissionResolver,
            _notificationService,
            _emailService,
            _configuration);

        private static ClaimsPrincipal CallerWithId(int userId) =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }));

        private void SetAuthorizationResult(bool succeeds) =>
            _authorizationService
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
                .Returns(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        private void SetNoUsersOrRoles()
        {
            _userRepo.Query().Returns(new List<User>().BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());
        }

        // ---------- GetPagesAsync / WordCount ----------

        [Fact]
        public async Task GetPagesAsync_WhenCurrentContentMarkdownIsNull_WordCountIsZeroRatherThanThrowing()
        {
            SetAuthorizationResult(succeeds: true);
            var page = new WikiPage { Id = 1, Title = "Empty Page", SpaceId = 10, CurrentContentMarkdown = null, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            _versionRepo.Query().Returns(new List<WikiPageVersion>().BuildMock());

            var result = await CreateSut().GetPagesAsync(10, null, CallerWithId(1));

            Assert.Single(result);
            Assert.Equal(0, result[0].WordCount);
        }

        [Fact]
        public async Task GetPagesAsync_ComputesWordCountByWhitespaceSplittingCurrentContent()
        {
            SetAuthorizationResult(succeeds: true);
            var page = new WikiPage
            {
                Id = 1,
                Title = "Page",
                SpaceId = 10,
                CurrentContentMarkdown = "The quick brown fox\njumps over   the lazy dog",
                IsActive = true
            };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            _versionRepo.Query().Returns(new List<WikiPageVersion>().BuildMock());

            var result = await CreateSut().GetPagesAsync(10, null, CallerWithId(1));

            Assert.Equal(9, result[0].WordCount);
        }

        [Fact]
        public async Task GetPagesAsync_WhenCurrentContentMarkdownIsEmptyString_WordCountIsZero()
        {
            SetAuthorizationResult(succeeds: true);
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, CurrentContentMarkdown = "", IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            _versionRepo.Query().Returns(new List<WikiPageVersion>().BuildMock());

            var result = await CreateSut().GetPagesAsync(10, null, CallerWithId(1));

            Assert.Equal(0, result[0].WordCount);
        }

        [Fact]
        public async Task GetPagesAsync_ReturnsLabelsOrderedAlphabeticallyFromWikiPageTags()
        {
            SetAuthorizationResult(succeeds: true);
            var tagZ = new Tag { Id = 1, Name = "zebra" };
            var tagA = new Tag { Id = 2, Name = "alpha" };
            var page = new WikiPage
            {
                Id = 1,
                Title = "Page",
                SpaceId = 10,
                IsActive = true,
                WikiPageTags = new List<WikiPageTag>
                {
                    new() { WikiPageId = 1, TagId = 1, Tag = tagZ },
                    new() { WikiPageId = 1, TagId = 2, Tag = tagA },
                }
            };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            _versionRepo.Query().Returns(new List<WikiPageVersion>().BuildMock());

            var result = await CreateSut().GetPagesAsync(10, null, CallerWithId(1));

            Assert.Equal(new[] { "alpha", "zebra" }, result[0].Labels);
        }

        [Fact]
        public async Task GetPagesAsync_UpdatedAtComesFromTheLatestVersionsEditedAt()
        {
            SetAuthorizationResult(succeeds: true);
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            var olderEditedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var newerEditedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 1, EditedAt = olderEditedAt },
                new() { WikiPageId = 1, VersionNumber = 2, EditedAt = newerEditedAt },
            }.BuildMock());

            var result = await CreateSut().GetPagesAsync(10, null, CallerWithId(1));

            Assert.Equal(newerEditedAt, result[0].UpdatedAt);
        }

        [Fact]
        public async Task GetPagesAsync_WhenCallerLacksSpaceAccess_ThrowsUnauthorized()
        {
            SetAuthorizationResult(succeeds: false);
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().GetPagesAsync(10, null, CallerWithId(1)));
        }

        // ---------- GetPageByIdAsync ----------

        [Fact]
        public async Task GetPageByIdAsync_WhenPageDoesNotExist_ReturnsNullWithoutAuthorizing()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().GetPageByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        [Fact]
        public async Task GetPageByIdAsync_WhenPageIsInactive_ReturnsNull()
        {
            var page = new WikiPage { Id = 1, Title = "Deleted", SpaceId = 10, IsActive = false };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());

            var result = await CreateSut().GetPageByIdAsync(1, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPageByIdAsync_WhenCallerLacksReadAccess_ThrowsUnauthorized()
        {
            var page = new WikiPage { Id = 1, Title = "Secret", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().GetPageByIdAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task GetPageByIdAsync_WhenAuthorized_ReturnsContentFromLatestVersion()
        {
            var page = new WikiPage
            {
                Id = 1,
                Title = "Page",
                SpaceId = 10,
                IsActive = true,
                Creator = new User { Id = 5, Name = "Creator" }
            };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 1, ContentMarkdown = "old" },
                new() { WikiPageId = 1, VersionNumber = 2, ContentMarkdown = "new content" },
            }.BuildMock());

            var result = await CreateSut().GetPageByIdAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal("new content", result!.ContentMarkdown);
            Assert.Equal(2, result.CurrentVersionNumber);
            Assert.Equal("Creator", result.CreatorName);
        }

        // ---------- CreatePageAsync ----------

        [Fact]
        public async Task CreatePageAsync_WhenCallerLacksWriteOnSpace_ThrowsAndNeverAddsPage()
        {
            SetAuthorizationResult(succeeds: false);
            var dto = new CreateWikiPageDto { Title = "New Page", SpaceId = 10, ContentMarkdown = "content" };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().CreatePageAsync(dto, CallerWithId(1)));
            _pageRepo.DidNotReceive().Add(Arg.Any<WikiPage>());
            _versionRepo.DidNotReceive().Add(Arg.Any<WikiPageVersion>());
        }

        [Fact]
        public async Task CreatePageAsync_WhenAuthorized_CreatesPageAndFirstVersion()
        {
            SetAuthorizationResult(succeeds: true);
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author" });
            var dto = new CreateWikiPageDto { Title = "New Page", SpaceId = 10, ContentMarkdown = "hello world" };

            WikiPage? addedPage = null;
            _pageRepo.When(r => r.Add(Arg.Any<WikiPage>())).Do(ci => addedPage = ci.Arg<WikiPage>());
            WikiPageVersion? addedVersion = null;
            _versionRepo.When(r => r.Add(Arg.Any<WikiPageVersion>())).Do(ci => addedVersion = ci.Arg<WikiPageVersion>());

            var result = await CreateSut().CreatePageAsync(dto, CallerWithId(1));

            Assert.Equal("New Page", result.Title);
            Assert.Equal("Author", result.CreatorName);
            Assert.Equal(1, result.CurrentVersionNumber);
            Assert.NotNull(addedPage);
            Assert.Equal("hello world", addedPage!.CurrentContentMarkdown);
            Assert.Equal(1, addedPage.CreatedBy);
            Assert.NotNull(addedVersion);
            Assert.Equal(1, addedVersion!.VersionNumber);
        }

        [Fact]
        public async Task CreatePageAsync_PlacesNewPageAfterExistingSiblingsInSortOrder()
        {
            SetAuthorizationResult(succeeds: true);
            _pageRepo.Query().Returns(new List<WikiPage>
            {
                new() { Id = 1, SpaceId = 10, ParentPageId = null, SortOrder = 0, IsActive = true },
                new() { Id = 2, SpaceId = 10, ParentPageId = null, SortOrder = 3, IsActive = true },
            }.BuildMock());
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author" });
            WikiPage? addedPage = null;
            _pageRepo.When(r => r.Add(Arg.Any<WikiPage>())).Do(ci => addedPage = ci.Arg<WikiPage>());
            var dto = new CreateWikiPageDto { Title = "Third", SpaceId = 10, ContentMarkdown = "x" };

            await CreateSut().CreatePageAsync(dto, CallerWithId(1));

            Assert.Equal(4, addedPage!.SortOrder);
        }

        // ---------- UpdatePageAsync ----------

        [Fact]
        public async Task UpdatePageAsync_WhenPageDoesNotExist_ReturnsFalse()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().UpdatePageAsync(999, new UpdateWikiPageDto { Title = "X", ContentMarkdown = "y" }, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task UpdatePageAsync_WhenCallerLacksWriteAccess_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().UpdatePageAsync(1, new UpdateWikiPageDto { Title = "X", ContentMarkdown = "y" }, CallerWithId(1)));
        }

        [Fact]
        public async Task UpdatePageAsync_WhenContentChanges_CreatesANewVersionWithIncrementedNumber()
        {
            var page = new WikiPage { Id = 1, Title = "Old Title", SpaceId = 10, IsActive = true, CurrentContentMarkdown = "old content" };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 2, ContentMarkdown = "old content" }
            }.BuildMock());

            WikiPageVersion? addedVersion = null;
            _versionRepo.When(r => r.Add(Arg.Any<WikiPageVersion>())).Do(ci => addedVersion = ci.Arg<WikiPageVersion>());

            var dto = new UpdateWikiPageDto { Title = "New Title", ContentMarkdown = "new content", EditSummary = "Fixed typo" };
            var result = await CreateSut().UpdatePageAsync(1, dto, CallerWithId(7));

            Assert.True(result);
            Assert.Equal("New Title", page.Title);
            Assert.Equal("new content", page.CurrentContentMarkdown);
            Assert.Equal(7, page.UpdatedBy);
            Assert.NotNull(addedVersion);
            Assert.Equal(3, addedVersion!.VersionNumber);
            Assert.Equal("new content", addedVersion.ContentMarkdown);
            Assert.Equal("Fixed typo", addedVersion.EditSummary);
            Assert.Equal(7, addedVersion.EditedBy);
        }

        [Fact]
        public async Task UpdatePageAsync_WhenOnlyTitleChangesAndContentIsUnchanged_DoesNotCreateANewVersion()
        {
            var page = new WikiPage { Id = 1, Title = "Old Title", SpaceId = 10, IsActive = true, CurrentContentMarkdown = "same content" };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 1, ContentMarkdown = "same content" }
            }.BuildMock());

            var dto = new UpdateWikiPageDto { Title = "Renamed Title", ContentMarkdown = "same content" };
            var result = await CreateSut().UpdatePageAsync(1, dto, CallerWithId(1));

            Assert.True(result);
            Assert.Equal("Renamed Title", page.Title);
            _versionRepo.DidNotReceive().Add(Arg.Any<WikiPageVersion>());
        }

        [Fact]
        public async Task UpdatePageAsync_WhenNoVersionExistsYet_CreatesVersionNumberOne()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>().BuildMock());

            WikiPageVersion? addedVersion = null;
            _versionRepo.When(r => r.Add(Arg.Any<WikiPageVersion>())).Do(ci => addedVersion = ci.Arg<WikiPageVersion>());

            var dto = new UpdateWikiPageDto { Title = "Page", ContentMarkdown = "first real content" };
            await CreateSut().UpdatePageAsync(1, dto, CallerWithId(1));

            Assert.NotNull(addedVersion);
            Assert.Equal(1, addedVersion!.VersionNumber);
        }

        // ---------- DeletePageAsync ----------

        [Fact]
        public async Task DeletePageAsync_WhenPageDoesNotExist_ReturnsFalse()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().DeletePageAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeletePageAsync_WhenCallerLacksManageAccess_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().DeletePageAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task DeletePageAsync_WhenAuthorized_SoftDeletesRatherThanRemoving()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var result = await CreateSut().DeletePageAsync(1, CallerWithId(1));

            Assert.True(result);
            Assert.False(page.IsActive);
            _pageRepo.DidNotReceive().Remove(Arg.Any<WikiPage>());
        }

        // ---------- MovePageAsync ----------

        [Fact]
        public async Task MovePageAsync_WhenPageDoesNotExist_ReturnsFalse()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().MovePageAsync(999, new MoveWikiPageDto { NewParentPageId = 1 }, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task MovePageAsync_WhenNewParentIsSelf_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = 1 }, CallerWithId(1)));
        }

        [Fact]
        public async Task MovePageAsync_WhenNewParentIsInADifferentSpace_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            var otherSpaceParent = new WikiPage { Id = 2, Title = "Other", SpaceId = 20, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page, otherSpaceParent }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = 2 }, CallerWithId(1)));
        }

        [Fact]
        public async Task MovePageAsync_WhenNewParentIsADescendantOfThePage_ThrowsCycleError()
        {
            // page(1) -> child(2) -> grandchild(3); attempting to move page(1) under grandchild(3).
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, ParentPageId = null, IsActive = true };
            var child = new WikiPage { Id = 2, Title = "Child", SpaceId = 10, ParentPageId = 1, IsActive = true };
            var grandchild = new WikiPage { Id = 3, Title = "Grandchild", SpaceId = 10, ParentPageId = 2, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page, child, grandchild }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = 3 }, CallerWithId(1)));
        }

        [Fact]
        public async Task MovePageAsync_WhenTargetParentDoesNotExist_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = 999 }, CallerWithId(1)));
        }

        [Fact]
        public async Task MovePageAsync_WhenValid_ReparentsAndAppendsToEndOfNewSiblingGroup()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, ParentPageId = null, SortOrder = 0, IsActive = true };
            var newParent = new WikiPage { Id = 2, Title = "New Parent", SpaceId = 10, IsActive = true };
            var existingSibling = new WikiPage { Id = 3, Title = "Sibling", SpaceId = 10, ParentPageId = 2, SortOrder = 0, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page, newParent, existingSibling }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var result = await CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = 2 }, CallerWithId(1));

            Assert.True(result);
            Assert.Equal(2, page.ParentPageId);
            Assert.Equal(1, page.SortOrder);
        }

        [Fact]
        public async Task MovePageAsync_WhenNewParentPageIdIsNull_MovesToRootOfSpace()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, ParentPageId = 5, SortOrder = 2, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var result = await CreateSut().MovePageAsync(1, new MoveWikiPageDto { NewParentPageId = null }, CallerWithId(1));

            Assert.True(result);
            Assert.Null(page.ParentPageId);
        }

        // ---------- ReorderPagesAsync ----------

        [Fact]
        public async Task ReorderPagesAsync_WhenSubmittedIdsDoNotMatchTheSiblingGroup_Throws()
        {
            SetAuthorizationResult(succeeds: true);
            _pageRepo.Query().Returns(new List<WikiPage>
            {
                new() { Id = 1, SpaceId = 10, ParentPageId = null, IsActive = true },
                new() { Id = 2, SpaceId = 10, ParentPageId = null, IsActive = true },
            }.BuildMock());

            var dto = new ReorderWikiPagesDto { OrderedPageIds = new List<int> { 1, 999 } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ReorderPagesAsync(10, null, dto, CallerWithId(1)));
        }

        [Fact]
        public async Task ReorderPagesAsync_WhenSubmittedIdsMatchTheSiblingGroup_AssignsSequentialSortOrders()
        {
            SetAuthorizationResult(succeeds: true);
            var pageA = new WikiPage { Id = 1, SpaceId = 10, ParentPageId = null, SortOrder = 0, IsActive = true };
            var pageB = new WikiPage { Id = 2, SpaceId = 10, ParentPageId = null, SortOrder = 1, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { pageA, pageB }.BuildMock());

            var dto = new ReorderWikiPagesDto { OrderedPageIds = new List<int> { 2, 1 } };
            var result = await CreateSut().ReorderPagesAsync(10, null, dto, CallerWithId(1));

            Assert.True(result);
            Assert.Equal(1, pageA.SortOrder);
            Assert.Equal(0, pageB.SortOrder);
        }

        // ---------- SetLabelsAsync ----------

        [Fact]
        public async Task SetLabelsAsync_WhenPageDoesNotExist_ReturnsFalse()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().SetLabelsAsync(999, new SetWikiPageLabelsDto { Labels = new List<string> { "x" } }, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task SetLabelsAsync_RemovesExistingTagsAndAddsNewOnesForEachDistinctTrimmedLowercasedLabel()
        {
            var existingTag = new WikiPageTag { WikiPageId = 1, TagId = 1, Tag = new Tag { Id = 1, Name = "old" } };
            var page = new WikiPage
            {
                Id = 1,
                Title = "Page",
                SpaceId = 10,
                IsActive = true,
                WikiPageTags = new List<WikiPageTag> { existingTag }
            };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            // "Guides" already exists as a Tag; "new-label" does not.
            _tagRepo.Query().Returns(new List<Tag> { new() { Id = 5, Name = "guides" } }.BuildMock());
            Tag? createdTag = null;
            _tagRepo.When(r => r.Add(Arg.Any<Tag>())).Do(ci =>
            {
                createdTag = ci.Arg<Tag>();
                createdTag.Id = 6;
            });

            var dto = new SetWikiPageLabelsDto { Labels = new List<string> { " Guides ", "New-Label", "guides" } };
            var result = await CreateSut().SetLabelsAsync(1, dto, CallerWithId(1));

            Assert.True(result);
            _pageTagRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<WikiPageTag>>(tags => tags.Contains(existingTag)));
            Assert.NotNull(createdTag);
            Assert.Equal("new-label", createdTag!.Name);
            _pageTagRepo.Received(1).Add(Arg.Is<WikiPageTag>(t => t.TagId == 5));
            _pageTagRepo.Received(1).Add(Arg.Is<WikiPageTag>(t => t.TagId == 6));
        }

        [Fact]
        public async Task SetLabelsAsync_WhenLabelsAreBlankOrDuplicateAfterNormalization_SkipsThem()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true, WikiPageTags = new List<WikiPageTag>() };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _tagRepo.Query().Returns(new List<Tag>().BuildMock());

            var dto = new SetWikiPageLabelsDto { Labels = new List<string> { "  ", "Docs", "docs", "DOCS" } };
            await CreateSut().SetLabelsAsync(1, dto, CallerWithId(1));

            // Only one distinct normalized label ("docs") should ever reach tag lookup/creation.
            _tagRepo.Received(1).Add(Arg.Any<Tag>());
        }

        // ---------- GetVersionsAsync / GetVersionAsync ----------

        [Fact]
        public async Task GetVersionsAsync_WhenPageDoesNotExist_ReturnsNull()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().GetVersionsAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetVersionsAsync_WhenCallerLacksReadAccess_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10 };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().GetVersionsAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task GetVersionsAsync_ReturnsVersionsNewestFirst()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10 };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 1, Editor = new User { Name = "A" } },
                new() { WikiPageId = 1, VersionNumber = 3, Editor = new User { Name = "C" } },
                new() { WikiPageId = 1, VersionNumber = 2, Editor = new User { Name = "B" } },
            }.BuildMock());

            var result = await CreateSut().GetVersionsAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal(new[] { 3, 2, 1 }, result!.Select(v => v.VersionNumber));
        }

        [Fact]
        public async Task GetVersionAsync_WhenVersionNumberDoesNotExist_ReturnsNull()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10 };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _versionRepo.Query().Returns(new List<WikiPageVersion>
            {
                new() { WikiPageId = 1, VersionNumber = 1 }
            }.BuildMock());

            var result = await CreateSut().GetVersionAsync(1, 5, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetVersionAsync_WhenPageDoesNotExist_ReturnsNullWithoutAuthorizing()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().GetVersionAsync(999, 1, CallerWithId(1));

            Assert.Null(result);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        // ---------- Comments / mention notifications ----------

        [Fact]
        public async Task AddCommentAsync_WhenPageDoesNotExist_Throws()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().AddCommentAsync(999, new CreateWikiCommentDto { Body = "hi" }, CallerWithId(1)));
        }

        [Fact]
        public async Task AddCommentAsync_WhenCallerLacksWriteAccess_Throws()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().AddCommentAsync(1, new CreateWikiCommentDto { Body = "hi" }, CallerWithId(1)));
        }

        [Fact]
        public async Task AddCommentAsync_WhenBodyMentionsAUserWithSpaceAccess_NotifiesThatUser()
        {
            var page = new WikiPage { Id = 1, Title = "Runbook", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
                new() { Id = 2, Name = "Mentioned Person", IsActive = true, Email = "mentioned@khoi.africa" },
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 2, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);
            _notificationService.IsEmailEnabledAsync(2, NotificationTypes.Mention).Returns(false);

            var dto = new CreateWikiCommentDto { Body = "Hey @Mentioned Person, please review this." };
            await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            await _notificationService.Received(1).CreateNotificationAsync(
                2, "mention", Arg.Is<string>(m => m.Contains("Author Name") && m.Contains("Runbook")), wikiPageId: 1);
        }

        [Fact]
        public async Task AddCommentAsync_NeverNotifiesTheCommentsOwnAuthorEvenIfTheirNameAppearsInTheBody()
        {
            var page = new WikiPage { Id = 1, Title = "Runbook", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());

            var dto = new CreateWikiCommentDto { Body = "Note to self, @Author Name, remember this." };
            await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task AddCommentAsync_WhenMentionedUserHasNoSpaceAccess_DoesNotNotifyOrEmailThem()
        {
            var page = new WikiPage { Id = 1, Title = "Private Page", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
                new() { Id = 2, Name = "No Access Person", IsActive = true, Email = "noaccess@khoi.africa" },
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 2, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns((PermissionLevel?)null);

            var dto = new CreateWikiCommentDto { Body = "@No Access Person take a look" };
            await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
            await _emailService.DidNotReceive().SendMentionEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task AddCommentAsync_WhenMentionedUserHasEmailEnabled_SendsMentionEmail()
        {
            var page = new WikiPage { Id = 1, Title = "Runbook", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
                new() { Id = 2, Name = "Mentioned Person", IsActive = true, Email = "mentioned@khoi.africa" },
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 2, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);
            _notificationService.IsEmailEnabledAsync(2, NotificationTypes.Mention).Returns(true);
            _configuration["App:FrontendBaseUrl"].Returns("http://localhost:3000");

            var dto = new CreateWikiCommentDto { Body = "@Mentioned Person please look" };
            await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            await _emailService.Received(1).SendMentionEmailAsync(
                "mentioned@khoi.africa", "Author Name", "wiki page", "Runbook", dto.Body, Arg.Is<string>(u => u.Contains("pageId=1")));
        }

        [Fact]
        public async Task AddCommentAsync_WhenMentionEmailSendThrows_CommentStillSucceeds()
        {
            var page = new WikiPage { Id = 1, Title = "Runbook", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
                new() { Id = 2, Name = "Mentioned Person", IsActive = true, Email = "mentioned@khoi.africa" },
            }.BuildMock());
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _userGroupRepo.Query().Returns(new List<UserGroup>().BuildMock());
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 2, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);
            _notificationService.IsEmailEnabledAsync(2, NotificationTypes.Mention).Returns(true);
            _emailService.SendMentionEmailAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns<Task>(_ => throw new InvalidOperationException("SMTP down"));

            var dto = new CreateWikiCommentDto { Body = "@Mentioned Person please look" };
            var result = await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal("@Mentioned Person please look", result.Body);
        }

        [Fact]
        public async Task AddCommentAsync_WithNoMentions_NeverCallsNotificationService()
        {
            var page = new WikiPage { Id = 1, Title = "Runbook", SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Author Name" });
            SetNoUsersOrRoles();
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Author Name", IsActive = true, Email = "author@khoi.africa" },
            }.BuildMock());

            var dto = new CreateWikiCommentDto { Body = "Just a plain comment, no mentions here." };
            await CreateSut().AddCommentAsync(1, dto, CallerWithId(1));

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        // ---------- GetCommentsAsync ----------

        [Fact]
        public async Task GetCommentsAsync_WhenPageDoesNotExist_ReturnsNull()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().GetCommentsAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetCommentsAsync_ForADeletedComment_RedactsAuthorAndBody()
        {
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10 };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _commentRepo.Query().Returns(new List<WikiPageComment>
            {
                new()
                {
                    Id = 1, WikiPageId = 1, IsDeleted = true, Body = "secret stuff",
                    Author = new User { Name = "Someone" }, AuthoredBy = 1
                }
            }.BuildMock());

            var result = await CreateSut().GetCommentsAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal("[deleted]", result![0].AuthorName);
            Assert.Equal("[deleted]", result[0].Body);
        }

        // ---------- DeleteCommentAsync ----------

        [Fact]
        public async Task DeleteCommentAsync_WhenCommentDoesNotExist_ReturnsFalse()
        {
            _commentRepo.Query().Returns(new List<WikiPageComment>().BuildMock());

            var result = await CreateSut().DeleteCommentAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenAlreadyDeleted_ReturnsFalse()
        {
            var comment = new WikiPageComment { Id = 1, IsDeleted = true, WikiPage = new WikiPage { Id = 1, SpaceId = 10 } };
            _commentRepo.Query().Returns(new List<WikiPageComment> { comment }.BuildMock());

            var result = await CreateSut().DeleteCommentAsync(1, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerIsTheAuthor_SucceedsWithoutCheckingSpacePermission()
        {
            var comment = new WikiPageComment
            {
                Id = 1,
                AuthoredBy = 42,
                IsDeleted = false,
                WikiPage = new WikiPage { Id = 1, SpaceId = 10 }
            };
            _commentRepo.Query().Returns(new List<WikiPageComment> { comment }.BuildMock());

            var result = await CreateSut().DeleteCommentAsync(1, CallerWithId(42));

            Assert.True(result);
            Assert.True(comment.IsDeleted);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerIsNotTheAuthorAndLacksManageAccess_Throws()
        {
            var comment = new WikiPageComment
            {
                Id = 1,
                AuthoredBy = 42,
                IsDeleted = false,
                WikiPage = new WikiPage { Id = 1, SpaceId = 10 }
            };
            _commentRepo.Query().Returns(new List<WikiPageComment> { comment }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateSut().DeleteCommentAsync(1, CallerWithId(99)));
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerIsNotTheAuthorButHasManageAccess_Succeeds()
        {
            var comment = new WikiPageComment
            {
                Id = 1,
                AuthoredBy = 42,
                IsDeleted = false,
                WikiPage = new WikiPage { Id = 1, SpaceId = 10 }
            };
            _commentRepo.Query().Returns(new List<WikiPageComment> { comment }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var result = await CreateSut().DeleteCommentAsync(1, CallerWithId(99));

            Assert.True(result);
            Assert.True(comment.IsDeleted);
        }

        // ---------- SearchPagesAsync ----------

        [Fact]
        public async Task SearchPagesAsync_WhenQueryIsBlank_ReturnsEmptyWithoutCallingSearchRepo()
        {
            var result = await CreateSut().SearchPagesAsync("   ", CallerWithId(1));

            Assert.Empty(result);
            await _wikiSearchRepo.DidNotReceive().FindCandidatesAsync(Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task SearchPagesAsync_FiltersOutCandidatesTheCallerCannotRead()
        {
            var readablePage = new WikiPage { Id = 1, Title = "Readable", SpaceId = 10, CurrentContentMarkdown = "some content" };
            var hiddenPage = new WikiPage { Id = 2, Title = "Hidden", SpaceId = 20, CurrentContentMarkdown = "other content" };
            _wikiSearchRepo.FindCandidatesAsync("content", 50).Returns(new List<WikiPage> { readablePage, hiddenPage });
            SetNoUsersOrRoles();
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 1, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);
            _spacePermissionResolver.ResolveEffectiveLevelAsync(20, 1, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns((PermissionLevel?)null);

            var result = await CreateSut().SearchPagesAsync("content", CallerWithId(1));

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task SearchPagesAsync_BuildsASnippetCenteredOnTheFirstMatch()
        {
            var content = new string('x', 100) + "NEEDLE" + new string('y', 100);
            var page = new WikiPage { Id = 1, Title = "Page", SpaceId = 10, CurrentContentMarkdown = content };
            _wikiSearchRepo.FindCandidatesAsync("NEEDLE", 50).Returns(new List<WikiPage> { page });
            SetNoUsersOrRoles();
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 1, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);

            var result = await CreateSut().SearchPagesAsync("NEEDLE", CallerWithId(1));

            Assert.Single(result);
            Assert.Contains("NEEDLE", result[0].Snippet);
            Assert.StartsWith("...", result[0].Snippet);
            Assert.EndsWith("...", result[0].Snippet);
        }

        [Fact]
        public async Task SearchPagesAsync_StopsAtTwentyResultsEvenWithMoreReadableCandidates()
        {
            var pages = Enumerable.Range(1, 25)
                .Select(i => new WikiPage { Id = i, Title = $"Page {i}", SpaceId = 10, CurrentContentMarkdown = "match" })
                .ToList();
            _wikiSearchRepo.FindCandidatesAsync("match", 50).Returns(pages);
            SetNoUsersOrRoles();
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 1, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Read);

            var result = await CreateSut().SearchPagesAsync("match", CallerWithId(1));

            Assert.Equal(20, result.Count);
        }

        // ---------- GetMyLevelForPageAsync ----------

        [Fact]
        public async Task GetMyLevelForPageAsync_WhenPageDoesNotExistOrIsInactive_ReturnsNull()
        {
            _pageRepo.Query().Returns(new List<WikiPage>().BuildMock());

            var result = await CreateSut().GetMyLevelForPageAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetMyLevelForPageAsync_ReturnsTheResolversEffectiveLevel()
        {
            var page = new WikiPage { Id = 1, SpaceId = 10, IsActive = true };
            _pageRepo.Query().Returns(new List<WikiPage> { page }.BuildMock());
            SetNoUsersOrRoles();
            _spacePermissionResolver.ResolveEffectiveLevelAsync(10, 1, Arg.Any<IEnumerable<int>>(), Arg.Any<IEnumerable<int>>())
                .Returns(PermissionLevel.Write);

            var result = await CreateSut().GetMyLevelForPageAsync(1, CallerWithId(1));

            Assert.Equal(PermissionLevel.Write, result);
        }
    }
}
