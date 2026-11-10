using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class PmCollaborationServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<WorkItemComment> _comments = Substitute.For<IRepository<WorkItemComment>>();
        private readonly IRepository<ChecklistItem> _checklist = Substitute.For<IRepository<ChecklistItem>>();
        private readonly IRepository<WorkItemLink> _links = Substitute.For<IRepository<WorkItemLink>>();
        private readonly IRepository<Attachment> _attachments = Substitute.For<IRepository<Attachment>>();
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IEmailService _email = Substitute.For<IEmailService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private readonly IConfiguration _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileUpload:MaxFileSize"] = "1000",
            ["FileUpload:AllowedExtensions:0"] = ".png",
            ["FileUpload:AllowedExtensions:1"] = ".pdf",
            ["FileUpload:AllowedExtensions:2"] = ".odt",
        }).Build();

        public PmCollaborationServiceTests()
        {
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Title = "Login page", AssignedToId = 20 },
                     new ProjectTask { Id = 2, ProjectId = 1, Title = "API" },
                     new ProjectTask { Id = 3, ProjectId = 1, Title = "DB" });
            SetLinks();
            SetComments();
            _users.Query().Returns(_ => new List<User> { new() { Id = 10, Name = "Dev Dana" } }.BuildMock());
            _members.Query().Returns(_ => new List<ProjectUser>
            {
                new() { ProjectId = 1, UserId = 10, User = new User { Id = 10, Name = "Dev Dana", Email = "dana@x.com", IsActive = true } },
                new() { ProjectId = 1, UserId = 20, User = new User { Id = 20, Name = "Sam Smith", Email = "sam@x.com", IsActive = true } },
                new() { ProjectId = 1, UserId = 30, User = new User { Id = 30, Name = "Pat Lee", Email = "pat@x.com", IsActive = true } },
            }.BuildMock());
            _access.GetRoleAsync(default!, default).ReturnsForAnyArgs(ProjectRoles.Developer);
            _notifications.IsEmailEnabledAsync(default, default!).ReturnsForAnyArgs(true);
        }

        private PmCollaborationService CreateSut() => new(_tasks, _comments, _checklist, _links, _attachments, _members, _users,
            _access, _notifications, _email, _activity, _storage, _config, _uow, NullLogger<PmCollaborationService>.Instance);

        private void SetTasks(params ProjectTask[] t) => _tasks.Query().Returns(_ => t.ToList().BuildMock());
        private void SetLinks(params WorkItemLink[] l) => _links.Query().Returns(_ => l.ToList().BuildMock());
        private void SetComments(params WorkItemComment[] c) => _comments.Query().Returns(_ => c.ToList().BuildMock());

        // ---- comments

        [Fact]
        public async Task AddComment_AsViewer_IsForbidden_AndSavesNothing()
        {
            _access.RequireAsync(Dev, 1, PmAction.Collaborate).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().AddCommentAsync(Dev, 1, new SaveCommentDto { Body = "hi" }));
            _comments.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task AddComment_OnUnknownItem_IsNotFound()
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().AddCommentAsync(Dev, 99, new SaveCommentDto { Body = "hi" }));
        }

        [Fact]
        public async Task AddComment_NotifiesAssignee_AndMentionedMembers_ButNotAuthor()
        {
            await CreateSut().AddCommentAsync(Dev, 1, new SaveCommentDto { Body = "  Please review @Pat Lee and @Dev Dana  " });

            await _notifications.Received(1).CreateNotificationAsync(30, NotificationTypes.Mention, Arg.Any<string>(), 1, 1, null, null, null);
            await _notifications.Received(1).CreateNotificationAsync(20, "comment", Arg.Any<string>(), 1, 1, null, null, null);
            await _notifications.DidNotReceive().CreateNotificationAsync(10, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
            await _email.Received(1).SendMentionEmailAsync("pat@x.com", "Dev Dana", "work item", "Login page", Arg.Any<string>(), Arg.Any<string?>());
        }

        [Fact]
        public async Task AddComment_MentioningTheAssignee_SendsOnlyTheMentionNotification()
        {
            await CreateSut().AddCommentAsync(Dev, 1, new SaveCommentDto { Body = "@Sam Smith can you check?" });

            await _notifications.Received(1).CreateNotificationAsync(20, NotificationTypes.Mention, Arg.Any<string>(), 1, 1, null, null, null);
            await _notifications.DidNotReceive().CreateNotificationAsync(20, "comment", Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task AddComment_MentionOfNonMember_IsIgnored()
        {
            await CreateSut().AddCommentAsync(Dev, 2, new SaveCommentDto { Body = "@Outside Person hello" });
            await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default, default!, default!, default, default, default, default, default);
        }

        [Fact]
        public async Task AddComment_FailedMentionEmail_DoesNotFailTheComment()
        {
            _email.SendMentionEmailAsync(default!, default!, default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException(new Exception("smtp down")));
            var dto = await CreateSut().AddCommentAsync(Dev, 2, new SaveCommentDto { Body = "@Pat Lee ping" });
            Assert.Equal("@Pat Lee ping", dto.Body);
        }

        [Fact]
        public async Task EditComment_ByAnotherUser_IsForbidden()
        {
            SetComments(new WorkItemComment { Id = 1, TaskId = 1, AuthoredBy = 99, Body = "x" });
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().EditCommentAsync(Dev, 1, new SaveCommentDto { Body = "y" }));
        }

        [Fact]
        public async Task EditComment_OnlyNotifiesNewlyMentionedPeople()
        {
            SetComments(new WorkItemComment { Id = 1, TaskId = 1, AuthoredBy = 10, Body = "cc @Pat Lee" });
            await CreateSut().EditCommentAsync(Dev, 1, new SaveCommentDto { Body = "cc @Pat Lee and @Sam Smith" });

            await _notifications.Received(1).CreateNotificationAsync(20, NotificationTypes.Mention, Arg.Any<string>(), 1, 1, null, null, null);
            await _notifications.DidNotReceive().CreateNotificationAsync(30, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task DeleteComment_OfSomeoneElse_RequiresDeletePermission()
        {
            var comment = new WorkItemComment { Id = 1, TaskId = 1, AuthoredBy = 99, Body = "x" };
            SetComments(comment);
            _access.RequireAsync(Dev, 1, PmAction.DeleteItem).Returns(Task.FromException(new ForbiddenException()));

            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().DeleteCommentAsync(Dev, 1));
            Assert.False(comment.IsDeleted);
        }

        [Fact]
        public async Task DeleteComment_OwnComment_SoftDeletes()
        {
            var comment = new WorkItemComment { Id = 1, TaskId = 1, AuthoredBy = 10, Body = "x" };
            SetComments(comment);
            await CreateSut().DeleteCommentAsync(Dev, 1);
            Assert.True(comment.IsDeleted);
        }

        // ---- checklist

        [Fact]
        public async Task UpdateChecklistItem_ToDone_StampsCompletion()
        {
            var entry = new ChecklistItem { Id = 1, TaskId = 1, Text = "step" };
            _checklist.Query().Returns(_ => new List<ChecklistItem> { entry }.BuildMock());

            var dto = await CreateSut().UpdateChecklistItemAsync(Dev, 1, new SaveChecklistItemDto { Text = "step", IsDone = true });

            Assert.True(dto.IsDone);
            Assert.NotNull(entry.CompletedAt);
            Assert.Equal(10, entry.CompletedBy);
        }

        [Fact]
        public async Task UpdateChecklistItem_BackToOpen_ClearsCompletion()
        {
            var entry = new ChecklistItem { Id = 1, TaskId = 1, Text = "step", IsDone = true, CompletedAt = DateTime.UtcNow, CompletedBy = 10 };
            _checklist.Query().Returns(_ => new List<ChecklistItem> { entry }.BuildMock());

            await CreateSut().UpdateChecklistItemAsync(Dev, 1, new SaveChecklistItemDto { Text = "step", IsDone = false });

            Assert.Null(entry.CompletedAt);
            Assert.Null(entry.CompletedBy);
        }

        [Fact]
        public async Task AddChecklistItem_AppendsAtTheEnd()
        {
            _checklist.Query().Returns(_ => new List<ChecklistItem> { new() { TaskId = 1, Position = 4 } }.BuildMock());
            ChecklistItem? added = null;
            _checklist.When(r => r.Add(Arg.Any<ChecklistItem>())).Do(ci => added = ci.Arg<ChecklistItem>());

            await CreateSut().AddChecklistItemAsync(Dev, 1, new SaveChecklistItemDto { Text = " new " });

            Assert.Equal(5, added!.Position);
            Assert.Equal("new", added.Text);
        }

        // ---- links

        [Fact]
        public async Task AddLink_ToSelf_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 1, Type = "Blocks" }));

        [Fact]
        public async Task AddLink_ToItemOutsideProject_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 404, Type = "Relates" }));

        [Fact]
        public async Task AddLink_WhenAlreadyLinkedEitherWay_IsRejected()
        {
            SetLinks(new WorkItemLink { Id = 1, SourceId = 2, TargetId = 1, Type = "Relates" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 2, Type = "Blocks" }));
        }

        [Fact]
        public async Task AddLink_ThatClosesABlockingCycle_IsRejected()
        {
            // 2 blocks 3, 3 blocks 1  =>  1 blocks 2 would loop.
            SetLinks(
                new WorkItemLink { Id = 1, SourceId = 2, TargetId = 3, Type = "Blocks", Source = new ProjectTask { Id = 2, ProjectId = 1 } },
                new WorkItemLink { Id = 2, SourceId = 3, TargetId = 1, Type = "Blocks", Source = new ProjectTask { Id = 3, ProjectId = 1 } });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 2, Type = "Blocks" }));
            Assert.Contains("circular", ex.Message);
        }

        [Fact]
        public async Task AddLink_Relates_BetweenOtherwiseCircularItems_IsAllowed()
        {
            SetLinks(new WorkItemLink { Id = 1, SourceId = 2, TargetId = 3, Type = "Blocks", Source = new ProjectTask { Id = 2, ProjectId = 1 } });
            WorkItemLink? added = null;
            _links.When(r => r.Add(Arg.Any<WorkItemLink>())).Do(ci => { added = ci.Arg<WorkItemLink>(); added.Id = 9; SetLinks(added); });

            var dto = await CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 3, Type = "Relates" });

            Assert.Equal("Relates to", dto.Relation);
            Assert.Equal(3, dto.OtherId);
        }

        [Theory]
        [InlineData("Blocks", true, "Blocks")]
        [InlineData("Blocks", false, "Is blocked by")]
        [InlineData("Duplicates", true, "Duplicates")]
        [InlineData("Duplicates", false, "Is duplicated by")]
        [InlineData("Relates", true, "Relates to")]
        [InlineData("Relates", false, "Relates to")]
        public void Relation_ReadsFromTheViewedItemsSide(string type, bool outgoing, string expected) =>
            Assert.Equal(expected, PmCollaborationService.Relation(type, outgoing));

        // ---- attachments

        private static AttachmentUploadDto Upload(string name, long length) => new()
        {
            Content = new MemoryStream(new byte[Math.Max(length, 1)]), FileName = name, ContentType = "image/png", Length = length,
        };

        [Theory]
        [InlineData("shell.exe", 10)]
        [InlineData("page.html", 10)]
        [InlineData("noextension", 10)]
        public async Task AddAttachment_DisallowedType_IsRejected_AndNothingIsStored(string name, long length)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAttachmentAsync(Dev, 1, Upload(name, length)));
            await _storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
        }

        [Fact]
        public async Task AddAttachment_OverSizeLimit_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAttachmentAsync(Dev, 1, Upload("big.png", 5000)));

        [Fact]
        public async Task AddAttachment_Empty_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAttachmentAsync(Dev, 1, Upload("a.png", 0)));

        [Fact]
        public async Task AddAttachment_StoresUnderProjectKey_AndSanitisesTheClientName()
        {
            Attachment? saved = null;
            _attachments.When(r => r.Add(Arg.Any<Attachment>())).Do(ci => saved = ci.Arg<Attachment>());

            var dto = await CreateSut().AddAttachmentAsync(Dev, 1, Upload(@"..\..\evil\shot.PNG", 100));

            Assert.Equal("shot.PNG", saved!.FileName);
            Assert.StartsWith("pm/1/", saved.FilePath);
            Assert.DoesNotContain("..", saved.FilePath);
            Assert.True(dto.IsImage);
            await _storage.Received(1).SaveAsync(saved.FilePath, Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AddAttachment_WhenDbSaveFails_RemovesTheStoredObject()
        {
            _uow.SaveChangesAsync().Returns<int>(_ => throw new InvalidOperationException("db down"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAttachmentAsync(Dev, 1, Upload("a.png", 10)));
            await _storage.Received(1).DeleteAsync(Arg.Is<string>(k => k.StartsWith("pm/1/")), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteAttachment_OfSomeoneElse_RequiresDeletePermission()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, UploadedBy = 99, FilePath = "pm/1/x.png" } }.BuildMock());
            _access.RequireAsync(Dev, 1, PmAction.DeleteItem).Returns(Task.FromException(new ForbiddenException()));

            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().DeleteAttachmentAsync(Dev, 1));
            _attachments.DidNotReceiveWithAnyArgs().Remove(default!);
            await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        }

        [Fact]
        public async Task DeleteAttachment_OwnFile_RemovesRowAndObject()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, UploadedBy = 10, FilePath = "pm/1/x.png" } }.BuildMock());

            await CreateSut().DeleteAttachmentAsync(Dev, 1);

            _attachments.Received(1).Remove(Arg.Any<Attachment>());
            await _storage.Received(1).DeleteAsync("pm/1/x.png", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DownloadAttachment_WhenObjectIsMissing_IsNotFound()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, FilePath = "pm/1/x.png", FileName = "x.png" } }.BuildMock());
            _storage.OpenReadAsync("pm/1/x.png").ReturnsForAnyArgs(Task.FromResult<Stream?>(null));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().DownloadAttachmentAsync(Dev, 1));
        }

        [Fact]
        public async Task DownloadAttachment_ChecksViewAccessOnTheOwningProject()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, FilePath = "pm/1/x.png", FileName = "x.png" } }.BuildMock());
            _access.RequireAsync(Dev, 1, PmAction.View).Returns(Task.FromException(new KeyNotFoundException()));

            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().DownloadAttachmentAsync(Dev, 1));
            await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!);
        }

        // ---- hardening

        [Fact]
        public async Task AddAttachment_WithAVeryLongFileName_IsRejectedBeforeAnythingIsStored()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddAttachmentAsync(Dev, 1, Upload(new string('a', 200) + ".png", 10)));
            Assert.Contains("too long", ex.Message);
            await _storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
        }

        [Fact]
        public async Task AddAttachment_PrefersTheExtensionsTypeOverWhatTheClientClaims()
        {
            Attachment? saved = null;
            _attachments.When(r => r.Add(Arg.Any<Attachment>())).Do(ci => saved = ci.Arg<Attachment>());
            var file = new AttachmentUploadDto { Content = new MemoryStream(new byte[10]), FileName = "a.png", ContentType = "text/html", Length = 10 };

            await CreateSut().AddAttachmentAsync(Dev, 1, file);

            Assert.Equal("image/png", saved!.ContentType);
        }

        [Theory]
        [InlineData("not a media type")]
        [InlineData("")]
        public async Task AddAttachment_MalformedClientType_ForAnUnmappedExtension_FallsBackToOctetStream(string claimed)
        {
            Attachment? saved = null;
            _attachments.When(r => r.Add(Arg.Any<Attachment>())).Do(ci => saved = ci.Arg<Attachment>());
            var file = new AttachmentUploadDto { Content = new MemoryStream(new byte[10]), FileName = "notes.odt", ContentType = claimed, Length = 10 };

            await CreateSut().AddAttachmentAsync(Dev, 1, file);

            Assert.Equal("application/octet-stream", saved!.ContentType);
        }

        [Fact]
        public async Task AddAttachment_WellFormedClientType_ForAnUnmappedExtension_IsKept()
        {
            Attachment? saved = null;
            _attachments.When(r => r.Add(Arg.Any<Attachment>())).Do(ci => saved = ci.Arg<Attachment>());
            var file = new AttachmentUploadDto { Content = new MemoryStream(new byte[10]), FileName = "notes.odt", ContentType = "application/vnd.oasis.opendocument.text", Length = 10 };

            await CreateSut().AddAttachmentAsync(Dev, 1, file);

            Assert.Equal("application/vnd.oasis.opendocument.text", saved!.ContentType);
        }

        [Fact]
        public async Task Attachments_UploadedThroughTheClassicTasksTab_AreNotListedServedOrDeletedHere()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, UploadedBy = 10, FilePath = "3f2a_legacy.png", FileName = "legacy.png" } }.BuildMock());
            var sut = CreateSut();

            Assert.Empty(await sut.GetAttachmentsAsync(Dev, 1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DownloadAttachmentAsync(Dev, 1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DeleteAttachmentAsync(Dev, 1));
            await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        }

        [Fact]
        public async Task DeleteAttachment_WhenTheObjectDeleteFails_StillSucceeds()
        {
            _attachments.Query().Returns(_ => new List<Attachment> { new() { Id = 1, TaskId = 1, UploadedBy = 10, FilePath = "pm/1/x.png" } }.BuildMock());
            _storage.DeleteAsync("pm/1/x.png", Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("r2 down")));

            await CreateSut().DeleteAttachmentAsync(Dev, 1);

            _attachments.Received(1).Remove(Arg.Any<Attachment>());
        }

        [Fact]
        public async Task AddLink_StoresADirectionIndependentPairKey()
        {
            WorkItemLink? added = null;
            _links.When(r => r.Add(Arg.Any<WorkItemLink>())).Do(ci => { added = ci.Arg<WorkItemLink>(); added.Id = 9; SetLinks(added); });

            await CreateSut().AddLinkAsync(Dev, 3, new CreateWorkItemLinkDto { TargetId = 1, Type = "Relates" });

            Assert.Equal("1-3", added!.PairKey);
            Assert.Equal(WorkItemLink.KeyFor(1, 3), WorkItemLink.KeyFor(3, 1));
        }

        [Fact]
        public async Task AddLink_WhenTheUniquePairIndexRejectsIt_ReportsAlreadyLinked()
        {
            _uow.SaveChangesAsync().Returns<int>(_ => throw new DbUpdateException("duplicate key"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 2, Type = "Blocks" }));
            Assert.Contains("already linked", ex.Message);
        }

        [Fact]
        public async Task AddLink_WhenARacingRequestClosedALoopMeanwhile_BacksTheNewEdgeOut()
        {
            // Both pre-checks pass (no edges yet). After this request's edge is stored, the other request's
            // edges 2->3 and 3->1 are visible, so 1->2 now closes a loop and must be removed again.
            var saves = 0;
            _uow.SaveChangesAsync().Returns(_ =>
            {
                if (++saves == 1)
                    SetLinks(
                        new WorkItemLink { Id = 1, SourceId = 2, TargetId = 3, Type = "Blocks", Source = new ProjectTask { Id = 2, ProjectId = 1 } },
                        new WorkItemLink { Id = 2, SourceId = 3, TargetId = 1, Type = "Blocks", Source = new ProjectTask { Id = 3, ProjectId = 1 } });
                return Task.FromResult(1);
            });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AddLinkAsync(Dev, 1, new CreateWorkItemLinkDto { TargetId = 2, Type = "Blocks" }));

            Assert.Contains("circular", ex.Message);
            _links.Received(1).Remove(Arg.Is<WorkItemLink>(l => l.SourceId == 1 && l.TargetId == 2));
            Assert.Equal(2, saves);
        }
    }
}
