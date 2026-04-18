using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    /// <summary>
    /// 导航常量定义
    /// </summary>
    public static class NavigationConstants
    {
        /// <summary>
        /// 对话框导航常量
        /// </summary>
        public static class Dialogs
        {
            /// <summary>自定义对话框基类</summary>
            public const string CustomDialogBase = nameof(CustomDialogBase);

            /// <summary>通用参数修改对话框</summary>
            public const string CommonChangeParamDialog = nameof(CommonChangeParamDialog);

            /// <summary>登录视图</summary>
            public const string LoginView = nameof(LoginView);

        }

        /// <summary>
        /// 区域导航常量
        /// </summary>
        public static class Regions
        {
            /// <summary>日志列表区域</summary>
            public const string LoggingListRegion = nameof(LoggingListRegion);

            /// <summary>软件视图区域</summary>
            public const string SoftwareViewRegion = nameof(SoftwareViewRegion);

            /// <summary>调试视图区域</summary>
            public const string DebugViewRegion = nameof(DebugViewRegion);

            /// <summary>模组内容区域</summary>
            public const string MechanismContentRegion = nameof(MechanismContentRegion);

            /// <summary>工站调试模块右侧内容区域</summary>
            public const string StationContentRegion = nameof(StationContentRegion);
            /// <summary>工站控制器内容区域</summary>
            public const string StationControllerContentRegion = nameof(StationControllerContentRegion);
        }

        /// <summary>
        /// 视图导航常量
        /// </summary>
        public static class Views
        {
            #region 日志
            /// <summary>日志列表视图</summary>
            public const string LoggingListView = nameof(LoggingListView);

            /// <summary>日志管理视图</summary>
            public const string LogManagementView = nameof(LogManagementView);
            #endregion

            #region 参数管理
            /// <summary>参数视图</summary>
            public const string ParameterView = nameof(ParameterView);
            /// <summary>系统配置参数视图</summary>
            public const string ParameterView_SystemConfigParam = nameof(ParameterView_SystemConfigParam);
            /// <summary>通用参数视图</summary>
            public const string CommonParamView = nameof(CommonParamView);
            /// <summary>硬件参数视图</summary>
            public const string ParameterView_HardwareParam = nameof(ParameterView_HardwareParam);
            /// <summary>用户登录参数视图</summary>
            public const string ParameterView_UserLoginParam = nameof(ParameterView_UserLoginParam);
            #endregion

            #region 登录
            /// <summary>页面权限视图</summary>
            public const string PagePermissionView = nameof(PagePermissionView);

            /// <summary>用户管理视图</summary>
            public const string UserManagementView = nameof(UserManagementView);
            #endregion

            #region 调试
            /// <summary>SecsGem调试视图</summary>
            public const string SecsGemDebugView = nameof(SecsGemDebugView);
            /// <summary>硬件调试视图</summary>
            public const string HardwareDebugView = nameof(HardwareDebugView);
            /// <summary>模组调试视图</summary>
            public const string MechanismDebugView = nameof(MechanismDebugView);
            /// <summary>轴调试视图</summary>
            public const string AxisDebugView = nameof(AxisDebugView);
            /// <summary>IO调试视图</summary>
            public const string IODebugView = nameof(IODebugView);
            /// <summary>扫码枪调试视图</summary>
            public const string BarcodeScanDebugView = nameof(BarcodeScanDebugView);
            /// <summary>工站调试视图</summary>
            public const string StationDebugView = nameof(StationDebugView);

            /// <summary>运动控制卡调试视图</summary>
            public const string CardDebugView = nameof(CardDebugView);
            /// <summary>相机调试视图</summary>
            public const string CameraDebugView = nameof(CameraDebugView);
            /// <summary>光源控制器调试视图</summary>
            public const string LightControllerDebugView = nameof(LightControllerDebugView);

            #endregion

            #region 主界面
            /// <summary>主视图</summary>
            public const string MainView = nameof(MainView);

            /// <summary>首页视图</summary>
            public const string HomeView = nameof(HomeView);
            #endregion


            #region 生产数据
            /// <summary>生产监控视图</summary>
            public const string ProductionMonitorView = nameof(ProductionMonitorView);
            /// <summary>生产历史视图</summary>
            public const string ProductionHistoryView = nameof(ProductionHistoryView);
            #endregion


            #region 报警中心
            /// <summary>报警中心视图</summary>
            public const string AlarmCenterView = nameof(AlarmCenterView);
            #endregion
        }

    }
}
