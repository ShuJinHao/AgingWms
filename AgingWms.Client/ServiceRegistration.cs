using AgingWms.Core.Domain;
using AgingWms.Infrastructure.Data;
using AgingWms.Infrastructure.Repositories;
using AgingWms.UseCases.Consumers.Wms.Commands;
using AgingWms.UseCases.Consumers.Wms.Queries;
using AgingWms.UseCases.Mappings;
using AgingWms.UseCases.Services.DB;
using AgingWms.UseCases.Services.Notify;
using AgingWms.UseCases.Services.Request;
using AgingWms.Workflow.Consumers; // 确保引用了这个
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

            // 1. 数据库
            services.AddDbContextPool<AgingWmsDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<AgingWmsDbContext>());

            // 2. 仓储
            services.AddScoped(typeof(IReadRepository<>), typeof(EfReadRepository<>));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped(typeof(IGenericRepository<>), typeof(EfGenericRepository<>));

            // 3. 业务服务 (确保 SlotCommandService 在这里!)
            services.AddScoped<ResourceControlService<WarehouseSlot>>();
            services.AddScoped<ResourceControlService<BatteryCell>>();

            services.AddScoped<WmsRequestService>();

            // 【关键】注册 SlotCommandService
            services.AddScoped<SlotCommandService>();

            services.AddScoped<AgingJobService>();
            services.AddScoped<AgingProcessWorkflow>();

            // 4. UI 监控
            services.AddSingleton<RealTimeMonitorService>();
            services.AddMemoryCache();

            // 5. Mapper
            services.AddAutoMapper(cfg => cfg.AddProfile<AgingWmsMappingProfile>());

            // 6. MassTransit
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                // 消费者
                x.AddConsumer<SlotCommandConsumer>();
                x.AddConsumer<SlotQueryConsumer>();
                x.AddConsumer<AgingJobConsumer>();
                x.AddConsumer<DashboardConsumer>(); // 如果没有这个类先注释掉

                // 自动寻找 Activity
                // 确保你的项目中引用了包含 RestActivity 的命名空间，如果报错找不到类，请改为 x.AddActivitiesFromNamespaceContaining<AgingWms.Workflow.Activities.RestActivity>();
                x.AddActivitiesFromNamespaceContaining<AgingWms.Workflow.Activities.RestActivity>();

                // 请求客户端配置
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseNewtonsoftJsonSerializer();
                    cfg.UseNewtonsoftJsonDeserializer();
                    cfg.ConfigureEndpoints(context);
                });
            });

            // 7. 【关键修复】MainWindow 注册为 Singleton
            // 我们将在 MainWindow 里注入 IBus 来规避作用域问题
            services.AddSingleton<MainWindow>();

            return services;
        }
    }
}