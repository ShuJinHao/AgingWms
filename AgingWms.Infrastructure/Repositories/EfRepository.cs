using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Linq; // 用于 Chunk
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

        // --- 同步实现 (保留) ---
        public T Add(T entity)
        {
            _dbSet.Add(entity);
            return entity;
        }

        public void Update(T entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
            _dbSet.Update(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        // --- 【关键修复】异步实现 ---

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
            // 自动保存，让 Service 层代码更简洁
            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            // EF Core 内存操作
            _dbContext.Entry(entity).State = EntityState.Modified;
            _dbSet.Update(entity);
            // 立即异步提交数据库
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _dbSet.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // --- 其他原有方法 (保留) ---

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> BatchDeleteAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
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

                    // 1. 清空表
                    await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM [{schema}].[{tableName}]");

                    // 2. 分批插入
                    foreach (var chunk in newData.Chunk(1000))
                    {
                        await _dbSet.AddRangeAsync(chunk);
                        await _dbContext.SaveChangesAsync();
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