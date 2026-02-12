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

        }

        public static class Regions
        {
            public const string LoggingListRegion = nameof(LoggingListRegion);

            public const string SoftwareViewRegion = nameof(SoftwareViewRegion);

        }

        public static class Views
        {
            #region 日志
            public const string LoggingListView = nameof(LoggingListView);

            public const string LogManagementView = nameof(LogManagementView);
            #endregion
            #region 参数管理
            public const string ParameterView = nameof(ParameterView);
            #endregion



        }

    }
}
