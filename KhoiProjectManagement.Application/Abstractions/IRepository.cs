namespace KhoiProjectManagement.Application.Abstractions
{
    // Generic persistence port - Application composes LINQ (Where/Include/OrderBy/EF.Functions) against
    // Query() exactly as it did against ProjectManagementContext.XxxSet before this port existed; only
    // the concrete DbContext dependency is removed, not EF Core's query surface. Infrastructure's
    // Repository<T> is the only thing that knows this is backed by ProjectManagementContext.
    public interface IRepository<T> where T : class
    {
        IQueryable<T> Query();

        // Mirrors DbSet<T>.FindAsync - PK lookup that checks the unit of work's local tracked-entity
        // cache before hitting the DB. Kept distinct from Query().FirstOrDefaultAsync(...) because it's
        // not equivalent: FindAsync is PK-only, doesn't support Include, and is cache-aware.
        ValueTask<T?> FindAsync(params object[] keyValues);

        void Add(T entity);
        void AddRange(IEnumerable<T> entities);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
    }
}
