using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbSet<T> _set;

        public Repository(ProjectManagementContext context)
        {
            _set = context.Set<T>();
        }

        public IQueryable<T> Query() => _set;
        public ValueTask<T?> FindAsync(params object[] keyValues) => _set.FindAsync(keyValues);
        public void Add(T entity) => _set.Add(entity);
        public void AddRange(IEnumerable<T> entities) => _set.AddRange(entities);
        public void Remove(T entity) => _set.Remove(entity);
        public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);
    }
}
