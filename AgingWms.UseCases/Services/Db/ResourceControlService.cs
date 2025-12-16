using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Repositoy;
using System;
using System.Threading.Tasks;

namespace AgingWms.UseCases.Services.DB
{
    public class ResourceControlService<T> where T : ProcessingNode
    {
        // 改为 protected，让子类 (SlotCommandService) 也能直接用仓储
        protected readonly IRepository<T> _repository;

        public ResourceControlService(IRepository<T> repository)
        {
            _repository = repository;
        }

        public async Task<T?> GetAsync(string id)
        {
            return await _repository.GetByIdAsync(id);
        }

        // 【修复报错】补充 AddAsync 方法
        public async Task AddAsync(T entity)
        {
            await _repository.AddAsync(entity);
            // 确保保存
            await _repository.SaveChangesAsync();
        }

        public async Task<bool> UpdateStatusAsync(string id, SlotStatus newStatus, string? operatorReason = null)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null) return false;

                // 使用实体方法更新状态
                entity.UpdateStatus(newStatus);

                await _repository.UpdateAsync(entity);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                throw;
            }
        }
    }
}