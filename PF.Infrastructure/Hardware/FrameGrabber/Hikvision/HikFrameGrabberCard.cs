using MvCameraControl;
using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Vision.Hikvision;

namespace PF.Infrastructure.Hardware.FrameGrabber.Hikvision
{
    /// <summary>
    /// 海康图像采集卡（CameraLink / CoaXPress / XoF），基于官方托管 SDK
    /// <c>MvCameraControl.Net.dll</c> 的 V2 面向对象 API（<see cref="IInterface"/>）。
    ///
    /// <para><b>为什么需要把采集卡建成独立设备</b>：CameraLink 相机没有自己的网络栈，
    /// 流的切帧发生在采集卡上——"一帧多少行"(ImageHeight)、"帧超时"(FrameTimeoutTime)、
    /// "从哪起帧"(StreamTrigger*) 全部是采集卡的节点，只能通过 IInterface 下发。
    /// 相机本体只负责逐行曝光（行触发/编码器配在相机上）。</para>
    ///
    /// <para><b>与相机的关系</b>：本卡为顶级设备（ParentDeviceId 为空，第 1 层初始化），
    /// 相机把 ParentDeviceId 填为本卡 DeviceId 挂在其下（第 2 层），
    /// 由 HardwareManagerService 保证"卡先于相机"的顺序并完成挂载注入。</para>
    ///
    /// <para>⚠️ 部署要求：除托管 DLL 外，还需安装采集卡驱动（MVFG）并随程序输出目录
    /// 携带 SDK 的 win64 原生库，否则枚举不到任何采集卡。</para>
    /// </summary>
    public class HikFrameGrabberCard : BaseFrameGrabberCard
    {
        private readonly string _serialNumber;
        private readonly int _index;

        private IInterface? _interface;
        private string _modelName = string.Empty;
        private string _resolvedSerialNumber = string.Empty;

        /// <summary>最近一次下发的帧触发源，决定软触发时该发哪条命令。</summary>
        private string _frameTriggerSource = "SoftwareSignal0";

        /// <summary>
        /// 构造海康采集卡。
        /// </summary>
        /// <param name="serialNumber">
        /// 采集卡序列号。非空时按序列号精确选定（推荐，插拔顺序变化不受影响）；
        /// 为空时回退到按 <paramref name="index"/> 选定。
        /// </param>
        /// <param name="index">枚举结果中的索引（序列号为空时使用）。</param>
        /// <param name="deviceId">设备唯一标识。</param>
        /// <param name="deviceName">设备显示名称。</param>
        /// <param name="isSimulated">是否仿真模式。</param>
        /// <param name="logger">日志服务。</param>
        public HikFrameGrabberCard(string? serialNumber, int index,
            string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            _serialNumber = serialNumber ?? string.Empty;
            _index = index < 0 ? 0 : index;
        }

        /// <inheritdoc/>
        public override string ModelName => _modelName;

        /// <inheritdoc/>
        public override string SerialNumber => _resolvedSerialNumber;

        /// <summary>
        /// 已打开的底层采集卡实例，供同程序集内挂在本卡下的相机取用（枚举相机、下发帧控制节点）。
        /// 未连接时为 null。
        /// <para>刻意保持 internal：<see cref="Core.Interfaces.Device.Hardware.Card.IFrameGrabberCard"/>
        /// 位于零外部依赖的 PF.Core，不能泄漏任何厂商 SDK 类型。</para>
        /// </summary>
        internal IInterface? NativeInterface => _interface;

        #region BaseDevice 钩子

        /// <summary>
        /// 内部连接：初始化 SDK → 枚举采集卡 → 按序列号/索引选定 → 创建实例 → 打开 → 建立节点通道。
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                MvSdkLifetime.Acquire();

                int ret = InterfaceEnumerator.EnumInterfaces(MvTypeMapper.AllInterfaceLayers,
                    out List<IInterfaceInfo> infoList);
                if (ret != MvError.MV_OK)
                {
                    HardwareLogger.Error($"[{DeviceName}] 枚举采集卡失败，错误码=0x{ret:X8}。");
                    MvSdkLifetime.Release();
                    return false;
                }

                if (infoList.Count == 0)
                {
                    HardwareLogger.Error($"[{DeviceName}] 未发现任何采集卡（请确认采集卡驱动 MVFG 已安装且未被其他程序占用）。");
                    MvSdkLifetime.Release();
                    return false;
                }

                IInterfaceInfo? target = SelectInterface(infoList);
                if (target == null)
                {
                    HardwareLogger.Error($"[{DeviceName}] 未找到匹配的采集卡"
                        + $"（序列号='{_serialNumber}'，索引={_index}，在线数量={infoList.Count}）。");
                    MvSdkLifetime.Release();
                    return false;
                }

