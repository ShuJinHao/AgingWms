using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AgingWms.Infrastructure.Data.Configurations // 确认你的命名空间
{
    public class WarehouseSlotConfiguration : IEntityTypeConfiguration<WarehouseSlot>
    {
        public void Configure(EntityTypeBuilder<WarehouseSlot> builder)
        {
            // 1. 映射表名
            builder.ToTable("Wms_WarehouseSlots");

            // 2. 设置主键 (修正点：SlotId -> Id)
            builder.HasKey(x => x.Id);

            // 如果你想保持数据库里的列名依然叫 "SlotId" (方便旧数据兼容)，可以加上 .HasColumnName
            builder.Property(x => x.Id)
                   .HasColumnName("SlotId") // 可选：保持数据库列名不变
                   .HasMaxLength(64);

            // 3. 基础字段配置
            builder.Property(x => x.SlotName).HasMaxLength(100);
            builder.Property(x => x.TrayBarcode).HasMaxLength(100);

            // 映射基类 ProcessingNode 的字段
            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.LastUpdatedTime);
            builder.Property(x => x.RowVersion).IsRowVersion(); // 【高并发核心】乐观锁

            // 4. 【架构升级】一对多关系配置 (替代原来的 JSON)
            // 既然 Cell 是独立的处理节点，它应该存独立的表，这样才能支持电芯级的高并发锁
            builder.HasMany(x => x.Cells)
                   .WithOne()
                   .HasForeignKey("WarehouseSlotId") // 对应 BatteryCell 里的外键属性
                   .OnDelete(DeleteBehavior.Cascade); // 删除库位时，级联删除电芯

            // 5. 如果你确实想存 JSON (仅针对不需要查询/锁定的纯数据对象)
            // 这里的 ProcessSteps 才是适合存 JSON 的地方，因为它不需要独立并发控制
            // (注意：这里假设你在基类或子类里使用了 ExtensionDataJson)
            builder.Property(x => x.ExtensionDataJson)
                   .HasColumnType("nvarchar(max)");
        }
    }
}