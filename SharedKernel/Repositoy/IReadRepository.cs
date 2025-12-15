using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Repositoy
{
    // 3. 读仓储接口
    public interface IReadRepository<T> where T : class, IEntity
    {
        Task<T?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default);

        Task<List<T>> GetListAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

        Task<T?> GetSingleOrDefaultAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

        Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

        Task<bool> AnyAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);
    }
}