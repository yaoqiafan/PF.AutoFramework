using Prism.Mvvm;

namespace PF.Modules.Halcon.Models;

/// <summary>
/// 视觉过程单个输入参数的 UI 绑定模型（动态行）。
/// 对应 .hdev 过程的 INPUT_* 控制变量。
/// </summary>
public class VisionParameterItem : BindableBase
{
    /// <summary>参数名（如 INPUT_THRESHOLD）</summary>
    public string Name { get; init; } = string.Empty;

    private string _value = string.Empty;
    /// <summary>参数值（字符串形式，执行时解析为对应 Halcon 类型）</summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
