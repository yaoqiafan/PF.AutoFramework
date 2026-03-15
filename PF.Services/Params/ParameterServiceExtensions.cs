using PF.Core.Configuration;
using PF.Core.Interfaces.Configuration;
using PF.Data.Entity.Category.Basic;
using Prism.Ioc;

namespace PF.Services.Params
{
    /// <summary>
    /// 参数服务 DI 注册扩展方法
    /// </summary>
    public static class ParameterServiceExtensions
    {
        /// <summary>
        /// 注册参数服务和公共设置到 DI 容器。
        /// DbContext 和 IParamRepository&lt;&gt; 的注册由调用方处理（需要具体 DbContext 类型和 DryIoc 专属 API）。
        /// </summary>
        /// <param name="containerRegistry">DI 容器注册器</param>
        /// <param name="defaultParam">默认参数提供者，若为 null 则跳过 IDefaultParam 注册</param>
        public static IContainerRegistry AddParameterServices(
            this IContainerRegistry containerRegistry,
            IDefaultParam? defaultParam = null)
        {
            containerRegistry.RegisterSingleton<IParamService, ParamService>();

            if (defaultParam != null)
                containerRegistry.RegisterInstance<IDefaultParam>(defaultParam);

            containerRegistry.RegisterSingleton<CommonSettings>();

            return containerRegistry;
        }
    }
}