                try
                {
                    _interface = InterfaceFactory.CreateInterface(target);
                }
                catch (Exception ex)
                {
                    HardwareLogger.Error($"[{DeviceName}] 创建采集卡实例失败：{ex.Message}", ex);
                    MvSdkLifetime.Release();
                    return false;
                }

                ret = _interface.Open();
                if (ret != MvError.MV_OK)
                {
                    HardwareLogger.Error($"[{DeviceName}] 打开采集卡失败，错误码=0x{ret:X8}。");
                    DisposeInterface();
                    return false;
                }

                _modelName = target.ModelName ?? string.Empty;
                _resolvedSerialNumber = target.SerialNumber ?? string.Empty;
                NodeAccessor = new GenICamNodeAccessor(() => _interface?.Parameters, HardwareLogger, DeviceName);

                HardwareLogger.Info($"[{DeviceName}] 采集卡已打开：{_modelName} (SN={_resolvedSerialNumber}, "
                    + $"ID={target.InterfaceID}, 类型={target.TLayerType})。");
                return true;
            }, token);
        }

        /// <summary>内部断开：关闭并释放采集卡实例，释放一次 SDK 引用。</summary>
        protected override async Task InternalDisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _interface?.Close();
                }
                catch (Exception ex)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 关闭采集卡时异常：{ex.Message}", ex);
                }
                finally
                {
                    DisposeInterface();
                }
            });
        }

        /// <summary>内部复位：采集卡无机械动作，重新打开一次即可。</summary>
        protected override async Task InternalResetAsync(CancellationToken token)
        {
            await InternalDisconnectAsync();
            await InternalConnectAsync(token);
        }

        /// <summary>
        /// 健康检查：采集卡实例存在即视为在线。
        /// 属连接类报警，用防抖自动消除（重新打开成功后自动撤销）。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                UpdateAutoClearableHealth(_interface == null,
                    AlarmCodes.Hardware.FrameGrabberDisconnected,
                    $"采集卡[{DeviceName}]连接中断。");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region IFrameGrabberCard

        /// <summary>
        /// 下发帧控制配置到采集卡节点树。
        /// <para>逐节点下发，单项失败只记警告并继续——不同卡型（CML/CXP/XoF）节点集不同，
        /// 缺少某个节点是常态，不应中断整体配置。</para>
        /// </summary>
        public override Task ApplyFrameControlAsync(FrameControlConfig config, CancellationToken token = default)
        {
            if (config == null) return Task.CompletedTask;

            var acc = NodeAccessor;
            if (acc == null)
            {
                HardwareLogger.Warn($"[{DeviceName}] 采集卡未连接，跳过帧控制配置下发。");
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                // 流参数只在流尚未绑定相机时可写。相机一旦 Open，ImageHeight/StreamTrigger* 全变只读，
                // 写入会返回 MV_E_GC_ACCESS(0x80000106)——"节点访问条件有误"，不是节点名错。
                // 提前探测并明说，免得对着一串"下发失败"去查节点名。
                if (config.ImageHeight > 0 && !acc.IsNodeWritable("ImageHeight"))
                {
                    // 把卡当前的流状态一并打出来：ImageHeight 属于某一路流，
                    // 只报"不可写"没法判断是没选中流、流没绑定相机，还是别的前置条件没满足。
                    HardwareLogger.Warn($"[{DeviceName}] ImageHeight 当前不可写。"
                        + $"流状态：StreamSelector='{acc.GetNode("StreamSelector") ?? "?"}', "
                        + $"CurrentStreamDevice='{acc.GetNode("CurrentStreamDevice") ?? "?"}', "
                        + $"当前 ImageHeight='{acc.GetNode("ImageHeight") ?? "?"}'。");
                }

                // 流选择与相机类型需先于其余流参数设置（CameraLink 位宽配置必须与相机侧匹配，
                // 不匹配的典型表现是出不了图或图像横向错位）
                acc.SetIfPresent("StreamSelector", config.StreamSelector);
                acc.SetIfPresent("CameraType", config.CameraType);

                // 帧长：线扫的"一帧多少行"
                if (config.ImageHeight > 0)
                    acc.SetIfPresent("ImageHeight", config.ImageHeight.ToString());

                // 帧超时：行数攒不满时的兜底出图，扫描中断/急停时靠它避免永久等待
                if (config.FrameTimeoutMs > 0)
                    acc.SetIfPresent("FrameTimeoutTime", config.FrameTimeoutMs.ToString());

                // 残帧策略的节点名随卡型而异：CameraLink 卡叫 StreamPartialImageControl，
                // XoF 卡叫 PartialImageOutputMode。探测哪个在，就写哪个。
                if (!string.IsNullOrWhiteSpace(config.PartialImageControl))
                {
                    string node = acc.IsNodeAvailable("StreamPartialImageControl")
                        ? "StreamPartialImageControl"
                        : "PartialImageOutputMode";
                    acc.SetIfPresent(node, config.PartialImageControl);
                }

                // 帧触发：StreamTrigger* 是 CameraLink/CXP 卡的节点，XoF 卡没有这一组。
                // 先探测再决定写不写——否则连续模式下每次下发都会白刷一条"节点不存在"。
                if (acc.IsNodeAvailable("StreamTriggerEnable"))
                {
                    acc.SetNode("StreamTriggerEnable", config.TriggerEnable ? "true" : "false");
                    if (config.TriggerEnable)
                    {
                        acc.SetIfPresent("StreamTriggerSource", config.TriggerSource);
                        acc.SetIfPresent("StreamTriggerActivation", config.TriggerActivation);
                        _frameTriggerSource = config.TriggerSource ?? _frameTriggerSource;
                    }
                }
                else if (config.TriggerEnable)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 本卡型无 StreamTriggerEnable 节点（XoF 卡即无此组），"
                        + "卡侧帧触发已跳过。此类卡的起帧应配在相机侧，或改用连续模式按帧长自动出图。");
                }

                acc.ApplyExtraNodes(config.ExtraNodes);

                HardwareLogger.Info($"[{DeviceName}] 帧控制配置已下发："
                    + $"帧长={config.ImageHeight}行, 帧超时={config.FrameTimeoutMs}ms, "
                    + $"帧触发={(config.TriggerEnable ? config.TriggerSource : "关闭(连续)")}。");
            }, token);
        }

        /// <summary>
        /// 发送一次帧软触发。
        /// <para>快速软触发（QuickSoftwareTrigger0）延迟更低，但需采集卡固件支持
        /// （官方示例注明仅部分 XoF/CXP 卡支持），故按最近下发的触发源选择命令。</para>
        /// </summary>
        public override Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default)
        {
            var acc = NodeAccessor;
            if (acc == null) return Task.FromResult(false);

            string command = _frameTriggerSource.StartsWith("QuickSoftwareTrigger", StringComparison.OrdinalIgnoreCase)
                ? _frameTriggerSource
                : "StreamSoftwareTrigger";

            return Task.Run(() => acc.ExecuteCommand(command), token);
        }

        /// <summary>枚举挂在本卡上的相机。</summary>
        public override Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverCamerasAsync(CancellationToken token = default)
        {
            var inst = _interface;
            if (inst == null)
                return Task.FromResult<IReadOnlyList<DiscoveredDeviceInfo>>(Array.Empty<DiscoveredDeviceInfo>());

            return Task.Run<IReadOnlyList<DiscoveredDeviceInfo>>(() =>
            {
                int ret = inst.EnumDevices(out List<IDeviceInfo> devices);
                if (ret != MvError.MV_OK)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 枚举本卡上的相机失败，错误码=0x{ret:X8}。");
                    return Array.Empty<DiscoveredDeviceInfo>();
                }

                return devices
                    .Select(d => MvTypeMapper.ToDiscoveredDevice(d, _resolvedSerialNumber))
                    .ToList();
            }, token);
        }

        #endregion

        #region 私有辅助

        /// <summary>按序列号优先、索引兜底的方式选定采集卡。</summary>
        private IInterfaceInfo? SelectInterface(List<IInterfaceInfo> infoList)
        {
            if (!string.IsNullOrWhiteSpace(_serialNumber))
            {
                var matched = infoList.FirstOrDefault(i =>
                    string.Equals(i.SerialNumber, _serialNumber, StringComparison.OrdinalIgnoreCase));

                if (matched != null) return matched;

                HardwareLogger.Warn($"[{DeviceName}] 未匹配到序列号为 '{_serialNumber}' 的采集卡，"
                    + $"回退按索引 {_index} 选定。");
            }

            return _index < infoList.Count ? infoList[_index] : null;
        }

        /// <summary>释放采集卡实例并归还一次 SDK 引用。</summary>
        private void DisposeInterface()
        {
            NodeAccessor = null;

            try
            {
                _interface?.Dispose();
            }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 释放采集卡实例时异常：{ex.Message}", ex);
            }
            finally
            {
                _interface = null;
                _modelName = string.Empty;
                _resolvedSerialNumber = string.Empty;
                MvSdkLifetime.Release();
            }
        }

        #endregion
    }
}
