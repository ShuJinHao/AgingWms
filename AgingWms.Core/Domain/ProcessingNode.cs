using SharedKernel.Contracts; // 确保引用了 IEntity 所在的命名空间
using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgingWms.Core.Domain
{
    public abstract class ProcessingNode : IEntity, IAggregateRoot
    {
        // 1. 我们自己用的强类型 ID
        public string Id { get; protected set; }

        object IEntity.Id => Id;

        // 2. 状态机
        public SlotStatus Status { get; protected set; }

        // 3. 乐观并发锁
        [Timestamp]
        public byte[] RowVersion { get; private set; }

        // 4. 最后活跃时间
        public DateTime LastUpdatedTime { get; protected set; }

        // 5. 扩展数据
        public string? ExtensionDataJson { get; protected set; }

        // EF Core 需要无参构造函数
        protected ProcessingNode() { }

        public ProcessingNode(string id)
        {
            Id = id;
            Status = SlotStatus.Empty;
            LastUpdatedTime = DateTime.Now;
        }

        // --- 通用行为 ---
        public void UpdateStatus(SlotStatus newStatus)
        {
            Status = newStatus;
            LastUpdatedTime = DateTime.Now;
        }

        public void SetExtensionData(string json)
        {
            ExtensionDataJson = json;
            LastUpdatedTime = DateTime.Now;
        }
    }
}