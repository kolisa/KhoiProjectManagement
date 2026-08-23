namespace KhoiProjectManagement.Application.Abstractions
{
    public interface IAppTransaction : IAsyncDisposable
    {
        Task CommitAsync();
        Task RollbackAsync();
    }
}
