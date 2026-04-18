using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Configuration
{
    /// <summary>
    /// 参数信息
    /// </summary>
    public class ParamInfo
    {
        /// <summary>参数ID</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>参数名称</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>参数描述</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>类型名称</summary>
        public string TypeName { get; set; } = string.Empty;
        /// <summary>参数值</summary>
        public Object Value { get; set; } = null;
        /// <summary>参数分类</summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>更新时间</summary>
        public DateTime UpdateTime { get; set; }
    }
}
