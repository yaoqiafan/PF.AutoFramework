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
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public Object Value { get; set; } = null;
        public string Category { get; set; } = string.Empty;
        public DateTime UpdateTime { get; set; }
    }
}
