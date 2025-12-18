using AgingWms.Infrastructure.Data;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System.Threading;
using System.Threading.Tasks;

namespace AgingWms.Infrastructure.Repositories
{
    // 用于处理不是 AggregateRoot 的普通 Entity
    public class EfGenericRepository<T> : EfReadRepository<T>, IGenericRepository<T>
        where T : class, IEntity
    {
        // 这里的 Context 需要强类型 AgingWmsDbContext 吗？
        // 按照你上传的代码，这里注入了 AgingWmsDbContext，保持一致。
        private readonly AgingWmsDbContext _dbContext;

        public EfGenericRepository(AgingWmsDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // 这里仅实现了 SaveChangesAsync，假设 Add/Update 等方法在 IGenericRepository 接口中未定义或通过其他方式实现？
        // 按照您提供的原文件，这里只有 SaveChangesAsync。如果 IGenericRepository 有 Add/Update，请务必参照 EfRepository 去除自动提交。
        // 基于上传文件，保持原样：

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}