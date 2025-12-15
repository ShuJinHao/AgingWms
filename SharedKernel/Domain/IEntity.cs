using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Domain
{
    // 1. 实体基础接口
    public interface IEntity
    {
        // 假设你用 object 或 int/string 做主键，这里泛型化或统一
        object Id { get; }
    }
}