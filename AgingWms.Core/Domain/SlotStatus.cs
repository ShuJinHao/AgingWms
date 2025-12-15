using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Core.Domain
{
    public enum SlotStatus
    {
        Empty = 0,
        Occupied = 1,
        Running = 2, // 【建议新增】正常运行中
        Paused = 3,  // 【新增】暂停中
        Error = 99
    }
}