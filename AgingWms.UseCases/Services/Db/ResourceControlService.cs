using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Repositoy;
using System;
using System.Threading.Tasks;

namespace AgingWms.UseCases.Services.DB
{
    public class ResourceControlService<T> where T : ProcessingNode
    {
        // 保持 protected，子类 (SlotCommandService) 需要用
        protected readonly IRepository<T> _repository;

        public ResourceControlService(IRepository<T> repository)
        {
            _repository = repository;
        }

        public async Task<T?> GetAsync(string id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task AddAsync(T entity)
        {
            // 1. 内存标记
            await _repository.AddAsync(entity);
            // 2. 【关键】显式提交，因为仓储层现在不自动保存了
            await _repository.SaveChangesAsync();
        }

        // --- 供后台或简单业务调用的状态更新 ---
        public async Task<bool> UpdateStatusAsync(string id, SlotStatus newStatus, string? operatorReason = null)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null) return false;

                entity.UpdateStatus(newStatus);

                // 1. 内存标记
                await _repository.UpdateAsync(entity);
                // 2. 【关键修复】必须调用保存，否则数据不会生效
                await _repository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                throw;
            }
        }

        // --- 【新增】供 UI 层调用的安全更新方法 (防崩溃) ---
        // 封装了并发异常处理，UI 按钮直接调这个
        public async Task<bool> UpdateAsync(T entity, Action<string> onError = null)
        {
            try
            {
                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync(); // 统一提交
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // 拦截并发冲突，通知 UI
                onError?.Invoke("当前数据已被其他人或后台修改，保存失败。系统已自动为您加载最新数据，请确认后重试。");
                return false;
            }
            catch (Exception ex)
            {
                onError?.Invoke($"保存发生未知错误: {ex.Message}");
                return false;
            }
        }
    }
}