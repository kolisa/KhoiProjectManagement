using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // SpacePermissionResolver takes a concrete ProjectManagementContext, not IRepository<T> - seeded
    // with EF Core's InMemory provider here rather than mocked, since the class under test is really
    // "a query + an in-process cache", not something meaningfully mockable at the repository level.
    public class SpacePermissionResolverTests : IDisposable
    {
        private readonly ProjectManagementContext _context;

        public SpacePermissionResolverTests()
        {
            var options = new DbContextOptionsBuilder<ProjectManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ProjectManagementContext(options);
        }

        public void Dispose() => _context.Dispose();

        private SpacePermissionResolver CreateSut() =>
            new(_context, new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenGrantExistsOnTheExactSpace_ReturnsThatLevel()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Vault Root", CreatedBy = 1 });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, UserId = 42, Level = PermissionLevel.Write, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(1, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());

            Assert.Equal(PermissionLevel.Write, result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenNoLocalGrantButInherits_WalksUpToParentGrant()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Root", CreatedBy = 1, InheritPermissions = true });
            _context.Spaces.Add(new Space { Id = 2, Name = "Child", ParentSpaceId = 1, CreatedBy = 1, InheritPermissions = true });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, UserId = 42, Level = PermissionLevel.Read, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(2, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());

            Assert.Equal(PermissionLevel.Read, result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenBoundaryBlocksInheritance_DeniesEvenWithParentGrant()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Root", CreatedBy = 1, InheritPermissions = true });
            _context.Spaces.Add(new Space { Id = 2, Name = "Locked Child", ParentSpaceId = 1, CreatedBy = 1, InheritPermissions = false });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, UserId = 42, Level = PermissionLevel.Manage, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(2, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());

            Assert.Null(result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenMultipleGrantsMatch_ReturnsTheMaxLevel()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Shared", CreatedBy = 1 });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, UserId = 42, Level = PermissionLevel.Read, CreatedBy = 1 });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, RoleId = 7, Level = PermissionLevel.Manage, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(1, userId: 42, roleIds: new[] { 7 }, groupIds: Array.Empty<int>());

            Assert.Equal(PermissionLevel.Manage, result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenGroupGrantMatches_ReturnsThatLevel()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Shared", CreatedBy = 1 });
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, GroupId = 9, Level = PermissionLevel.Write, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(1, userId: 42, roleIds: Array.Empty<int>(), groupIds: new[] { 9 });

            Assert.Equal(PermissionLevel.Write, result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenSpaceDoesNotExist_ReturnsNull()
        {
            var result = await CreateSut().ResolveEffectiveLevelAsync(999, userId: 1, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());

            Assert.Null(result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_WhenReachingRootWithNoGrant_ReturnsNull()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Root", CreatedBy = 1, InheritPermissions = true });
            await _context.SaveChangesAsync();

            var result = await CreateSut().ResolveEffectiveLevelAsync(1, userId: 1, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());

            Assert.Null(result);
        }

        [Fact]
        public async Task ResolveEffectiveLevelAsync_CachesSnapshot_UntilInvalidated()
        {
            _context.Spaces.Add(new Space { Id = 1, Name = "Root", CreatedBy = 1 });
            await _context.SaveChangesAsync();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var sut = new SpacePermissionResolver(_context, cache);

            var beforeGrant = await sut.ResolveEffectiveLevelAsync(1, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());
            Assert.Null(beforeGrant);

            // Add a grant directly in the DB without invalidating the cache - resolver should still see
            // the stale (no-grant) snapshot it already cached.
            _context.SpacePermissions.Add(new SpacePermission { SpaceId = 1, UserId = 42, Level = PermissionLevel.Read, CreatedBy = 1 });
            await _context.SaveChangesAsync();

            var stillCached = await sut.ResolveEffectiveLevelAsync(1, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());
            Assert.Null(stillCached);

            sut.InvalidateCache();
            var afterInvalidate = await sut.ResolveEffectiveLevelAsync(1, userId: 42, roleIds: Array.Empty<int>(), groupIds: Array.Empty<int>());
            Assert.Equal(PermissionLevel.Read, afterInvalidate);
        }
    }
}
