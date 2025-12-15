using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Infrastructure.Repositories
{
    public class EfReadRepository<T> : IReadRepository<T> where T : class, IEntity
    {
        // 改为 protected 以便子类访问
        protected readonly DbContext _dbContext;

        protected readonly DbSet<T> _dbSet;

        public EfReadRepository(DbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = dbContext.Set<T>();
        }

        public async Task<T?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<List<T>> GetListAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).ToListAsync(cancellationToken);
        }

        // ... 实现其他 CountAsync, AnyAsync (参考你提供的代码)
        public async Task<bool> AnyAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).AnyAsync(cancellationToken);
        }

        public async Task<T?> GetSingleOrDefaultAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).CountAsync(cancellationToken);
        }
    }
}