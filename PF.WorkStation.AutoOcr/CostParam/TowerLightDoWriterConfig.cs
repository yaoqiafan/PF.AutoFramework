using PF.Core.Interfaces.TowerLight;
using System.Collections.Generic;

namespace PF.Workstation.AutoOcr.CostParam
{
    /// <summary>
    /// 三色灯 DO 点映射配置实现。
    /// 将逻辑 tag（Red/Yellow/Green/Buzzer）映射到物理 IO 板卡端口索引。
    /// 与 PanelIoConfig 保持一致的配置风格：硬编码映射 + E_OutPutName 枚举。
    /// </summary>
    public class TowerLightDoWriterConfig : ITowerLightDoWriterConfig
    {
        /// <summary>绑定的 IO 板卡 DeviceId（与 PanelIoConfig 一致，使用同一块板卡）</summary>
        public string IoDeviceId => "IO_Collectorll";

        /// <summary>
        /// Tag → 端口映射表（大小写不敏感）。
        /// 复用现有 E_OutPutName 枚举定义（三色灯红=32, 黄=33, 绿=34, 蜂鸣器=35）。
        /// </summary>
        private readonly Dictionary<string, int> _tagToPort = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Red"]    = (int)E_OutPutName.三色灯红,
            ["Yellow"] = (int)E_OutPutName.三色灯黄,
            ["Green"]  = (int)E_OutPutName.三色灯绿,
            ["Buzzer"] = (int)E_OutPutName.蜂鸣器,
        };

        /// <summary>
        /// 通过逻辑 tag 获取对应的端口索引。
        /// tag 未配置时返回 -1，由 TowerLightDoWriter 拦截并记录警告日志。
        /// </summary>
        public int GetPort(string tag)
            => _tagToPort.TryGetValue(tag, out var port) ? port : -1;
    }
}
