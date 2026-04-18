
using PF.UI.Shared.Data;
using PF.UI.Shared.Tools;
using System.Windows;
using System.Windows.Controls;


namespace PF.UI.Resources;

/// <summary>
/// StyleSelector 选择器
/// </summary>
public class ComboBoxItemCapsuleStyleSelector : StyleSelector
{
    /// <summary>
    /// 选择模板
    /// </summary>
    public override Style SelectStyle(object item, DependencyObject container)
    {
        if (container is ComboBoxItem comboBoxItem && VisualHelper.GetParent<ComboBox>(comboBoxItem) is { } comboBox)
        {
            var count = comboBox.Items.Count;
            if (count == 1)
            {
                return ResourceHelper.GetResourceInternal<Style>(ResourceToken.ComboBoxItemCapsuleSingle);
            }

            var index = comboBox.ItemContainerGenerator.IndexFromContainer(comboBoxItem);
            return index == 0
                ? ResourceHelper.GetResourceInternal<Style>(ResourceToken.ComboBoxItemCapsuleHorizontalFirst)
                : ResourceHelper.GetResourceInternal<Style>(index == count - 1
                    ? ResourceToken.ComboBoxItemCapsuleHorizontalLast
                    : ResourceToken.ComboBoxItemCapsuleDefault);
        }

        return null;
    }
}
