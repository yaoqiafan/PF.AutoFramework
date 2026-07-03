using PF.Core.Enums;
using PF.Core.Interfaces.Communication;
using PF.UI.Controls;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Parameter.Dialog.Editors
{
    /// <summary>
    /// TCP 客户端实例选择编辑器：下拉列出当前所有已启用的 Tcp/Client 通讯实例 InstanceId，
    /// 供 HKBarcodeScan/KeyenceIntelligentCamera 等硬件参数视图选择要绑定的通讯实例。
    /// </summary>
    public class TcpClientInstancePropertyEditor : PropertyEditorBase
    {
        /// <summary>构建下拉选择控件，选项为当前所有 Tcp/Client 通讯实例的 InstanceId</summary>
        public override FrameworkElement CreateElement(PropertyItem propertyItem)
        {
            var commManager = ContainerLocator.Container.Resolve<ICommunicationManagerService>();

            var instanceIds = commManager.ActiveCommunications
                .Where(c => c.Category == CommunicationCategory.Tcp && c.Role == CommunicationRole.Client)
                .Select(c => c.InstanceId)
                .ToList();

            return new SearchComboBox
            {
                IsEnabled = !propertyItem.IsReadOnly,
                ItemsSource = instanceIds
            };
        }

        /// <summary>与属性双向绑定的依赖属性：选中项</summary>
        public override DependencyProperty GetDependencyProperty() => ListBox.SelectedItemProperty;
    }
}
