using Prism.Mvvm;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// Modbus 调试面板"写多个线圈/写多个寄存器"测试时，单个元素位置对应的可编辑值。
    /// 集合按当前 Quantity 自动生成/裁剪（见 ModbusRtuDebugViewModel/ModbusTcpDebugViewModel.SyncMultiWriteValues），
    /// 每个位置允许用户单独填值，不再是"全部写同一个值"。
    /// </summary>
    public class ModbusMultiWriteValueItem : BindableBase
    {
        /// <summary>相对起始地址的偏移（第几个元素，从 0 开始）</summary>
        public int Index { get; }

        private string _value = "0";
        /// <summary>该位置填写的值：写线圈时非 "0" 视为 ON，写寄存器时按 ushort 解析，解析失败按 0 处理</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>初始化</summary>
        public ModbusMultiWriteValueItem(int index)
        {
            Index = index;
        }
    }
}
