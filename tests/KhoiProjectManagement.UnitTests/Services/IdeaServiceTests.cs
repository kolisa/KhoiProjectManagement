using System.Security.Claims;
using System.Text;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // IdeaService writes attachment files to disk (UploadAttachmentAsync/DeleteAttachmentAsync), unlike
    // most other services in this suite - FileUpload:IdeaPath is always pointed at a per-test-run temp
    // directory (see CreateSut) so nothing here ever touches the real wwwroot/idea-files folder, and
    // Dispose cleans it up.
    public class IdeaServiceTests : IDisposable
    {
        private readonly IRepository<Idea> _ideaRepo = Substitute.For<IRepository<Idea>>();
        private readonly IRepository<Project> _projectRepo = Substitute.For<IRepository<Project>>();
        private readonly IRepository<IdeaComment> _commentRepo = Substitute.For<IRepository<IdeaComment>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<IdeaAttachment> _attachmentRepo = Substitute.For<IRepository<IdeaAttachment>>();
        private readonly IRepository<IdeaAttachmentAnnotation> _annotationRepo = Substitute.For<IRepository<IdeaAttachmentAnnotation>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly IActivityLogService _activityLogService = Substitute.For<IActivityLogService>();
        private readonly string _uploadPath = Path.Combine(Path.GetTempPath(), "khoipro-idea-tests-" + Guid.NewGuid());

        public IdeaServiceTests()
        {
            // Baseline so AddCommentAsync's mention lookup (_userRepo.Query()) never NREs in tests that
            // don't care about mentions - tests that do exercise mentions override this.
            _userRepo.Query().Returns(new List<User>().BuildMock());
        }

        public void Dispose()
        {
            if (Directory.Exists(_uploadPath))
                Directory.Delete(_uploadPath, recursive: true);
        }

        private IdeaService CreateSut(Dictionary<string, string?>? configOverrides = null)
        {
            var overrides = configOverrides ?? new Dictionary<string, string?>();
            if (!overrides.ContainsKey("FileUpload:IdeaPath"))
                overrides["FileUpload:IdeaPath"] = _uploadPath;

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();

            return new IdeaService(
                _ideaRepo, _projectRepo, _commentRepo, _userRepo, _attachmentRepo, _annotationRepo,
                _unitOfWork, _notificationService, _emailService, configuration, _activityLogService);
        }

        private static ClaimsPrincipal CallerWithId(int userId, params string[] permissions)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        // --- GetIdeasAsync ---

        [Fact]
        public async Task GetIdeasAsync_WhenStatusIsNull_ReturnsAllIdeasOrderedByCreatedAtDescending()
        {
            _ideaRepo.Query().Returns(new List<Idea>
            {
                new() { Id = 1, Title = "Older", Status = "Submitted", CreatedAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Title = "Newer", Status = "Approved", CreatedAt = new DateTime(2026, 1, 5) },
            }.BuildMock());

            var result = await CreateSut().GetIdeasAsync(null);

            Assert.Equal(new[] { "Newer", "Older" }, result.Select(i => i.Title));
        }

        [Fact]
        public async Task GetIdeasAsync_WhenStatusProvided_FiltersByStatus()
        {
            _ideaRepo.Query().Returns(new List<Idea>
            {
                new() { Id = 1, Title = "Pending", Status = "Submitted", CreatedAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Title = "Done", Status = "Approved", CreatedAt = new DateTime(2026, 1, 2) },
            }.BuildMock());

            var result = await CreateSut().GetIdeasAsync("Approved");

            Assert.Single(result);
            Assert.Equal("Done", result[0].Title);
        }

        // --- GetIdeaByIdAsync ---

        [Fact]
        public async Task GetIdeaByIdAsync_WhenNotFound_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().GetIdeaByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetIdeaByIdAsync_WhenFound_MapsSubmitterNameAndExcludesDeletedCommentsFromCount()
        {
            var idea = new Idea
            {
                Id = 1,
                Title = "Great Idea",
                Submitter = new User { Id = 1, Name = "Submitter Sam" },
                Comments = new List<IdeaComment>
                {
                    new() { Id = 1, Body = "Nice", IsDeleted = false },
                    new() { Id = 2, Body = "Removed", IsDeleted = true },
                }
            };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            var result = await CreateSut().GetIdeaByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Submitter Sam", result!.SubmitterName);
            Assert.Equal(1, result.CommentCount);
        }

        // --- CreateIdeaAsync ---

        [Fact]
        public async Task CreateIdeaAsync_AddsIdeaSubmittedByCaller()
        {
            Idea? added = null;
            _ideaRepo.When(r => r.Add(Arg.Any<Idea>())).Do(ci =>
            {
                added = ci.Arg<Idea>();
                added.Id = 42;
            });
            _ideaRepo.Query().Returns(ci => new List<Idea> { added! }.BuildMock());

            var dto = new CreateIdeaDto { Title = "Dark mode", Description = "Add a dark theme" };

            var result = await CreateSut().CreateIdeaAsync(dto, CallerWithId(7));

            Assert.Equal("Dark mode", result.Title);
            Assert.Equal(7, result.SubmittedBy);
            _ideaRepo.Received(1).Add(Arg.Is<Idea>(i => i.Title == "Dark mode" && i.SubmittedBy == 7));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- UpdateIdeaAsync ---

        [Fact]
        public async Task UpdateIdeaAsync_WhenIdeaDoesNotExist_ReturnsFalse()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().UpdateIdeaAsync(999, new UpdateIdeaDto(), CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateIdeaAsync_WhenCallerIsNotSubmitter_Throws()
        {
            var idea = new Idea { Id = 1, SubmittedBy = 2, Status = "Submitted" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().UpdateIdeaAsync(1, new UpdateIdeaDto(), CallerWithId(1)));
        }

        [Fact]
        public async Task UpdateIdeaAsync_WhenStatusIsNotSubmitted_Throws()
        {
            var idea = new Idea { Id = 1, SubmittedBy = 1, Status = "UnderReview" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().UpdateIdeaAsync(1, new UpdateIdeaDto(), CallerWithId(1)));
        }

        [Fact]
        public async Task UpdateIdeaAsync_WhenSubmitterAndStatusIsSubmitted_UpdatesTitleAndDescription()
        {
            var idea = new Idea { Id = 1, SubmittedBy = 1, Status = "Submitted", Title = "Old", Description = "Old desc" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            var result = await CreateSut().UpdateIdeaAsync(1, new UpdateIdeaDto { Title = "New", Description = "New desc" }, CallerWithId(1));

            Assert.True(result);
            Assert.Equal("New", idea.Title);
            Assert.Equal("New desc", idea.Description);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- UpdateStatusAsync ---

        [Theory]
        [InlineData("NotARealStatus")]
        [InlineData("ConvertedToProject")]
        public async Task UpdateStatusAsync_WhenStatusIsInvalidOrConvertedToProject_Throws(string status)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().UpdateStatusAsync(1, status, 1));
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenIdeaDoesNotExist_ReturnsFalse()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().UpdateStatusAsync(999, "Approved", 1);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenValid_UpdatesStatusAndLogsActivity()
        {
            var idea = new Idea { Id = 1, Title = "Idea", Status = "Submitted" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            var result = await CreateSut().UpdateStatusAsync(1, "Approved", actingUserId: 9);

            Assert.True(result);
            Assert.Equal("Approved", idea.Status);
            await _unitOfWork.Received(1).SaveChangesAsync();
            await _activityLogService.Received(1).LogAsync("Idea", 1, "Idea", 9, "StatusChanged", "Approved");
        }

        // --- ConvertToProjectAsync ---

        [Fact]
        public async Task ConvertToProjectAsync_WhenIdeaDoesNotExist_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().ConvertToProjectAsync(999, 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task ConvertToProjectAsync_WhenAlreadyConverted_Throws()
        {
            var idea = new Idea { Id = 1, Status = "ConvertedToProject" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ConvertToProjectAsync(1, 9));
        }

        [Fact]
        public async Task ConvertToProjectAsync_CreatesProjectAttributedToCallerAndLinksIdea()
        {
            var idea = new Idea { Id = 1, Title = "Great Idea", Description = "Details", Status = "Submitted" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            Project? addedProject = null;
            _projectRepo.When(r => r.Add(Arg.Any<Project>())).Do(ci =>
            {
                addedProject = ci.Arg<Project>();
                addedProject.Id = 55;
            });

            var result = await CreateSut().ConvertToProjectAsync(1, callerId: 9);

            Assert.NotNull(result);
            Assert.Equal("ConvertedToProject", idea.Status);
            Assert.Equal(55, idea.ConvertedProjectId);
            _projectRepo.Received(1).Add(Arg.Is<Project>(p => p.Name == "Great Idea" && p.Description == "Details" && p.CreatedBy == 9));
            await _unitOfWork.Received(2).SaveChangesAsync();
        }

        // --- GetCommentsAsync ---

        [Fact]
        public async Task GetCommentsAsync_WhenIdeaDoesNotExist_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().GetCommentsAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetCommentsAsync_ExcludesDeletedCommentsAndOrdersByCreatedAtAscending()
        {
            var idea = new Idea { Id = 1, Title = "Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());
            _commentRepo.Query().Returns(new List<IdeaComment>
            {
                new() { Id = 1, IdeaId = 1, Body = "Second", CreatedAt = new DateTime(2026, 1, 2), Author = new User { Name = "A" } },
                new() { Id = 2, IdeaId = 1, Body = "First", CreatedAt = new DateTime(2026, 1, 1), Author = new User { Name = "B" } },
                new() { Id = 3, IdeaId = 1, Body = "Deleted", IsDeleted = true, CreatedAt = new DateTime(2026, 1, 3), Author = new User { Name = "C" } },
                new() { Id = 4, IdeaId = 2, Body = "OtherIdea", CreatedAt = new DateTime(2026, 1, 1), Author = new User { Name = "D" } },
            }.BuildMock());

            var result = await CreateSut().GetCommentsAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new[] { "First", "Second" }, result!.Select(c => c.Body));
        }

        // --- AddCommentAsync ---

        [Fact]
        public async Task AddCommentAsync_WhenIdeaDoesNotExist_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().AddCommentAsync(999, new CreateIdeaCommentDto { Body = "Hi" }, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task AddCommentAsync_AddsCommentAuthoredByCaller()
        {
            var idea = new Idea { Id = 1, Title = "Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            IdeaComment? added = null;
            _commentRepo.When(r => r.Add(Arg.Any<IdeaComment>())).Do(ci =>
            {
                added = ci.Arg<IdeaComment>();
                added.Id = 5;
                added.Author = new User { Id = 3, Name = "Carl Caller" };
            });
            _commentRepo.Query().Returns(ci => new List<IdeaComment> { added! }.BuildMock());

            var result = await CreateSut().AddCommentAsync(1, new CreateIdeaCommentDto { Body = "Nice idea" }, CallerWithId(3));

            Assert.NotNull(result);
            Assert.Equal("Nice idea", result!.Body);
            Assert.Equal("Carl Caller", result.AuthorName);
            _commentRepo.Received(1).Add(Arg.Is<IdeaComment>(c => c.IdeaId == 1 && c.AuthoredBy == 3 && c.Body == "Nice idea"));
        }

        [Fact]
        public async Task AddCommentAsync_WhenBodyMentionsAnActiveUser_CreatesAMentionNotification()
        {
            var idea = new Idea { Id = 1, Title = "Great Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            IdeaComment? added = null;
            _commentRepo.When(r => r.Add(Arg.Any<IdeaComment>())).Do(ci =>
            {
                added = ci.Arg<IdeaComment>();
                added.Id = 100;
                added.Author = new User { Id = 7, Name = "Alice Author" };
            });
            _commentRepo.Query().Returns(ci => new List<IdeaComment> { added! }.BuildMock());

            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 7, Name = "Alice Author", IsActive = true, Email = "alice@x.com" },
                new() { Id = 8, Name = "Bob Bystander", IsActive = true, Email = "bob@x.com" },
            }.BuildMock());
            _notificationService.IsEmailEnabledAsync(8, NotificationTypes.Mention).Returns(false);

            var dto = new CreateIdeaCommentDto { Body = "Hey @Bob Bystander, check this idea" };

            var result = await CreateSut().AddCommentAsync(1, dto, CallerWithId(7));

            Assert.NotNull(result);
            await _notificationService.Received(1).CreateNotificationAsync(
                8, "mention", Arg.Is<string>(m => m.Contains("Alice Author") && m.Contains("Great Idea")), null, null, null, 1, null);
            await _emailService.DidNotReceiveWithAnyArgs().SendMentionEmailAsync(default!, default!, default!, default!, default!);
        }

        [Fact]
        public async Task AddCommentAsync_WhenMentionedUserHasEmailEnabled_SendsMentionEmail()
        {
            var idea = new Idea { Id = 1, Title = "Great Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            IdeaComment? added = null;
            _commentRepo.When(r => r.Add(Arg.Any<IdeaComment>())).Do(ci =>
            {
                added = ci.Arg<IdeaComment>();
                added.Id = 100;
                added.Author = new User { Id = 7, Name = "Alice Author" };
            });
            _commentRepo.Query().Returns(ci => new List<IdeaComment> { added! }.BuildMock());

            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 7, Name = "Alice Author", IsActive = true, Email = "alice@x.com" },
                new() { Id = 8, Name = "Bob Bystander", IsActive = true, Email = "bob@x.com" },
            }.BuildMock());
            _notificationService.IsEmailEnabledAsync(8, NotificationTypes.Mention).Returns(true);

            var dto = new CreateIdeaCommentDto { Body = "Hey @Bob Bystander, check this idea" };

            await CreateSut().AddCommentAsync(1, dto, CallerWithId(7));

            await _emailService.Received(1).SendMentionEmailAsync(
                "bob@x.com", "Alice Author", "idea", "Great Idea", dto.Body);
        }

        [Fact]
        public async Task AddCommentAsync_WhenMentionEmailSendThrows_CommentStillSucceeds()
        {
            // Regression coverage for the deliberate swallow in NotifyMentionedUsersAsync - the comment
            // is already persisted, so a failed SMTP send must never fail the whole request.
            var idea = new Idea { Id = 1, Title = "Great Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            IdeaComment? added = null;
            _commentRepo.When(r => r.Add(Arg.Any<IdeaComment>())).Do(ci =>
            {
                added = ci.Arg<IdeaComment>();
                added.Id = 100;
                added.Author = new User { Id = 7, Name = "Alice Author" };
            });
            _commentRepo.Query().Returns(ci => new List<IdeaComment> { added! }.BuildMock());

            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 7, Name = "Alice Author", IsActive = true, Email = "alice@x.com" },
                new() { Id = 8, Name = "Bob Bystander", IsActive = true, Email = "bob@x.com" },
            }.BuildMock());
            _notificationService.IsEmailEnabledAsync(8, NotificationTypes.Mention).Returns(true);
            _emailService.SendMentionEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("SMTP unreachable")));

            var dto = new CreateIdeaCommentDto { Body = "Hey @Bob Bystander, check this idea" };

            var exception = await Record.ExceptionAsync(() => CreateSut().AddCommentAsync(1, dto, CallerWithId(7)));

            Assert.Null(exception);
        }

        // --- DeleteCommentAsync ---

        [Fact]
        public async Task DeleteCommentAsync_WhenCommentDoesNotExist_ReturnsFalse()
        {
            _commentRepo.Query().Returns(new List<IdeaComment>().BuildMock());

            var result = await CreateSut().DeleteCommentAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerIsNotAuthorAndLacksManagePermission_Throws()
        {
            var comment = new IdeaComment { Id = 1, AuthoredBy = 2 };
            _commentRepo.Query().Returns(new List<IdeaComment> { comment }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().DeleteCommentAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerIsAuthor_SoftDeletes()
        {
            var comment = new IdeaComment { Id = 1, AuthoredBy = 1, IsDeleted = false };
            _commentRepo.Query().Returns(new List<IdeaComment> { comment }.BuildMock());

            var result = await CreateSut().DeleteCommentAsync(1, CallerWithId(1));

            Assert.True(result);
            Assert.True(comment.IsDeleted);
            _commentRepo.DidNotReceive().Remove(Arg.Any<IdeaComment>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteCommentAsync_WhenCallerHasIdeasManagePermission_SoftDeletesEvenThoughNotAuthor()
        {
            var comment = new IdeaComment { Id = 1, AuthoredBy = 2, IsDeleted = false };
            _commentRepo.Query().Returns(new List<IdeaComment> { comment }.BuildMock());

            var result = await CreateSut().DeleteCommentAsync(1, CallerWithId(1, "ideas.manage"));

            Assert.True(result);
            Assert.True(comment.IsDeleted);
        }

        // --- GetAttachmentsAsync ---

        [Fact]
        public async Task GetAttachmentsAsync_WhenIdeaDoesNotExist_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());

            var result = await CreateSut().GetAttachmentsAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAttachmentsAsync_ReturnsAttachmentsOrderedByUploadedAtDescendingWithAnnotationCount()
        {
            var idea = new Idea { Id = 1, Title = "Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());
            _attachmentRepo.Query().Returns(new List<IdeaAttachment>
            {
                new()
                {
                    Id = 1, IdeaId = 1, OriginalFileName = "old.png", UploadedAt = new DateTime(2026, 1, 1),
                    Uploader = new User { Name = "U1" },
                    Annotations = new List<IdeaAttachmentAnnotation> { new(), new() }
                },
                new()
                {
                    Id = 2, IdeaId = 1, OriginalFileName = "new.png", UploadedAt = new DateTime(2026, 1, 5),
                    Uploader = new User { Name = "U2" },
                    Annotations = new List<IdeaAttachmentAnnotation>()
                },
            }.BuildMock());

            var result = await CreateSut().GetAttachmentsAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new[] { "new.png", "old.png" }, result!.Select(a => a.OriginalFileName));
            Assert.Equal(2, result.Single(a => a.OriginalFileName == "old.png").AnnotationCount);
        }

        // --- UploadAttachmentAsync ---

        [Fact]
        public async Task UploadAttachmentAsync_WhenIdeaDoesNotExist_ReturnsNull()
        {
            _ideaRepo.Query().Returns(new List<Idea>().BuildMock());
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("mockup.png");

            var result = await CreateSut().UploadAttachmentAsync(999, file, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task UploadAttachmentAsync_SanitizesPathTraversalOutOfTheClientSuppliedFileName()
        {
            // Regression test for the fix noted in the task brief: OriginalFileName/StoredFileName must
            // both be built from Path.GetFileName(), never the raw client-supplied FileName, or a crafted
            // "../"-laden name could write outside the upload directory.
            var idea = new Idea { Id = 1, Title = "Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("../../../../etc/passwd.txt");
            file.ContentType.Returns("text/plain");
            file.Length.Returns(0);

            IdeaAttachment? added = null;
            _attachmentRepo.When(r => r.Add(Arg.Any<IdeaAttachment>())).Do(ci =>
            {
                added = ci.Arg<IdeaAttachment>();
                added.Id = 10;
            });
            _attachmentRepo.Query().Returns(ci => new List<IdeaAttachment> { added! }.BuildMock());

            var result = await CreateSut().UploadAttachmentAsync(1, file, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal("passwd.txt", added!.OriginalFileName);
            Assert.DoesNotContain("..", added.StoredFileName);
            Assert.EndsWith("_passwd.txt", added.StoredFileName);
        }

        [Fact]
        public async Task UploadAttachmentAsync_WritesTheFileContentToTheConfiguredUploadPath()
        {
            var idea = new Idea { Id = 1, Title = "Idea" };
            _ideaRepo.Query().Returns(new List<Idea> { idea }.BuildMock());

            var contentBytes = Encoding.UTF8.GetBytes("mockup-bytes");
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("mockup.png");
            file.ContentType.Returns("image/png");
            file.Length.Returns(contentBytes.Length);
            file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(ci => ci.Arg<Stream>().WriteAsync(contentBytes, 0, contentBytes.Length));

            IdeaAttachment? added = null;
            _attachmentRepo.When(r => r.Add(Arg.Any<IdeaAttachment>())).Do(ci =>
            {
                added = ci.Arg<IdeaAttachment>();
                added.Id = 11;
            });
            _attachmentRepo.Query().Returns(ci => new List<IdeaAttachment> { added! }.BuildMock());

            var result = await CreateSut().UploadAttachmentAsync(1, file, CallerWithId(3));

            Assert.NotNull(result);
            Assert.Equal(3, added!.UploadedBy);
            var writtenPath = Path.Combine(_uploadPath, added.StoredFileName);
            Assert.True(File.Exists(writtenPath));
            Assert.Equal(contentBytes, await File.ReadAllBytesAsync(writtenPath));
        }

        // --- DownloadAttachmentAsync ---

        [Fact]
        public async Task DownloadAttachmentAsync_WhenAttachmentDoesNotExist_ReturnsNull()
        {
            _attachmentRepo.Query().Returns(new List<IdeaAttachment>().BuildMock());

            var result = await CreateSut().DownloadAttachmentAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadAttachmentAsync_WhenFileIsMissingFromDisk_ReturnsNullEvenThoughTheRowExists()
        {
            var attachment = new IdeaAttachment { Id = 1, StoredFileName = "missing_file.png", ContentType = "image/png", OriginalFileName = "file.png" };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            var result = await CreateSut().DownloadAttachmentAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadAttachmentAsync_WhenFileExists_ReturnsContentAndOriginalFileName()
        {
            Directory.CreateDirectory(_uploadPath);
            var storedFileName = "stored123_mockup.png";
            var filePath = Path.Combine(_uploadPath, storedFileName);
            await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });

            var attachment = new IdeaAttachment { Id = 1, StoredFileName = storedFileName, ContentType = "image/png", OriginalFileName = "mockup.png" };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            var result = await CreateSut().DownloadAttachmentAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new byte[] { 1, 2, 3 }, result!.Value.Content);
            Assert.Equal("image/png", result.Value.ContentType);
            Assert.Equal("mockup.png", result.Value.FileName);
        }

        // --- DeleteAttachmentAsync ---
        // Per the earlier audit finding on this method: only the uploader or someone holding
        // ideas.manage may delete an attachment.

        [Fact]
        public async Task DeleteAttachmentAsync_WhenAttachmentDoesNotExist_ReturnsFalse()
        {
            _attachmentRepo.Query().Returns(new List<IdeaAttachment>().BuildMock());

            var result = await CreateSut().DeleteAttachmentAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAttachmentAsync_WhenCallerIsNotUploaderAndLacksManagePermission_Throws()
        {
            var attachment = new IdeaAttachment { Id = 1, UploadedBy = 2, StoredFileName = "irrelevant.png" };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().DeleteAttachmentAsync(1, CallerWithId(1)));

            _attachmentRepo.DidNotReceive().Remove(Arg.Any<IdeaAttachment>());
        }

        [Fact]
        public async Task DeleteAttachmentAsync_WhenCallerIsUploader_RemovesTheRowAndDeletesTheFileFromDisk()
        {
            Directory.CreateDirectory(_uploadPath);
            var storedFileName = "abc123_mockup.png";
            var filePath = Path.Combine(_uploadPath, storedFileName);
            await File.WriteAllTextAsync(filePath, "fake content");

            var attachment = new IdeaAttachment { Id = 1, IdeaId = 1, StoredFileName = storedFileName, UploadedBy = 5 };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            var result = await CreateSut().DeleteAttachmentAsync(1, CallerWithId(5));

            Assert.True(result);
            Assert.False(File.Exists(filePath));
            _attachmentRepo.Received(1).Remove(attachment);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteAttachmentAsync_WhenCallerHasIdeasManagePermission_RemovesEvenThoughNotUploader()
        {
            var attachment = new IdeaAttachment { Id = 1, StoredFileName = "does-not-exist-on-disk.png", UploadedBy = 2 };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            var result = await CreateSut().DeleteAttachmentAsync(1, CallerWithId(1, "ideas.manage"));

            Assert.True(result);
            _attachmentRepo.Received(1).Remove(attachment);
        }

        // --- GetAnnotationsAsync ---

        [Fact]
        public async Task GetAnnotationsAsync_WhenAttachmentDoesNotExist_ReturnsNull()
        {
            _attachmentRepo.Query().Returns(new List<IdeaAttachment>().BuildMock());

            var result = await CreateSut().GetAnnotationsAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAnnotationsAsync_ReturnsAnnotationsForThatAttachmentOrderedByCreatedAt()
        {
            var attachment = new IdeaAttachment { Id = 1 };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());
            _annotationRepo.Query().Returns(new List<IdeaAttachmentAnnotation>
            {
                new() { Id = 1, IdeaAttachmentId = 1, Body = "Second", CreatedAt = new DateTime(2026, 1, 2), Author = new User { Name = "A" } },
                new() { Id = 2, IdeaAttachmentId = 1, Body = "First", CreatedAt = new DateTime(2026, 1, 1), Author = new User { Name = "B" } },
                new() { Id = 3, IdeaAttachmentId = 2, Body = "OtherAttachment", CreatedAt = new DateTime(2026, 1, 1), Author = new User { Name = "C" } },
            }.BuildMock());

            var result = await CreateSut().GetAnnotationsAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new[] { "First", "Second" }, result!.Select(a => a.Body));
        }

        // --- AddAnnotationAsync ---

        [Fact]
        public async Task AddAnnotationAsync_WhenAttachmentDoesNotExist_ReturnsNull()
        {
            _attachmentRepo.Query().Returns(new List<IdeaAttachment>().BuildMock());

            var result = await CreateSut().AddAnnotationAsync(999, new CreateIdeaAttachmentAnnotationDto { Body = "Note" }, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task AddAnnotationAsync_AddsAnnotationAuthoredByCaller()
        {
            var attachment = new IdeaAttachment { Id = 1 };
            _attachmentRepo.Query().Returns(new List<IdeaAttachment> { attachment }.BuildMock());

            IdeaAttachmentAnnotation? added = null;
            _annotationRepo.When(r => r.Add(Arg.Any<IdeaAttachmentAnnotation>())).Do(ci =>
            {
                added = ci.Arg<IdeaAttachmentAnnotation>();
                added.Id = 9;
                added.Author = new User { Id = 4, Name = "Dana" };
            });
            _annotationRepo.Query().Returns(ci => new List<IdeaAttachmentAnnotation> { added! }.BuildMock());

            var result = await CreateSut().AddAnnotationAsync(1, new CreateIdeaAttachmentAnnotationDto { Body = "Looks great" }, CallerWithId(4));

            Assert.NotNull(result);
            Assert.Equal("Looks great", result!.Body);
            Assert.Equal("Dana", result.AuthorName);
            _annotationRepo.Received(1).Add(Arg.Is<IdeaAttachmentAnnotation>(a => a.IdeaAttachmentId == 1 && a.AuthoredBy == 4));
        }

        // --- DeleteAnnotationAsync ---

        [Fact]
        public async Task DeleteAnnotationAsync_WhenAnnotationDoesNotExist_ReturnsFalse()
        {
            _annotationRepo.Query().Returns(new List<IdeaAttachmentAnnotation>().BuildMock());

            var result = await CreateSut().DeleteAnnotationAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAnnotationAsync_WhenCallerIsNotAuthorAndLacksManagePermission_Throws()
        {
            var annotation = new IdeaAttachmentAnnotation { Id = 1, AuthoredBy = 2 };
            _annotationRepo.Query().Returns(new List<IdeaAttachmentAnnotation> { annotation }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().DeleteAnnotationAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task DeleteAnnotationAsync_WhenCallerIsAuthor_Removes()
        {
            var annotation = new IdeaAttachmentAnnotation { Id = 1, AuthoredBy = 1 };
            _annotationRepo.Query().Returns(new List<IdeaAttachmentAnnotation> { annotation }.BuildMock());

            var result = await CreateSut().DeleteAnnotationAsync(1, CallerWithId(1));

            Assert.True(result);
            _annotationRepo.Received(1).Remove(annotation);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}
