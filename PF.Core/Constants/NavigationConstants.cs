using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    public static class NavigationConstants
    {
        public static class Dialogs
        {
            public const string CustomDialogBase = nameof(CustomDialogBase);

            public const string CommonChangeParamDialog = nameof(CommonChangeParamDialog);

            public const string LoginView = nameof(LoginView);

        }

        public static class Regions
        {
            public const string LoggingListRegion = nameof(LoggingListRegion);

            public const string SoftwareViewRegion = nameof(SoftwareViewRegion);

            public const string DebugViewRegion = nameof(DebugViewRegion);

            public const string MechanismContentRegion = nameof(MechanismContentRegion);

            /// <summary>工站调试模块右侧内容区域</summary>
            public const string StationContentRegion = nameof(StationContentRegion);
            public const string StationControllerContentRegion = nameof(StationControllerContentRegion);
        }

        public static class Views
        {
            #region 日志
            public const string LoggingListView = nameof(LoggingListView);

            public const string LogManagementView = nameof(LogManagementView);
            #endregion

            #region 参数管理
            public const string ParameterView = nameof(ParameterView);
            public const string ParameterView_SystemConfigParam = nameof(ParameterView_SystemConfigParam);
            public const string CommonParamView = nameof(CommonParamView);
            public const string ParameterView_HardwareParam = nameof(ParameterView_HardwareParam);
            public const string ParameterView_UserLoginParam = nameof(ParameterView_UserLoginParam);
            #endregion

            #region 登录
            public const string PagePermissionView = nameof(PagePermissionView);

            public const string UserManagementView = nameof(UserManagementView);
            #endregion

            #region 调试
            public const string HardwareDebugView = nameof(HardwareDebugView);
            public const string MechanismDebugView = nameof(MechanismDebugView);
            public const string AxisDebugView = nameof(AxisDebugView);
            public const string IODebugView = nameof(IODebugView);
            public const string BarcodeScanDebugView = nameof(BarcodeScanDebugView);
            public const string StationDebugView = nameof(StationDebugView);
           
            public const string CardDebugView = nameof(CardDebugView);
            public const string CameraDebugView = nameof(CameraDebugView);
         
            #endregion

            #region 主界面
            public const string MainView = nameof(MainView);

            public const string HomeView = nameof(HomeView);
            #endregion


            #region 生产数据
            public const string ProductionMonitorView = nameof(ProductionMonitorView);
            public const string ProductionHistoryView = nameof(ProductionHistoryView);
            #endregion

            #region SECS/GEM
            public const string SecsGemDebugView = nameof(SecsGemDebugView);
            #endregion


        }

    }
}
