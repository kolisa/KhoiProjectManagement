using KhoiProjectManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    internal class EfAppTransaction : IAppTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfAppTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync() => _transaction.CommitAsync();
        public Task RollbackAsync() => _transaction.RollbackAsync();
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
