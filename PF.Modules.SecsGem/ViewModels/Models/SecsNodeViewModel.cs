using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// SECS/GEM 报文树节点 ViewModel，映射 SecsGemNodeMessage。
    /// 支持无限嵌套（LIST 类型自动管理子节点长度）。
    /// </summary>
    public class SecsNodeViewModel : BindableBase
    {
        private DataType _dataType;
        private string _value;
        private int _length;
        private bool _isVariableNode;
        private uint _variableCode;
        private string _variableDescription;
        private bool _hasValidationError;
        private string _validationErrorMessage;
        private bool _isExpanded = true;

        /// <summary>节点添加请求事件</summary>
        public event EventHandler NodeAddRequested;
        /// <summary>
        /// 当 IsVariableNode = true 时触发，外部 ViewModel 订阅后弹出 VID 选择对话框
        /// </summary>
        public event EventHandler VidSelectionRequested;

        // 父节点引用，用于 RemoveNodeCommand
        private SecsNodeViewModel _parent;

        /// <summary>初始化实例</summary>
        public SecsNodeViewModel()
        {
            Children = new ObservableCollection<SecsNodeViewModel>();

            // 【修复 1】：在此处统一监听集合变化，自动维护长度和父子节点关系
            Children.CollectionChanged += (s, e) =>
            {
                if (_dataType == DataType.LIST)
                    RaisePropertyChanged(nameof(Length));

                // 自动维护父节点引用，确保外部手动 Add 进来的节点也能正常调用 Remove
                if (e.NewItems != null)
                {
                    foreach (SecsNodeViewModel child in e.NewItems)
                        child._parent = this;
                }
                if (e.OldItems != null)
                {
                    foreach (SecsNodeViewModel child in e.OldItems)
                        if (child._parent == this) child._parent = null;
                }
            };

            AddChildCommand = new DelegateCommand(ExecuteAddChild, () => IsListNode);
            RemoveNodeCommand = new DelegateCommand(ExecuteRemoveNode);
            SelectVariableCommand = new DelegateCommand(ExecuteSelectVariable);
        }

        // ──────────────────────────────────────────────
        // 核心属性
        // ──────────────────────────────────────────────

        /// <summary>获取或设置数据类型</summary>
        public DataType DataType
        {
            get => _dataType;
            set
            {
                if (SetProperty(ref _dataType, value))
                {
                    RaisePropertyChanged(nameof(ItemFormat));
                    RaisePropertyChanged(nameof(IsListNode));
                    RaisePropertyChanged(nameof(Length));
                    AddChildCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 显示用类型字符串，如 "L"、"A"、"U4"、"B"、"BOOL"
        /// </summary>
        public string ItemFormat => DataTypeToString(_dataType);

        /// <summary>
        /// 节点长度。
        /// LIST 节点：只读，等于 Children.Count；数据节点：用户可编辑。
        /// </summary>
        public int Length
        {
            get => _dataType == DataType.LIST ? Children.Count : _length;
            set
            {
                if (_dataType != DataType.LIST)
                    SetProperty(ref _length, value);
            }
        }

        /// <summary>
        /// 节点数据值（字符串形式）。写入时执行类型校验。
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                    ValidateValue(value);
            }
        }

        /// <summary>获取或设置是否为列表节点</summary>
        public bool IsListNode => _dataType == DataType.LIST;

        /// <summary>获取或设置是否展开</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // ──────────────────────────────────────────────
        // 变量绑定属性
        // ──────────────────────────────────────────────

        /// <summary>获取或设置是否为变量节点</summary>
        public bool IsVariableNode
        {
            get => _isVariableNode;
            set
            {
                if (SetProperty(ref _isVariableNode, value))
                {
                    if (value)
                    {
                        // 延迟触发 VID 选择，确保属性变更通知先完成
                        Application.Current?.Dispatcher.BeginInvoke(
                            new Action(() => SelectVariableCommand.Execute()));
                    }
                    else
                    {
                        VariableCode = 0;
                        VariableDescription = string.Empty;
                    }
                }
            }
        }

        /// <summary>获取或设置变量代码</summary>
        public uint VariableCode
        {
            get => _variableCode;
            set => SetProperty(ref _variableCode, value);
        }

        /// <summary>获取或设置变量描述</summary>
        public string VariableDescription
        {
            get => _variableDescription;
            set => SetProperty(ref _variableDescription, value);
        }

        // ──────────────────────────────────────────────
        // 校验属性
        // ──────────────────────────────────────────────

        /// <summary>获取或设置是否有验证错误</summary>
        public bool HasValidationError
        {
            get => _hasValidationError;
            set => SetProperty(ref _hasValidationError, value);
        }

        /// <summary>获取或设置验证错误消息</summary>
        public string ValidationErrorMessage
        {
            get => _validationErrorMessage;
            set => SetProperty(ref _validationErrorMessage, value);
        }

        /// <summary>获取子节点集合</summary>
        public ObservableCollection<SecsNodeViewModel> Children { get; }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        /// <summary>添加子节点命令</summary>
        public DelegateCommand AddChildCommand { get; }
        /// <summary>移除节点命令</summary>
        public DelegateCommand RemoveNodeCommand { get; }
        /// <summary>选择变量命令</summary>
        public DelegateCommand SelectVariableCommand { get; }

        // ──────────────────────────────────────────────
        // 工厂：SecsGemNodeMessage ↔ SecsNodeViewModel
        // ──────────────────────────────────────────────

        /// <summary>从节点消息创建视图模型</summary>
        public static SecsNodeViewModel FromNodeMessage(SecsGemNodeMessage node, SecsNodeViewModel parent = null)
        {
            if (node == null) return null;

            var vm = new SecsNodeViewModel
            {
                _parent = parent,
                _dataType = node.DataType,
                _length = node.Length,
                _isVariableNode = node.IsVariableNode,
                _variableCode = node.VariableCode,
            };

            // 设置显示值
            vm._value = NodeValueToString(node);

            if (node.IsVariableNode && node.VariableCode > 0)
                vm._variableDescription = $"{node.VariableCode}";

            // 递归子节点（LIST 类型）
            if (node.DataType == DataType.LIST && node.SubNode != null)
            {
                foreach (var sub in node.SubNode)
                {
                    var childVm = FromNodeMessage(sub, vm);
                    vm.Children.Add(childVm);
                }
            }

            return vm;
        }

        /// <summary>转换为节点消息</summary>
        public SecsGemNodeMessage ToNodeMessage()
        {
            if (_dataType == DataType.LIST)
            {
                var node = new SecsGemNodeMessage
                {
                    DataType = DataType.LIST,
                    Length = Children.Count,
                    SubNode = new List<SecsGemNodeMessage>(),
                    IsVariableNode = _isVariableNode,
                    VariableCode = _variableCode,
                };
                foreach (var child in Children)
                    node.SubNode.Add(child.ToNodeMessage());
                return node;
            }
            else
            {
                var typedValue = ParseTypedValue(_value, _dataType);
                var node = new SecsGemNodeMessage(_dataType, typedValue)
                {
                    Length = _length,
                    IsVariableNode = _isVariableNode,
                    VariableCode = _variableCode,
                };
                return node;
            }
        }

        // ──────────────────────────────────────────────
        // 命令实现
        // ──────────────────────────────────────────────

        private void ExecuteAddChild()
        {
            NodeAddRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>设置变量绑定</summary>
        public void SetVariableBinding(uint code, string description)
        {
            _isVariableNode = true;
            _variableCode = code;
            _variableDescription = description;
            RaisePropertyChanged(nameof(IsVariableNode));
            RaisePropertyChanged(nameof(VariableCode));
            RaisePropertyChanged(nameof(VariableDescription));
        }

        private void ExecuteRemoveNode()
        {
            _parent?.Children.Remove(this);
        }

        private void ExecuteSelectVariable()
        {
            VidSelectionRequested?.Invoke(this, EventArgs.Empty);
        }

        // ──────────────────────────────────────────────
        // 数据校验
        // ──────────────────────────────────────────────

        private void ValidateValue(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                HasValidationError = false;
                ValidationErrorMessage = string.Empty;
                return;
            }

            switch (_dataType)
            {
                case DataType.U1:
                    HasValidationError = !byte.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为 0~255 的无符号整数" : string.Empty;
                    break;
                case DataType.U2:
                    HasValidationError = !ushort.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为 0~65535 的无符号整数" : string.Empty;
                    break;
                case DataType.U4:
                    HasValidationError = !uint.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为 0~4294967295 的无符号整数" : string.Empty;
                    break;
                case DataType.U8:
                    HasValidationError = !ulong.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为无符号64位整数" : string.Empty;
                    break;
                case DataType.I1:
                    HasValidationError = !sbyte.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为 -128~127 的有符号整数" : string.Empty;
                    break;
                case DataType.I2:
                    HasValidationError = !short.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为有符号16位整数" : string.Empty;
                    break;
                case DataType.I4:
                    HasValidationError = !int.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为有符号32位整数" : string.Empty;
                    break;
                case DataType.I8:
                    HasValidationError = !long.TryParse(val, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为有符号64位整数" : string.Empty;
                    break;
                case DataType.F4:
                    HasValidationError = !float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为单精度浮点数 (如 3.14)" : string.Empty;
                    break;
                case DataType.F8:
                    HasValidationError = !double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为双精度浮点数" : string.Empty;
                    break;
                case DataType.Binary:
                    HasValidationError = !IsValidHexString(val);
                    ValidationErrorMessage = HasValidationError ? "格式: 以空格分隔的十六进制字节串 (如 0A 1B FF)" : string.Empty;
                    break;
                case DataType.Boolean:
                    HasValidationError = !IsValidBoolean(val);
                    ValidationErrorMessage = HasValidationError ? "值必须为 true/false 或 1/0" : string.Empty;
                    break;
                case DataType.ASCII:
                case DataType.JIS8:
                    HasValidationError = false;
                    Length = val.Length;
                    if (_length > 0 && val.Length != _length)
                    {
                        ValidationErrorMessage = $"⚠ 字符串长度 {val.Length} 与声明长度 {_length} 不符";
                    }
                    else
                    {
                        ValidationErrorMessage = string.Empty;
                    }
                    // 更新长度放在判断之后
                    
                    break;
                default:
                    HasValidationError = false;
                    ValidationErrorMessage = string.Empty;
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // 辅助方法
        // ──────────────────────────────────────────────

        private static string DataTypeToString(DataType dt) => dt switch
        {
            DataType.LIST => "L",
            DataType.ASCII => "A",
            DataType.Binary => "B",
            DataType.Boolean => "BOOL",
            DataType.JIS8 => "J",
            DataType.CHARACTER_2 => "C2",
            DataType.I1 => "I1",
            DataType.I2 => "I2",
            DataType.I4 => "I4",
            DataType.I8 => "I8",
            DataType.U1 => "U1",
            DataType.U2 => "U2",
            DataType.U4 => "U4",
            DataType.U8 => "U8",
            DataType.F4 => "F4",
            DataType.F8 => "F8",
            _ => dt.ToString()
        };

        private static string NodeValueToString(SecsGemNodeMessage node)
        {
            if (node.DataType == DataType.LIST) return string.Empty;

            if (node.TypedValue != null)
                return node.TypedValue.ToString();

            if (node.Data != null && node.Data.Length > 0)
            {
                if (node.DataType == DataType.ASCII)
                    return Encoding.ASCII.GetString(node.Data);
                if (node.DataType == DataType.Binary)
                    return BitConverter.ToString(node.Data).Replace("-", " ");
                return BitConverter.ToString(node.Data).Replace("-", " ");
            }

            return string.Empty;
        }

        private static object ParseTypedValue(string value, DataType dt)
        {
            if (string.IsNullOrEmpty(value)) return null;
            try
            {
                return dt switch
                {
                    DataType.U1 => byte.Parse(value),
                    DataType.U2 => ushort.Parse(value),
                    DataType.U4 => uint.Parse(value),
                    DataType.U8 => ulong.Parse(value),
                    DataType.I1 => sbyte.Parse(value),
                    DataType.I2 => short.Parse(value),
                    DataType.I4 => int.Parse(value),
                    DataType.I8 => long.Parse(value),
                    // 补充了 NumberStyles.Float 保证与校验逻辑一致
                    DataType.F4 => float.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
                    DataType.F8 => double.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
                    DataType.Boolean => value is "1" or "true" or "True",
                    DataType.ASCII => value,
                    DataType.JIS8 => value,
                    DataType.Binary => HexStringToBytes(value),
                    _ => value
                };
            }
            catch
            {
                return value;
            }
        }

        private static bool IsValidHexString(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return true;
            return Regex.IsMatch(val.Trim(), @"^([0-9A-Fa-f]{2})(\s[0-9A-Fa-f]{2})*$");
        }

        private static bool IsValidBoolean(string val)
        {
            return val is "0" or "1" or "true" or "false" or "True" or "False";
        }

        private static byte[] HexStringToBytes(string hex)
        {
            // 【修复 3】：加入 RemoveEmptyEntries 防止因为多输了空格导致崩溃
            var parts = hex.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        }
    }
}