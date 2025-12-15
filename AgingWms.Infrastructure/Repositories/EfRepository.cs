using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Infrastructure.Repositories
{
    public class EfRepository<T> : EfReadRepository<T>, IRepository<T>
            where T : class, IEntity, IAggregateRoot
    {
        public EfRepository(DbContext dbContext) : base(dbContext)
        {
        }

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

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> BatchDeleteAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvaluator.GetQuery(_dbSet, specification).ExecuteDeleteAsync(cancellationToken);
        }

        // 你的批量替换逻辑
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
                    // 这里注意 Schema 处理
                    var schema = entityType.GetSchema() ?? "dbo";

                    // 1. 清空表 (SQL Server 语法)
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