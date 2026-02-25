using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;

namespace PF.Workstation.Demo.Hardware
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
        // 问题：_inputs 由后台 Task（WriteOutput 的 ContinueWith）写入，同时被
        //       WaitInputAsync 所在的工站线程读取，存在无锁并发访问（数据竞争）。
        // 修复：使用 Volatile.Read/Write 保证内存可见性（对 bool 读写本已原子，
        //       但 volatile 语义确保跨线程缓存一致性，无需加锁）。
        private readonly bool[] _inputs  = new bool[8];

        public int InputCount  => _inputs.Length;
        public int OutputCount => _outputs.Length;

        public SimVacuumIO(ILogService logger)
            : base("SIM_VACUUM_IO", "模拟真空IO卡", isSimulated: true, logger) { Category = Core.Enums.HardwareCategory.IOController; }

        // ── BaseDevice 钩子实现 ────────────────────────────────────────────
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
            => Task.FromResult(true);

        protected override Task InternalDisconnectAsync()
            => Task.CompletedTask;

        protected override Task InternalResetAsync(CancellationToken token)
        {
            // 复位：关闭所有输出，清除所有输入缓存（使用 Volatile.Write 保证写入可见）
            for (int i = 0; i < _outputs.Length; i++) Volatile.Write(ref _outputs[i], false);
            for (int i = 0; i < _inputs.Length;  i++) Volatile.Write(ref _inputs[i],  false);
            return Task.CompletedTask;
        }

        // ── IIOController 实现 ────────────────────────────────────────────

        /// <summary>
        /// 修复：使用 Volatile.Read 保证跨线程读到最新值（防 CPU 缓存导致的脏读）
        /// </summary>
        public bool ReadInput(int portIndex)  => Volatile.Read(ref _inputs[portIndex]);
        public bool ReadOutput(int portIndex) => Volatile.Read(ref _outputs[portIndex]);

        /// <summary>
        /// 写输出端口，并模拟物理反馈延迟（300ms 后 Input[0] 跟随变化）
        /// 修复：后台任务写入 _inputs 时使用 Volatile.Write，与 ReadInput/WaitInputAsync
        ///       的 Volatile.Read 形成配对，消除数据竞争。
        /// </summary>
        public void WriteOutput(int portIndex, bool value)
        {
            Volatile.Write(ref _outputs[portIndex], value);
            _logger.Info($"[{DeviceName}] OUT[{portIndex}] → {(value ? "ON ↑" : "OFF ↓")}");

            // 模拟真空阀动作后传感器的物理响应延迟
            if (portIndex == 0)
            {
                _ = Task.Delay(300).ContinueWith(_ => Volatile.Write(ref _inputs[0], value));
            }
        }

        /// <summary>
        /// 轮询等待输入端口达到目标状态（内置超时防卡死）
        /// 每 50ms 采样一次，直到目标状态或超时
        /// 修复：使用 Volatile.Read 读取 _inputs，与 WriteOutput 的后台写入配对。
        /// </summary>
        public async Task<bool> WaitInputAsync(int portIndex, bool targetState,
            int timeoutMs = 5000, CancellationToken token = default)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested(); // 支持急停打断
                if (Volatile.Read(ref _inputs[portIndex]) == targetState) return true;
                await Task.Delay(50, token);
            }
            _logger.Warn($"[{DeviceName}] WaitInput IN[{portIndex}]=={targetState} 超时 ({timeoutMs}ms)");
            return false;
        }
    }
}
