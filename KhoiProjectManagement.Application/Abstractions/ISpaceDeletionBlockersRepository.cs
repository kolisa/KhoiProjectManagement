namespace KhoiProjectManagement.Application.Abstractions
{
    // A deliberate, narrow escape hatch from the generic IRepository<T> pattern, same reasoning as
    // IWikiSearchRepository/IDashboardStatsRepository: SpaceService.DeleteSpaceAsync only ever needed
    // one boolean ("does anything still reference this Space"), but checking it via four separate
    // IRepository<T>.AnyAsync() calls (one per table: child Spaces, VaultEntries, WikiPages,
    // LibraryFiles) meant four round trips for a single yes/no answer. One SQL statement with four
    // EXISTS subqueries, OR'd together, replaces that - Dapper, not EF, since a scalar boolean has no
    // entity shape to project into and spans tables no single IRepository<T> owns.
    public interface ISpaceDeletionBlockersRepository
    {
        Task<bool> HasBlockingChildrenAsync(int spaceId);
    }
}
