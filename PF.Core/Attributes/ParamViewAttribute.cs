using PF.Core.Interfaces.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 参数视图特性
    /// 标记在自定义类型上，指定该类型对应的视图类型和映射器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
    public class ParamViewAttribute : Attribute
    {
        /// <summary>
        /// 视图类型（必须有无参构造函数）
        /// </summary>
        public Type ViewType { get; }

        /// <summary>
        /// 映射器类型（必须实现 IViewDataMapper 接口）
        /// </summary>
        public Type MapperType { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="viewType">视图类型</param>
        /// <param name="mapperType">映射器类型</param>
        public ParamViewAttribute(Type viewType, Type mapperType)
        {
            ViewType = viewType;
            MapperType = mapperType;

            // 验证参数
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            if (mapperType == null)
                throw new ArgumentNullException(nameof(mapperType));

            // 验证映射器类型
            if (!typeof(IViewDataMapper).IsAssignableFrom(mapperType))
                throw new ArgumentException($"映射器类型 {mapperType.Name} 必须实现 IViewDataMapper 接口");
        }
    }
}
