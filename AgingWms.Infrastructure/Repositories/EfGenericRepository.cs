using AgingWms.Infrastructure.Data;
using SharedKernel.Domain;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Infrastructure.Repositories
{
    public class EfGenericRepository<T>(AgingWmsDbContext dbContext)
                : EfReadRepository<T>(dbContext), IGenericRepository<T> where T : class, IEntity
    {
        private readonly AgingWmsDbContext _dbContext = dbContext;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}