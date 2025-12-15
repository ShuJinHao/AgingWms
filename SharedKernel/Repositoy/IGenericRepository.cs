using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Repositoy
{
    /// <summary>
    ///     <para>
    ///         <see cref="IRepository{T}" /> 用于查询和保存 <typeparamref name="T" />
    ///     </para>
    /// </summary>
    /// <typeparam name="T">该仓储操作的通用实体类型</typeparam>
    public interface IGenericRepository<T> : IReadRepository<T> where T : class, IEntity
    {
        /// <summary>
        ///     持久化实体到数据库
        /// </summary>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}