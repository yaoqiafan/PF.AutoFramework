using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Data
{
    /// <summary>
    /// 实体接口
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 数据库ID
        /// </summary>
        string ID { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        DateTime CreateTime { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        DateTime UpdateTime { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        string? Remarks { get; set; }

    }
}
