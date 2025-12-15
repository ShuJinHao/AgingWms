using AgingWms.Infrastructure.Data;
using AgingWms.Infrastructure.Repositories;
using AgingWms.UseCases.Consumers.Wms.Commands;
using AgingWms.UseCases.Consumers.Wms.Queries;
using AgingWms.UseCases.Mappings;
using AgingWms.UseCases.Services;
using AgingWms.Workflow.Activities;
using AgingWms.Workflow.Consumers;
using AgingWms.Workflow.Services;
using AgingWms.Workflow.Workflows;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Repositoy;
using System;

namespace AgingWms.Client
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddAgingWmsServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // 1. 数据库与上下文
            services.AddDbContextPool<AgingWmsDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<AgingWmsDbContext>());

            // 2. 仓储注册
            services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped(typeof(IGenericRepository<>), typeof(EfGenericRepository<>));

            // 3. 业务服务注册
            services.AddScoped<SlotCommandService>();
            services.AddScoped<AgingJobService>();
            // 【注意】这里注册了工作流，它依赖 IEndpointNameFormatter
            services.AddScoped<AgingProcessWorkflow>();

            // 4. UI 实时监控服务 (必须单例)
            services.AddSingleton<RealTimeMonitorService>();
            services.AddMemoryCache();

            // 5. AutoMapper
            services.AddAutoMapper(cfg => cfg.AddProfile<AgingWmsMappingProfile>());

            // 6. MassTransit 完整配置
            services.AddMassTransit(x =>
            {
                // =================================================================
                // 【修复点 1：必须加】注册 EndpointNameFormatter
                // 你的 Workflow 类构造函数里要了这个，不加这行，消费者直接创建失败！
                // =================================================================
                x.SetKebabCaseEndpointNameFormatter();

                // 注册消费者
                x.AddConsumer<SlotCommandConsumer>();
                x.AddConsumer<SlotQueryConsumer>();
                x.AddConsumer<AgingJobConsumer>();      // 核心任务消费者
                x.AddConsumer<DashboardConsumer>();     // UI 看板消费者

                // 注册所有 Activities
                x.AddActivitiesFromNamespaceContaining<RestActivity>();

                // 注册请求客户端
                x.AddRequestClient<SharedKernel.Contracts.SaveSlotData>();
                x.AddRequestClient<SharedKernel.Contracts.RelocateSlot>();
                x.AddRequestClient<SharedKernel.Contracts.ClearSlot>();
                x.AddRequestClient<SharedKernel.Contracts.GetSlotQuery>();

                x.UsingInMemory((context, cfg) =>
                {
                    // =================================================================
                    // 【修复点 2：必须加】 强制使用 Newtonsoft.Json
                    // 解决 JObject/JToken 报错，必须放在 ConfigureEndpoints 之前！
                    // =================================================================
                    cfg.UseNewtonsoftJsonSerializer();
                    cfg.UseNewtonsoftJsonDeserializer();

                    // 重试策略
                    cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromMilliseconds(100)));
                    cfg.UseConcurrencyLimit(10);

                    // 自动配置所有断点
                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}