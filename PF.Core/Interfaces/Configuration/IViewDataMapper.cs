using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Configuration
{
    /// <summary>
    /// 视图数据映射器接口
    /// </summary>
    public interface IViewDataMapper
    {
        /// <summary>
        /// 将数据映射到视图
        /// </summary>
        /// <param name="viewInstance">视图实例</param>
        /// <param name="data">数据对象</param>
        /// <returns>是否映射成功</returns>
        bool MapToView(object viewInstance, object data);

        /// <summary>
        /// 从视图获取数据
        /// </summary>
        /// <param name="viewInstance">视图实例</param>
        /// <returns>数据对象</returns>
        object MapFromView(object viewInstance);
    }
}
