namespace KhoiProjectManagement.Application.Abstractions
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync();

        // Only needed by services that make several SaveChangesAsync calls that must succeed or fail
        // together (e.g. ProjectService/TaskService creating a parent row plus related rows). Returns
        // IAppTransaction, not EF Core's IDbContextTransaction, so this interface stays EF-agnostic.
        Task<IAppTransaction> BeginTransactionAsync();
    }
}
