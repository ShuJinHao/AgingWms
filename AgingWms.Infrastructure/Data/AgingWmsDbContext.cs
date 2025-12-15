using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace AgingWms.Infrastructure.Data
{
    public class AgingWmsDbContext : DbContext
    {
        public AgingWmsDbContext(DbContextOptions<AgingWmsDbContext> options) : base(options)
        {
        }

        // 核心数据集：库位
        public DbSet<WarehouseSlot> Slots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 WarehouseSlot 实体
            modelBuilder.Entity<WarehouseSlot>(entity =>
            {
                entity.ToTable("Wms_WarehouseSlots");

                // 设置主键
                entity.HasKey(e => e.SlotId);

                // 索引优化：经常要查状态和最后更新时间
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.LastUpdatedTime);

                // 【关键策略】值转换器：将复杂的 Cells 对象列表映射为 JSON 字符串
                entity.Property(e => e.Cells)
                    .HasConversion(
                        // 写入数据库时：对象 -> JSON
                        v => JsonConvert.SerializeObject(v, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                        // 从数据库读取时：JSON -> 对象
                        v => JsonConvert.DeserializeObject<List<BatteryCell>>(v) ?? new List<BatteryCell>()
                    )
                    // SQL Server 中使用 NVARCHAR(MAX) 存储大文本
                    .HasColumnType("nvarchar(max)");

                // 并发控制 (可选，防止两个人同时修改同一个库位)
                entity.Property(e => e.LastUpdatedTime).IsConcurrencyToken();
            });
        }
    }
}