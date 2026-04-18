using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Navigation
{
    /// <summary>
    /// MechanismUIModel 模型
    /// </summary>
    public class MechanismUIModel
    {
        /// <summary>
        /// Title
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// ViewName 视图
        /// </summary>
        public string ViewName { get; set; }
        /// <summary>
        /// Order
        /// </summary>
        public int Order { get; set; }
    }
}
