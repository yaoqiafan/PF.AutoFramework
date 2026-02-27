using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Hardware
{
    /// <summary>
    /// 实体操作面板 IO 映射配置
    /// </summary>
    public class PanelIoConfig
    {
        /// <summary>
        /// 绑定实体面板所在的 IO 卡 DeviceId
        /// </summary>
        public string IoDeviceId { get; set; } = "MAIN_IO_CARD";

        /// <summary>启动按钮端口 (常开 NO)</summary>
        public int StartButtonPort { get; set; } = 0;

        /// <summary>暂停按钮端口 (常开 NO)</summary>
        public int PauseButtonPort { get; set; } = 1;

        /// <summary>复位按钮端口 (常开 NO)</summary>
        public int ResetButtonPort { get; set; } = 2;

        /// <summary>急停按钮端口 (常闭 NC)</summary>
        public int EStopButtonPort { get; set; } = 3;
    }
}
