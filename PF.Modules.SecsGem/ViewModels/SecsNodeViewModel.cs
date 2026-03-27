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

        // 父节点引用，用于 RemoveNodeCommand
        private SecsNodeViewModel _parent;

        public SecsNodeViewModel()
        {
            Children = new ObservableCollection<SecsNodeViewModel>();
            Children.CollectionChanged += (s, e) =>
            {
                if (_dataType == DataType.LIST)
                    RaisePropertyChanged(nameof(Length));
            };

            AddChildCommand = new DelegateCommand(ExecuteAddChild, () => IsListNode);
            RemoveNodeCommand = new DelegateCommand(ExecuteRemoveNode);
            SelectVariableCommand = new DelegateCommand(ExecuteSelectVariable);
        }

        // ──────────────────────────────────────────────
        // 核心属性
        // ──────────────────────────────────────────────

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

        public bool IsListNode => _dataType == DataType.LIST;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // ──────────────────────────────────────────────
        // 变量绑定属性
        // ──────────────────────────────────────────────

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

        public uint VariableCode
        {
            get => _variableCode;
            set => SetProperty(ref _variableCode, value);
        }

        public string VariableDescription
        {
            get => _variableDescription;
            set => SetProperty(ref _variableDescription, value);
        }

        // ──────────────────────────────────────────────
        // 校验属性
        // ──────────────────────────────────────────────

        public bool HasValidationError
        {
            get => _hasValidationError;
            set => SetProperty(ref _hasValidationError, value);
        }

        public string ValidationErrorMessage
        {
            get => _validationErrorMessage;
            set => SetProperty(ref _validationErrorMessage, value);
        }

        // ──────────────────────────────────────────────
        // 子节点集合
        // ──────────────────────────────────────────────

        public ObservableCollection<SecsNodeViewModel> Children { get; }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        public DelegateCommand AddChildCommand { get; }
        public DelegateCommand RemoveNodeCommand { get; }
        public DelegateCommand SelectVariableCommand { get; }

        // ──────────────────────────────────────────────
        // 工厂：SecsGemNodeMessage → SecsNodeViewModel
        // ──────────────────────────────────────────────

        /// <summary>
        /// 递归将 SecsGemNodeMessage 转换为 ViewModel 树
        /// </summary>
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
                vm._variableDescription = $"VID:{node.VariableCode}";

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

        /// <summary>
        /// 递归将 ViewModel 树序列化回 SecsGemNodeMessage
        /// </summary>
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
            // 添加一个默认 ASCII 子节点，用户可以改类型
            var child = new SecsNodeViewModel
            {
                _parent = this,
                _dataType = DataType.ASCII,
                _length = 0,
                _value = string.Empty,
            };
            child.AddChildCommand.RaiseCanExecuteChanged();
            Children.Add(child);
        }

        private void ExecuteRemoveNode()
        {
            _parent?.Children.Remove(this);
        }

        private void ExecuteSelectVariable()
        {
            // VID 选择逻辑由外部（ViewModel/对话框服务）注入回调处理。
            // 这里触发一个外部可订阅的事件，具体弹窗由 SecsGemDebugViewModel 负责。
            VidSelectionRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 当 IsVariableNode = true 时触发，外部 ViewModel 订阅后弹出 VID 选择对话框
        /// </summary>
        public event EventHandler VidSelectionRequested;

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
                    HasValidationError = !float.TryParse(val, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为单精度浮点数 (如 3.14)" : string.Empty;
                    break;
                case DataType.F8:
                    HasValidationError = !double.TryParse(val, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _);
                    ValidationErrorMessage = HasValidationError ? "值必须为双精度浮点数" : string.Empty;
                    break;
                case DataType.Binary:
                    HasValidationError = !IsValidHexString(val);
                    ValidationErrorMessage = HasValidationError
                        ? "格式: 以空格分隔的十六进制字节串 (如 0A 1B FF)"
                        : string.Empty;
                    break;
                case DataType.Boolean:
                    HasValidationError = !IsValidBoolean(val);
                    ValidationErrorMessage = HasValidationError
                        ? "值必须为 true/false 或 1/0"
                        : string.Empty;
                    break;
                case DataType.ASCII:
                case DataType.JIS8:
                    HasValidationError = false;
                    if (_length > 0 && val.Length != _length)
                        ValidationErrorMessage = $"⚠ 字符串长度 {val.Length} 与声明长度 {_length} 不符";
                    else
                        ValidationErrorMessage = string.Empty;
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
                    DataType.F4 => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                    DataType.F8 => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
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
            var parts = hex.Trim().Split(' ');
            return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        }
    }
}
