using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgingWms.Infrastructure.Repositories
{
    public class EfRepository<T> : EfReadRepository<T>, IRepository<T>
            where T : class, IEntity, IAggregateRoot
    {
        public EfRepository(DbContext dbContext) : base(dbContext)
        {
        }

        // =========================================================
        // 同步方法：纯内存操作，不提交
        // =========================================================
        public T Add(T entity)
        {
            _dbSet.Add(entity);
            return entity;
        }

        public void Update(T entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
            // _dbSet.Update(entity); // 这一句其实和上面重复，保留一个即可，通常 Entry.State 更底层稳健
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        // =========================================================
        // 异步方法：【核心修复】纯内存操作，不自动 SaveChanges
        // =========================================================

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            // 仅异步添加到上下文追踪，不写库
            await _dbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            // 标记为修改状态，不写库
            _dbContext.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask; // 为了满足接口的 Task 返回值
        }

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            // 标记为删除状态，不写库
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        // =========================================================
        // 事务提交入口：由 Service 层手动调用
        // =========================================================

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // =========================================================
        // 特殊工具方法 (保留原样)
        // =========================================================

        public async Task<int> BatchDeleteAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            // 这里的 ExecuteDeleteAsync 是 EF Core 7.0+ 特性，它是立即生效的，不受 ChangeTracker 控制
            // 这是符合预期的“特例”
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task ReplaceTableDataAsync(List<T> newData)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    var entityType = _dbContext.Model.FindEntityType(typeof(T));
                    var tableName = entityType.GetTableName();
                    var schema = entityType.GetSchema() ?? "dbo";

                    // 1. 清空表 (立即生效)
                    await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM [{schema}].[{tableName}]");

                    // 2. 分批插入
                    foreach (var chunk in newData.Chunk(1000))
                    {
                        await _dbSet.AddRangeAsync(chunk);
                        await _dbContext.SaveChangesAsync(); // 这里的 SaveChanges 是必须的，因为是分批处理大数据的特殊逻辑
                        _dbContext.ChangeTracker.Clear();
                    }
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }
}