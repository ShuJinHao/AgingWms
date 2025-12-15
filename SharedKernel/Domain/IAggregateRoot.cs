using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Domain
{
    // 2. 聚合根标记接口 (只有聚合根才能被 Repository 直接操作)
    public interface IAggregateRoot : IEntity { }
}