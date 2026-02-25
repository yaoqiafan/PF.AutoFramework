using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Hardware
{
    public class MechanismUIInfo
    {
        public string MechanismId { get; set; }
        public string Title { get; set; }
        public string ViewName { get; set; } // Prism 导航使用的名称
        public int Order { get; set; }
    }
}
