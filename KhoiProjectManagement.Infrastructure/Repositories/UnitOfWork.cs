using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Infrastructure.Data;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ProjectManagementContext _context;

        public UnitOfWork(ProjectManagementContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<IAppTransaction> BeginTransactionAsync()
        {
            var transaction = await _context.Database.BeginTransactionAsync();
            return new EfAppTransaction(transaction);
        }
    }
}
