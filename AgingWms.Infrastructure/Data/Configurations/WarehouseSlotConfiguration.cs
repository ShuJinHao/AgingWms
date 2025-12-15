using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AgingWms.Infrastructure.Data.Configurations
{
    public class WarehouseSlotConfiguration : IEntityTypeConfiguration<WarehouseSlot>
    {
        public void Configure(EntityTypeBuilder<WarehouseSlot> builder)
        {
            // 1. 映射表名
            builder.ToTable("Wms_WarehouseSlots");

            // 2. 设置主键
            builder.HasKey(x => x.SlotId);
            builder.Property(x => x.SlotId).HasMaxLength(64); // 根据实际长度调整

            // 3. 基础字段配置
            builder.Property(x => x.SlotName).HasMaxLength(100);
            builder.Property(x => x.TrayBarcode).HasMaxLength(100);
            builder.Property(x => x.Status).IsRequired(); // Enum 默认存 int

            // 4. 【核心】JSON 序列化配置
            // 将 List<BatteryCell> 转换为 JSON 字符串存储
            // 这样里面的 ProcessSteps 和 Metrics 都会自动包含在内
            builder.Property(x => x.Cells)
                .HasConversion(
                    // 写入: 对象 -> JSON 字符串
                    v => JsonConvert.SerializeObject(v, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }),

                    // 读取: JSON 字符串 -> 对象
                    v => string.IsNullOrEmpty(v)
                        ? new List<BatteryCell>()
                        : JsonConvert.DeserializeObject<List<BatteryCell>>(v) ?? new List<BatteryCell>()
                )
                .HasColumnType("nvarchar(max)"); // SQL Server 大文本类型

            // 5. 忽略字段 (如果有不需要映射的字段，使用 .Ignore())
            // builder.Ignore(x => x.SomeProperty);
        }
    }
}