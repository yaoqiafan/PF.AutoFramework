using PF.Core.Entities.ProductionData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 生产数据记录事件参数
    /// </summary>
    public class ProductionDataRecordedEventArgs : EventArgs
    {
        /// <summary>生产数据记录</summary>
        public ProductionRecord Record { get; set; } = null!;
    }
}
