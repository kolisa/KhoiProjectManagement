using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // EnsureProjectSpaceAsync was refactored to set the ParentSpace/Space navigation properties
    // (instead of the *Id FK columns directly) so a brand-new root Space, the new project Space, and
    // the Project's own SpaceId update all commit in a single SaveChangesAsync - that FK-resolution-
    // from-navigation behavior is genuine EF Core change-tracker fixup, not something a mocked
    // IRepository<T> (NSubstitute, see SpaceServiceTests) can exercise. Same technique as
    // NotificationServiceSerializationTests: a real ProjectManagementContext against EF Core's
    // InMemory provider, wrapped in the real Repository<T>/UnitOfWork adapters, so the actual
    // SpaceService code under test is exercised unmodified.
    public class SpaceServiceEnsureProjectSpaceTests : IDisposable
    {
        private readonly ProjectManagementContext _context;

        public SpaceServiceEnsureProjectSpaceTests()
        {
            var options = new DbContextOptionsBuilder<ProjectManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ProjectManagementContext(options);
        }

        public void Dispose() => _context.Dispose();

        private SpaceService CreateSut() => new(
            new Repository<Project>(_context),
            new Repository<Space>(_context),
            new Repository<SpacePermission>(_context),
            new Repository<User>(_context),
            new Repository<UserRole>(_context),
            new Repository<UserGroup>(_context),
            Substitute.For<ISpaceDeletionBlockersRepository>(),
            new UnitOfWork(_context),
            Substitute.For<ISpacePermissionResolver>());

        [Fact]
        public async Task WhenNoRootSpaceExistsYet_CreatesRootAndProjectSpaceInOneSaveWithCorrectParentage()
        {
            var project = new Project { Name = "Test Project", CreatedBy = 1 };
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var spaceId = await CreateSut().EnsureProjectSpaceAsync(project.Id, createdByUserId: 1);

            var projectSpace = await _context.Spaces.FindAsync(spaceId);
            Assert.NotNull(projectSpace);
            Assert.NotNull(projectSpace!.ParentSpaceId);

            var rootSpace = await _context.Spaces.FindAsync(projectSpace.ParentSpaceId!.Value);
            Assert.NotNull(rootSpace);
            Assert.Equal("Projects", rootSpace!.Name);
            Assert.Null(rootSpace.ParentSpaceId);

            var reloadedProject = await _context.Projects.FindAsync(project.Id);
            Assert.Equal(spaceId, reloadedProject!.SpaceId);
        }

        [Fact]
        public async Task WhenARootSpaceAlreadyExists_ReusesItAsTheParentInsteadOfCreatingAnother()
        {
            var existingRoot = new Space { Name = "Projects", SpaceType = SpaceType.ProjectSpace, CreatedBy = 1 };
            _context.Spaces.Add(existingRoot);
            var project = new Project { Name = "Test Project", CreatedBy = 1 };
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var spaceId = await CreateSut().EnsureProjectSpaceAsync(project.Id, createdByUserId: 1);

            var projectSpace = await _context.Spaces.FindAsync(spaceId);
            Assert.Equal(existingRoot.Id, projectSpace!.ParentSpaceId);

            var rootSpaceCount = await _context.Spaces.CountAsync(s => s.ParentSpaceId == null && s.SpaceType == SpaceType.ProjectSpace);
            Assert.Equal(1, rootSpaceCount);
        }

        [Fact]
        public async Task WhenTheProjectAlreadyHasASpace_ReturnsItWithoutCreatingAnything()
        {
            var project = new Project { Name = "Test Project", CreatedBy = 1 };
            var existingSpace = new Space { Name = "Test Project", SpaceType = SpaceType.ProjectSpace, CreatedBy = 1 };
            _context.Spaces.Add(existingSpace);
            project.Space = existingSpace;
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var spaceCountBefore = await _context.Spaces.CountAsync();
            var spaceId = await CreateSut().EnsureProjectSpaceAsync(project.Id, createdByUserId: 1);

            Assert.Equal(existingSpace.Id, spaceId);
            Assert.Equal(spaceCountBefore, await _context.Spaces.CountAsync());
        }
    }
}
