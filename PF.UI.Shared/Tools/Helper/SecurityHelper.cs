using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Permissions;

namespace PF.UI.Shared.Tools;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SecurityHelper
{
    private static UIPermission _allWindowsUIPermission;

    [SecurityCritical]
    public static void DemandUIWindowPermission()
    {
        _allWindowsUIPermission ??= new UIPermission(UIPermissionWindow.AllWindows);
        _allWindowsUIPermission.Demand();
    }
}
