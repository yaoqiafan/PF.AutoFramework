using PF.Core.Interfaces.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;

namespace PF.Services.CustomWorkstation.Hardware
{
    /// <summary>
    /// 【硬件层示例】模拟真空吸盘IO控制卡
    ///
    /// 继承链：SimVacuumIO → BaseDevice → IHardwareDevice
    ///                                  → IIOController
    ///
    /// 端口规划（8输入 / 8输出）：
    ///   Output[0] = 真空阀控制  （true=开阀，false=关阀）
    ///   Input[0]  = 真空检测传感器（true=有料/真空建立，false=无料）
    ///
    /// 模拟物理延迟：
    ///   开阀后约 300ms 传感器才反映真空建立（吸附延迟）
    ///   关阀后约 300ms 传感器才反映真空消失（释放延迟）
    ///
    /// 实际项目中替换为 DIO 板卡 SDK（如 Advantech, NI DAQ 等）调用即可。
    /// </summary>
    public class SimVacuumIO : BaseDevice, IIOController
    {
        private readonly bool[] _outputs = new bool[8];
        private readonly bool[] _inputs  = new bool[8];

        public int InputCount  => _inputs.Length;
        public int OutputCount => _outputs.Length;

        public SimVacuumIO(ILogService logger)
            : base("SIM_VACUUM_IO", "模拟真空IO卡", isSimulated: true, logger) { }

        // ── BaseDevice 钩子实现 ────────────────────────────────────────────
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
            => Task.FromResult(true);

        protected override Task InternalDisconnectAsync()
            => Task.CompletedTask;

        protected override Task InternalResetAsync(CancellationToken token)
        {
            // 复位：关闭所有输出，清除所有输入缓存
            Array.Clear(_outputs, 0, _outputs.Length);
            Array.Clear(_inputs,  0, _inputs.Length);
            return Task.CompletedTask;
        }

        // ── IIOController 实现 ────────────────────────────────────────────

        public bool ReadInput(int portIndex)  => _inputs[portIndex];
        public bool ReadOutput(int portIndex) => _outputs[portIndex];

        /// <summary>
        /// 写输出端口，并模拟物理反馈延迟（300ms 后 Input[0] 跟随变化）
        /// </summary>
        public void WriteOutput(int portIndex, bool value)
        {
            _outputs[portIndex] = value;
            _logger.Info($"[{DeviceName}] OUT[{portIndex}] → {(value ? "ON ↑" : "OFF ↓")}");

            // 模拟真空阀动作后传感器的物理响应延迟
            if (portIndex == 0)
            {
                _ = Task.Delay(300).ContinueWith(_ => _inputs[0] = value);
            }
        }

        /// <summary>
        /// 轮询等待输入端口达到目标状态（内置超时防卡死）
        /// 每 50ms 采样一次，直到目标状态或超时
        /// </summary>
        public async Task<bool> WaitInputAsync(int portIndex, bool targetState,
            int timeoutMs = 5000, CancellationToken token = default)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested(); // 支持急停打断
                if (_inputs[portIndex] == targetState) return true;
                await Task.Delay(50, token);
            }
            _logger.Warn($"[{DeviceName}] WaitInput IN[{portIndex}]=={targetState} 超时 ({timeoutMs}ms)");
            return false;
        }
    }
}
