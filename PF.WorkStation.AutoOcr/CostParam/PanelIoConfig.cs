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
        public string InputType { get; }
        public int Port { get; }
        public int DebounceMs { get; }
        public string Name { get; }
        public InputScanGroup ScanGroup { get; }
        public bool IsMuted { get; set; } = false;

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
        public string IoDeviceId { get; } = "IO_Collectorll";

        public IEnumerable<IHardwareInputConfig> MonitoredInputs { get; } =
            new List<IHardwareInputConfig>
            {
                // ── 普通按键（Standard 组，20ms 防抖）────────────────────────
                new HardwareInputConfig(HardwareInputType.Start, port: (int)E_InPutName.上晶圆左启动按钮, debounceMs: 20,
                    name: "工位1启动按钮", InputScanGroup.Standard),

                new HardwareInputConfig(HardwareInputType.Pause, port: (int)E_InPutName.上晶圆右启动按钮, debounceMs: 20,
                    name: "暂停按钮", InputScanGroup.Standard),


                // ── 安全传感器（Safety 组，零延迟）───────────────────────────
                new HardwareInputConfig(HardwareInputType.EStop, port: 3, debounceMs: 0,
                    name: "急停按钮（NC）", InputScanGroup.Safety),

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: 4, debounceMs: 0,
                    name: "安全门1（NC）", InputScanGroup.Safety),

                new HardwareInputConfig(HardwareInputType.SafeDoor, port: 5, debounceMs: 0,
                    name: "安全门2（NC）", InputScanGroup.Safety),
            };
    }
}
