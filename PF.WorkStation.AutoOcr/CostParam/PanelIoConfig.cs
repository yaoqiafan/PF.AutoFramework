using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware;
using PF.Workstation.AutoOcr.CostParam;
using System.Collections.Generic;

namespace PF.WorkStation.AutoOcr.CostParam
{
    /// <summary>
    /// 单个硬件输入点的配置实现。
    /// </summary>
    public class HardwareInputConfig : IHardwareInputConfig
    {
        /// <summary>获取输入类型</summary>
        public string InputType { get; }
        /// <summary>获取端口号</summary>
        public int Port { get; }
        /// <summary>获取防抖时间(ms)</summary>
        public int DebounceMs { get; }
        /// <summary>获取输入名称</summary>
        public string Name { get; }
        /// <summary>获取扫描分组</summary>
        public InputScanGroup ScanGroup { get; }
        /// <summary>获取或设置是否静默</summary>
        public bool IsMuted { get; set; } = false;

        /// <summary>
        /// 初始化硬件输入配置
        /// </summary>
        public HardwareInputConfig(
            string inputType,
            int port,
            int debounceMs,
            string name,
            InputScanGroup scanGroup)
        {
            InputType  = inputType;
            Port       = port;
            DebounceMs = debounceMs;
            Name       = name;
            ScanGroup  = scanGroup;
        }
    }

    /// <summary>
    /// AutoOcr 工站实体面板的 IO 监控配置。
    ///
    /// Standard 组（普通按键，DebounceMs = 20）：Start / Pause / Reset
    /// Safety   组（安全传感器，DebounceMs = 0）：EStop / 安全门1 / 安全门2
    /// </summary>
    public class PanelIoConfig : IPanelIoConfig
    {
        /// <summary>获取IO设备ID</summary>
        public string IoDeviceId { get; } = "IO_Collectorll";

        /// <summary>获取监控的输入列表</summary>
        public IEnumerable<IHardwareInputConfig> MonitoredInputs { get; } =
            new List<IHardwareInputConfig>
            {
                // ── 普通按键（Standard 组，20ms 防抖）────────────────────────
                new HardwareInputConfig(HardwareInputTypeExtension.WorkStation1Start, port: (int)E_InPutName.上晶圆左启动按钮, debounceMs: 20,
                    name: "工位1启动按钮", InputScanGroup.Standard),

                new HardwareInputConfig(HardwareInputTypeExtension.WorkStation2Start, port: (int)E_InPutName.上晶圆右启动按钮, debounceMs: 20,
                    name: "工位2启动按钮", InputScanGroup.Standard),


             

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: (int)E_InPutName.电磁门锁1_2信号, debounceMs: 0,
                    name: nameof(E_InPutName.电磁门锁1_2信号), InputScanGroup.Safety),

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: (int)E_InPutName.电磁门锁3_4信号, debounceMs: 0,
                    name: nameof(E_InPutName.电磁门锁3_4信号), InputScanGroup.Safety),

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: (int)E_InPutName.电磁门锁5_6信号, debounceMs: 0,
                    name: nameof(E_InPutName.电磁门锁5_6信号), InputScanGroup.Safety),

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: (int)E_InPutName.电磁门锁7_8信号, debounceMs: 0,
                    name: nameof(E_InPutName.电磁门锁7_8信号), InputScanGroup.Safety),
            };
    }
}
