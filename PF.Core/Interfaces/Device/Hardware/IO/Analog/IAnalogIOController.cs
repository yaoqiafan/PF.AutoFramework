using PF.Core.Models.Device.Hardware.IO.Analog;

namespace PF.Core.Interfaces.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量 IO 设备接口（业务层只依赖它），读写的都是<b>工程值</b>（已按通道配置换算）。
    ///
    /// <para>通道号即物理通道号：业务枚举的 int 值 = AI 或 AO 的通道号（两者各自编号），
    /// 与 <see cref="Basic.IIOController"/> "枚举值 = 端口号"一致。</para>
    ///
    /// <para><b>契约：同步读（<see cref="ReadAnalogInput(int)"/> / <see cref="ReadAnalogInputDetail(int)"/> /
    /// <see cref="ReadAnalogOutput(int)"/>）不得做阻塞 I/O。</b>
    /// 卡类实现直接调用板卡 SDK（读过程映像，极快）；通讯类实现只读后台扫描维护的缓存。
    /// 因此任何线程（含 UI 线程）都可放心同步读。</para>
    ///
    /// <para>写有同步、异步两个版本：机构内简单动作用同步写；工站循环与界面用异步写
    /// （对通讯类设备不占线程、可取消；卡类设备的异步写只是同步写的包装）。</para>
    ///
    /// <para>AO 越限（超出通道 OutputMin/OutputMax）一律<b>拒绝写出并返回 false</b>，不截断，
    /// 避免掩盖业务计算错误。</para>
    /// </summary>
    public interface IAnalogIOController : IHardwareDevice
    {
        #region 通道表

        /// <summary>AI 通道表（按通道号排序的只读快照）</summary>
        IReadOnlyList<AnalogChannelConfig> InputChannels { get; }

        /// <summary>AO 通道表（按通道号排序的只读快照）</summary>
        IReadOnlyList<AnalogChannelConfig> OutputChannels { get; }

        /// <summary>
        /// 校验并整表原子替换通道配置（调试页保存后热更新，无需重载设备）。
        /// 校验失败时原通道表保持不变，返回失败原因。本方法只改内存，持久化由调用方负责。
        /// </summary>
        AnalogConfigResult UpdateChannels(IEnumerable<AnalogChannelConfig> channels);

        #endregion

        #region AI 读（同步，不阻塞）

        /// <summary>读取 AI 工程值；断线 / 通讯失败 / 数据过期 / 未配置时返回 null，超量程时仍返回值。</summary>
        double? ReadAnalogInput(int channel);

        /// <summary>按枚举名读取 AI 工程值（枚举 int 值即通道号）。</summary>
        double? ReadAnalogInput<T>(T name) where T : Enum;

        /// <summary>读取 AI 详细读数（工程值 + 原始值 + 状态 + 时间戳）。</summary>
        AnalogReading ReadAnalogInputDetail(int channel);

        /// <summary>按枚举名读取 AI 详细读数。</summary>
        AnalogReading ReadAnalogInputDetail<T>(T name) where T : Enum;

        #endregion

        #region AO

        /// <summary>写出 AO 工程值；越限、未配置或写出失败返回 false。</summary>
        bool WriteAnalogOutput(int channel, double value);

        /// <summary>按枚举名写出 AO 工程值。</summary>
        bool WriteAnalogOutput<T>(T name, double value) where T : Enum;

        /// <summary>异步写出 AO 工程值；越限、未配置或写出失败返回 false，token 取消时抛 OperationCanceledException。</summary>
        Task<bool> WriteAnalogOutputAsync(int channel, double value, CancellationToken token = default);

        /// <summary>按枚举名异步写出 AO 工程值。</summary>
        Task<bool> WriteAnalogOutputAsync<T>(T name, double value, CancellationToken token = default) where T : Enum;

        /// <summary>回读 AO 当前输出（工程值，同步，不阻塞）；读取失败或未配置返回 null。</summary>
        double? ReadAnalogOutput(int channel);

        /// <summary>按枚举名回读 AO 当前输出。</summary>
        double? ReadAnalogOutput<T>(T name) where T : Enum;

        #endregion

        #region 等待

        /// <summary>
        /// 异步等待 AI 进入 [<paramref name="min"/>, <paramref name="max"/>] 并持续 <paramref name="stableMs"/> 毫秒。
        /// <list type="bullet">
        ///   <item>满足返回 true；超时返回 false 并记 Warn 日志；</item>
        ///   <item><paramref name="token"/> 取消时抛 <see cref="OperationCanceledException"/>（不吞掉，
        ///     避免"用户暂停"被误判为"等待超时"而触发假报警）；</item>
        ///   <item>读数为 null（断线/通讯失败等）视为不满足，同时重置稳定计时；</item>
        ///   <item>仿真模式下通道 SimMode = AlwaysPass 时立即返回 true。</item>
        /// </list>
        /// </summary>
        Task<bool> WaitAnalogInputInRangeAsync(int channel, double min, double max,
            int timeoutMs = 5000, int stableMs = 0, CancellationToken token = default);

        /// <summary>按枚举名等待 AI 进入区间。</summary>
        Task<bool> WaitAnalogInputInRangeAsync<T>(T name, double min, double max,
            int timeoutMs = 5000, int stableMs = 0, CancellationToken token = default) where T : Enum;

        #endregion

        #region 安全

        /// <summary>
        /// 把所有配置了 SafeValue 的 AO 写到安全值，全部成功返回 true。
        /// <para>框架不会自动调用：请在机构 <c>InternalStopAsync</c> 或工站 <c>OnPhysicalStopAsync</c> 中显式调用。
        /// 真正的掉电/掉线保障应在模块硬件上配置输出保持/清零行为。</para>
        /// </summary>
        Task<bool> ApplySafeOutputsAsync(CancellationToken token = default);

        #endregion
    }
}
