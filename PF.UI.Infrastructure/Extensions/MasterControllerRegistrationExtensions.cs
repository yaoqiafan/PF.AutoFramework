using PF.Core.Attributes;
using PF.Core.Constants;
using Prism.Ioc;
using System;
using System.Linq;
using System.Reflection;

namespace PF.UI.Infrastructure.Extensions
{
    public static class MasterControllerRegistrationExtensions
    {
        private static bool _hasRegisteredMasterController = false;

        /// <summary>
        /// 扫描并注册唯一的主控界面 (基于字符串名称查找视图)
        /// </summary>
        /// <param name="containerRegistry">Prism 容器</param>
        /// <param name="controllerAssembly">主控类所在的程序集 (逻辑层)</param>
        /// <param name="viewAssembly">主控视图所在的程序集 (UI层)</param>
        /// <param name="baseControllerType">主控基类的 Type</param>
        public static void RegisterMasterControllerView(
            this IContainerRegistry containerRegistry,
            Assembly controllerAssembly,
            Assembly viewAssembly,
            Type baseControllerType)
        {
            // 1. 查找主控类
            var controllerTypes = controllerAssembly.GetTypes()
                .Where(t => baseControllerType.IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            if (controllerTypes.Count == 0) return;

            // 2. 唯一性防呆
            if (controllerTypes.Count > 1)
                throw new InvalidOperationException($"架构违规：系统中只允许实现一个 {baseControllerType.Name}！");

            if (_hasRegisteredMasterController)
                throw new InvalidOperationException("架构违规：MasterControllerView 全局仅允许存在一个主控实例！");

            // 3. 获取特性信息
            var masterType = controllerTypes.Single();
            var uiAttribute = masterType.GetCustomAttribute<MasterControllerUIAttribute>();

            if (uiAttribute == null || string.IsNullOrWhiteSpace(uiAttribute.ViewName))
                throw new InvalidOperationException($"类 {masterType.Name} 未正确标记 [MasterControllerUI] 特性！");

            // 4. ★ 核心修改：在 UI 程序集中，通过字符串名称寻找对应的 View Type
            var viewType = viewAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == uiAttribute.ViewName);

            // 5. 查找对应的 ViewModel Type (基于约定：ViewName + "ViewModel")
            string expectedViewModelName = uiAttribute.ViewModelName ;
            var viewModelType = viewAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == expectedViewModelName);

            // 6. 注册导航与绑定
            if (viewModelType != null)
            {
                // ① 正常注册 View 导航
                containerRegistry.RegisterForNavigation(viewType, NavigationConstants.Views.MasterControllerView);

                // ② 显式告知 Prism：这个 View 对应的 ViewModel 就是它 (需要 using Prism.Mvvm;)
                Prism.Mvvm.ViewModelLocationProvider.Register(viewType.ToString(), viewModelType);
            }
            else
            {
                // 如果没找到 ViewModel，就只注册 View（降级方案）
                containerRegistry.RegisterForNavigation(viewType, NavigationConstants.Views.MasterControllerView);
            }

            _hasRegisteredMasterController = true;
        }
    }

}