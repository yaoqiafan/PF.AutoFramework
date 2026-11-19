using PF.Core.Enums.Hardware;

namespace PF.Core.Interfaces.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 通讯类模拟量数据源（Modbus 远程模块、私有协议传感器等），读写<b>原始值</b>，异步。
    ///
    /// <para>【预留扩展点，当前版本框架内尚无实现】计划由扫描型模拟量设备持有：设备内部后台循环按周期
    /// 调用 <see cref="ReadAllAsync"/> 刷新缓存，<see cref="IAnalogIOController"/> 的同步读只读缓存，
    /// 写请求进队列由扫描线程执行。这样通讯延迟不会传导到业务与 UI。</para>
    ///
    /// <para>接新协议只需实现本接口；扫描调度、缓存、过期判定、换算、仿真、调试页、报警均由框架复用。</para>
    /// </summary>
    public interface IAnalogScanSource
    {
        /// <summary>首次建链 / 握手。</summary>
        Task<bool> OpenAsync(CancellationToken token);

        /// <summary>关闭。</summary>
        Task CloseAsync();

        /// <summary>
        /// 一轮扫描读回全部已配置通道的原始值（实现方自行合并请求，如 Modbus 连续寄存器段）。
        /// 读取失败的通道不出现在结果中；整体失败抛异常。
        /// </summary>
        Task<IReadOnlyDictionary<(AnalogDirection Direction, int Channel), double>> ReadAllAsync(CancellationToken token);

        /// <summary>写出 AO 原始值。</summary>
        Task<bool> WriteAsync(int channel, double raw, CancellationToken token);
    }
}
