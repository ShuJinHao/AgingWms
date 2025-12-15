using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Repositoy
{
    public interface IRepository<T> : IReadRepository<T> where T : class, IEntity, IAggregateRoot
    {
        T Add(T entity);

        void Update(T entity);

        void Delete(T entity);

        Task<int> BatchDeleteAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // 你文件里的特殊方法
        Task ReplaceTableDataAsync(List<T> newData);
    }
}