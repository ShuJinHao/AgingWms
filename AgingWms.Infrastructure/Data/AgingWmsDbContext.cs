using AgingWms.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgingWms.Infrastructure.Data
{
    public class AgingWmsDbContext : DbContext
    {
        public AgingWmsDbContext(DbContextOptions<AgingWmsDbContext> options) : base(options)
        {
        }

        public DbSet<WarehouseSlot> WarehouseSlots { get; set; }
        public DbSet<BatteryCell> BatteryCells { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 配置 WarehouseSlot
            modelBuilder.Entity<WarehouseSlot>(entity =>
            {
                // 【关键修复】这里原来的 e.SlotId 必须改成 e.Id
                entity.HasKey(e => e.Id);

                // 如果数据库里的列名还想保留叫 "SlotId"，可以用这一行映射回去：
                // entity.Property(e => e.Id).HasColumnName("SlotId");

                entity.Property(e => e.SlotName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TrayBarcode).HasMaxLength(50);

                // 映射基类属性
                entity.Property(e => e.Status);
                entity.Property(e => e.LastUpdatedTime);
                entity.Property(e => e.RowVersion).IsRowVersion(); // 乐观锁

                // 配置与电芯的一对多关系
                entity.HasMany(e => e.Cells)
                      .WithOne()
                      .HasForeignKey(c => c.WarehouseSlotId);
            });

            // 2. 配置 BatteryCell
            modelBuilder.Entity<BatteryCell>(entity =>
            {
                // 【关键修复】这里也要用 Id (对应原来的 Barcode)
                entity.HasKey(e => e.Id);

                // 如果你想让数据库列名更直观，可以映射一下：
                // entity.Property(e => e.Id).HasColumnName("Barcode");

                entity.Property(e => e.ChannelIndex);
                entity.Property(e => e.IsNg);

                // 映射基类属性
                entity.Property(e => e.Status);
                entity.Property(e => e.LastUpdatedTime);
                entity.Property(e => e.RowVersion).IsRowVersion();

                // 配置忽略 ProcessSteps (因为它是一个 List 对象，SQL 存不下，或者需要转 JSON 存)
                // 如果你之前是用 OwnsMany 存的，保留之前的写法；
                // 如果仅仅是内存对象，不想存数据库，就 Ignore 掉：
                entity.Ignore(e => e.ProcessSteps);
            });
        }
    }
}