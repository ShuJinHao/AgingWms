using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;       // 【新增】引用
using System.Threading.Tasks; // 【新增】引用

namespace SharedKernel.Repositoy
{
    public interface IRepository<T> : IReadRepository<T> where T : class, IEntity, IAggregateRoot
    {
        // --- 原有同步方法 (保留) ---
        T Add(T entity);

        void Update(T entity);

        void Delete(T entity);

        // --- 【关键修复】新增异步方法 (Fix 报错的核心) ---
        // 配合 ResourceControlService 使用
        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

        Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

        Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

        // --- 原有其他方法 (保留) ---
        Task<int> BatchDeleteAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task ReplaceTableDataAsync(List<T> newData);
    }
}