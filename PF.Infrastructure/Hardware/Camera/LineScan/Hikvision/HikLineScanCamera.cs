using MvCameraControl;
using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.FrameGrabber.Hikvision;
using PF.Infrastructure.Hardware.Vision.Hikvision;
using System.Threading.Channels;

namespace PF.Infrastructure.Hardware.Camera.LineScan.Hikvision
{
    /// <summary>
    /// 海康线阵（线扫）相机，基于官方托管 SDK <c>MvCameraControl.Net.dll</c> 的 V2 面向对象 API。
    /// 已验证型号：MV-CL162-91F2M（CameraLink，配采集卡）。
    ///
    /// <para><b>两种链路一套代码</b>：本类不带"链路开关"字段，而是由配置拓扑决定——
    /// 挂了采集卡父设备（<see cref="BaseLineScanCamera.HasFrameGrabber"/> 为 true）即走 CameraLink，
    /// 帧长/帧触发下发到采集卡节点树；没挂即为 GigE/USB 直连，下发到相机自身节点树。
    /// 这一分叉被收敛在 <see cref="IFrameControl"/> 策略里，其余逻辑（枚举、开关流、收帧、存盘）完全共用。</para>
    ///
    /// <para><b>取流方式</b>：用后台线程轮询 <c>GetImageBuffer</c>，而不是注册 FrameGrabedEvent。
    /// 与官方 BasicDemoLineScan 一致——轮询能让"停流"变成一个可确定 Join 的动作，
    /// 回调式则要额外处理"回调仍在执行时设备已被 Close"的竞态。</para>
    ///
    /// <para><b>内存</b>：线扫单帧可达数百 MB（16K 宽 × 上万行）。SDK 内部缓存节点数
    /// （<c>imageNodeNum</c>）会成倍放大占用，不可照抄示例的 5，构造时按实际帧长评估并传入。</para>
    ///
    /// <para>⚠️ 部署要求：除托管 DLL 外还需安装海康 MVS 运行时；CameraLink 还需采集卡驱动(MVFG)。</para>
    /// </summary>
    public class HikLineScanCamera : BaseLineScanCamera
    {
        /// <summary>取流线程单次轮询的等待时长（毫秒）。停流时最多多等这么久即可 Join。</summary>
        private const int GrabPollTimeoutMs = 500;

        /// <summary>仿真模式下合成图像的宽度。取小值以免调试机被假图撑爆内存。</summary>
        private const int SimulatedWidth = 1024;

        /// <summary>仿真模式下连续取流的出图周期（毫秒）。</summary>
        private const int SimulatedFramePeriodMs = 200;

        private readonly string _serialNumber;
        private readonly int _index;
        private readonly uint _imageNodeNum;

        private IDevice? _device;
        private IFrameControl? _frameControl;

        private Thread? _receiveThread;
        private volatile bool _isGrabbing;

        /// <summary>
        /// 供存盘用的克隆帧。SDK 没有 ImageFactory，无法从 byte[] 反构 IImage，
        /// 存盘只能走 <c>ImageSaver.SaveImageToFile(path, IImage, ...)</c>，
        /// 因此必须额外持有一份 SDK 侧的克隆（与官方 Demo 的 frameForSave 同理）。
        /// </summary>
        private IFrameOut? _frameForSave;
        private readonly object _saveLock = new();

        /// <summary>
        /// 最近帧的传递通道。容量 2 且满时丢最旧：调试面板预览跟不上出图速度时，
        /// 应该丢掉过期帧而不是把 SDK 缓存拖满导致丢帧告警。
        /// </summary>
        private Channel<LineScanFrame> _frameChannel = CreateFrameChannel();

        private CancellationTokenSource? _simulationCts;

        private string _modelName = string.Empty;
        private string _resolvedSerialNumber = string.Empty;
        private CameraTransportLayer _transportLayer = CameraTransportLayer.Unknown;
        private double _lineSpacingUm;
        private LineScanCameraConfig? _lastConfig;

        /// <summary>
        /// 构造海康线阵相机。
        /// </summary>
        /// <param name="serialNumber">
        /// 相机序列号。非空时按序列号精确选定（推荐，插拔/枚举顺序变化不受影响）；
        /// 为空时回退到按 <paramref name="index"/> 选定。
        /// </param>
        /// <param name="index">枚举结果中的索引（序列号为空时使用）。</param>
        /// <param name="imageNodeNum">
        /// SDK 内部图像缓存节点数。占用 ≈ 帧字节数 × 本值，线扫大帧下必须按实际帧长评估：
        /// 单帧几百 MB 时取 2，中小帧可取 3~5。小于 2 时按 2 处理（少于 2 会让取流与消费完全串行）。
        /// </param>
        /// <param name="deviceId">设备唯一标识。</param>
        /// <param name="deviceName">设备显示名称。</param>
        /// <param name="isSimulated">是否仿真模式。</param>
        /// <param name="logger">日志服务。</param>
        public HikLineScanCamera(string? serialNumber, int index, int imageNodeNum,
            string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            _serialNumber = serialNumber ?? string.Empty;
            _index = index < 0 ? 0 : index;
            _imageNodeNum = (uint)(imageNodeNum < 2 ? 2 : imageNodeNum);
        }

        /// <inheritdoc/>
        public override string ModelName => _modelName;

        /// <inheritdoc/>
        public override string SerialNumber => _resolvedSerialNumber;

        /// <inheritdoc/>
        public override CameraTransportLayer TransportLayer => _transportLayer;

        /// <inheritdoc/>
        public override double LineSpacingUm => _lineSpacingUm;

        /// <inheritdoc/>
        public override bool IsGrabbing => _isGrabbing;

        /// <summary>当前生效的帧控制落点描述（"采集卡[xx]" 或 "相机自身(直连)"），供调试面板展示。</summary>
        public string FrameControlDescription => _frameControl?.Description ?? "未连接";

        #region BaseDevice 钩子

        /// <summary>
        /// 内部连接：初始化 SDK → 枚举相机（挂卡时只枚举本卡下的）→ 按序列号/索引选定
        /// → 创建实例并打开 → GigE 探测最佳包大小 → 建立节点通道与帧控制策略。
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                MvSdkLifetime.Acquire();

                if (!EnumerateDevices(out List<IDeviceInfo> infoList))
                {
                    MvSdkLifetime.Release();
                    return false;
                }

                IDeviceInfo? target = SelectDevice(infoList);
                if (target == null)
                {
                    HardwareLogger.Error($"[{DeviceName}] 未找到匹配的相机"
                        + $"（序列号='{_serialNumber}'，索引={_index}，在线数量={infoList.Count}）。");
                    MvSdkLifetime.Release();
                    return false;
                }

                try
                {
                    _device = DeviceFactory.CreateDevice(target);
                }
                catch (Exception ex)
                {
                    HardwareLogger.Error($"[{DeviceName}] 创建相机实例失败：{ex.Message}", ex);
                    MvSdkLifetime.Release();
                    return false;
                }

                int ret = _device.Open();
                if (ret != MvError.MV_OK)
                {
                    HardwareLogger.Error($"[{DeviceName}] 打开相机失败，错误码=0x{ret:X8}。");
                    DisposeDevice();
                    return false;
                }

                TryOptimizeGigEPacketSize();

                _modelName = target.ModelName ?? string.Empty;
                _resolvedSerialNumber = target.SerialNumber ?? string.Empty;
                _transportLayer = MvTypeMapper.ToTransportLayer(target.TLayerType);

                var accessor = new GenICamNodeAccessor(() => _device?.Parameters, HardwareLogger, DeviceName);
                NodeAccessor = accessor;

                // 帧长/帧触发写哪棵节点树，由是否挂了采集卡决定
                _frameControl = Parent != null
                    ? new InterfaceFrameControl(Parent)
                    : new DeviceFrameControl(accessor, HardwareLogger, DeviceName);

                // 采集模式固定为连续：线扫的"一帧"由帧长/帧触发界定，与相机的单帧/多帧采集模式无关
                accessor.SetIfPresent("AcquisitionMode", "Continuous");

                HardwareLogger.Info($"[{DeviceName}] 相机已打开：{_modelName} (SN={_resolvedSerialNumber}, "
                    + $"传输层={_transportLayer}, 帧控制落点={_frameControl.Description})。");
                return true;
            }, token);
        }

        /// <summary>内部断开：停流 → 关闭并释放相机实例 → 释放一次 SDK 引用。</summary>
        protected override async Task InternalDisconnectAsync()
        {
            await StopAsync();

            await Task.Run(() =>
            {
                try
                {
                    _device?.Close();
                }
                catch (Exception ex)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 关闭相机时异常：{ex.Message}", ex);
                }
                finally
                {
                    DisposeDevice();
                }
            });
        }

        /// <summary>
        /// 内部复位：相机无机械动作，重新打开并把最近一次配置补发一遍
        /// （相机断电重启后参数会回到设备默认值，不补发会静默地按错误参数扫描）。
        /// </summary>
        protected override async Task InternalResetAsync(CancellationToken token)
        {
            await InternalDisconnectAsync();

            if (!await InternalConnectAsync(token)) return;

            if (_lastConfig != null)
                await ApplyConfigAsync(_lastConfig, token);
        }

        /// <summary>
        /// 健康检查：相机实例存在且 SDK 报告在线才算健康。
        /// 属连接类报警，用防抖自动消除（重连成功后自动撤销）。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool faulted = _device == null || !_device.IsConnected;
                UpdateAutoClearableHealth(faulted,
                    AlarmCodes.Hardware.CameraHeartbeatTimeout,
                    $"线阵相机[{DeviceName}]连接中断。");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region 配置下发

        /// <summary>
        /// 下发完整配置：相机本体参数 → 行触发（含编码器）→ 帧控制（转交策略）。
        /// <para>逐节点下发，单项失败只记警告并继续：换型号时个别节点缺失是常态，
        /// 不应让整机初始化失败在一个可选参数上。</para>
        /// </summary>
        public override async Task ApplyConfigAsync(LineScanCameraConfig config, CancellationToken token = default)
        {
            if (config == null) return;

            _lastConfig = config;
            _lineSpacingUm = config.LineTrigger.Mode == LineTriggerMode.Encoder
                ? config.LineTrigger.Encoder?.LineSpacingUm ?? 0
                : 0;

            if (IsSimulated)
            {
                HardwareLogger.Info($"[{DeviceName}] 仿真模式：跳过参数下发（帧长={config.FrameControl.ImageHeight}行）。");
                return;
            }

            var acc = NodeAccessor;
            if (acc == null)
            {
                HardwareLogger.Warn($"[{DeviceName}] 相机未连接，跳过配置下发。");
                return;
            }

            await Task.Run(() =>
            {
                ApplyBodyParameters(acc, config);
                ApplyLineTrigger(acc, config.LineTrigger);
                acc.ApplyExtraNodes(config.ExtraNodes);
            }, token);

            // 帧控制交给策略：挂卡时写采集卡节点树，直连时写相机节点树
            if (_frameControl != null)
                await _frameControl.ApplyAsync(config.FrameControl, token);

            WarnIfFrameMemoryHeavy(config.FrameControl.ImageHeight);

            HardwareLogger.Info($"[{DeviceName}] 配置已下发：行触发={config.LineTrigger.Mode}, "
                + $"帧长={config.FrameControl.ImageHeight}行, 行间距={_lineSpacingUm:F3}μm/行。");
        }

        /// <summary>相机本体参数：扫描模式、像素格式、压缩、曝光、增益。</summary>
        private void ApplyBodyParameters(GenICamNodeAccessor acc, LineScanCameraConfig config)
        {
            acc.SetIfPresent("ScanMode", config.ScanMode);
            acc.SetIfPresent("PixelFormat", config.PixelFormat);
            acc.SetIfPresent("ImageCompressionMode", config.ImageCompressionMode);

            if (config.ExposureTimeUs is > 0)
            {
                // 自动曝光开着时曝光时间是只读的，必须先关
                acc.SetIfPresent("ExposureAuto", "Off");
                acc.SetIfPresent("ExposureTime", config.ExposureTimeUs.Value.ToString("F0"));
            }

            acc.SetIfPresent("PreampGain", config.PreampGain);

            if (config.DigitalShift.HasValue)
            {
                acc.SetIfPresent("DigitalShiftEnable", "true");
                acc.SetIfPresent("DigitalShift", config.DigitalShift.Value.ToString("F2"));
            }
        }

        /// <summary>
        /// 行触发：控制"每扫一行由谁驱动"。两种链路下都配在相机本体上。
        /// <para>新固件有独立的 LineTriggerControl 节点组，老固件要退回
        /// TriggerSelector=LineStart，用节点探测选分支（同官方 LineScanIOSettings 示例）。</para>
        /// </summary>
        private void ApplyLineTrigger(GenICamNodeAccessor acc, LineTriggerConfig cfg)
        {
            bool modern = acc.IsNodeAvailable("LineTriggerControl");

            if (cfg.Mode == LineTriggerMode.InternalRate)
            {
                if (modern) acc.SetIfPresent("LineTriggerMode", "false");
                else if (acc.SetNode("TriggerSelector", "LineStart")) acc.SetIfPresent("TriggerMode", "Off");

                acc.SetIfPresent("AcquisitionLineRateEnable", cfg.AcquisitionLineRateEnable ? "true" : "false");
                if (cfg.AcquisitionLineRateEnable && cfg.AcquisitionLineRate > 0)
                    acc.SetIfPresent("AcquisitionLineRate", cfg.AcquisitionLineRate.ToString());

                acc.ApplyExtraNodes(cfg.ExtraNodes);
                return;
            }

            // 编码器模式下触发源固定为编码器模块输出；外部行信号模式下由配置给出（如 Line0）
            string source = cfg.TriggerSource
                ?? (cfg.Mode == LineTriggerMode.Encoder ? "EncoderModuleOut" : "Line0");

            if (modern)
            {
                acc.SetIfPresent("LineTriggerMode", "true");
                acc.SetIfPresent("LineTriggerSource", source);
            }
            else if (acc.SetNode("TriggerSelector", "LineStart"))
            {
                acc.SetIfPresent("TriggerMode", "On");
                acc.SetIfPresent("TriggerSource", source);
            }
            else
            {
                HardwareLogger.Warn($"[{DeviceName}] 相机既无 LineTriggerControl 节点组，"
                    + "也不支持 TriggerSelector=LineStart，行触发配置已跳过。");
                return;
            }

            if (cfg.Mode == LineTriggerMode.Encoder)
                ApplyEncoder(acc, cfg.Encoder);

            acc.ApplyExtraNodes(cfg.ExtraNodes);
        }

        /// <summary>
        /// 编码器接线配置（外置磁栅读数头接入相机 IO）。
        /// <para>分频、方向反转、单/双向触发等节点因型号而异，不写死在代码里，
        /// 由 <see cref="EncoderConfig.ExtraNodes"/> 现场填入。</para>
        /// </summary>
        private void ApplyEncoder(GenICamNodeAccessor acc, EncoderConfig? encoder)
        {
            if (encoder == null)
            {
                HardwareLogger.Warn($"[{DeviceName}] 行触发为编码器模式但未提供编码器配置，"
                    + "已沿用相机当前的编码器接线参数。");
                return;
            }

            acc.SetIfPresent("EncoderSelector", encoder.Selector);
            acc.SetIfPresent("EncoderSourceA", encoder.SourceA);
            acc.SetIfPresent("EncoderSourceB", encoder.SourceB);
            acc.ApplyExtraNodes(encoder.ExtraNodes);
        }

        #endregion

        #region 取流

        /// <summary>
        /// 开流并武装。必须在轴开始运动**之前**调用，否则会漏掉起始若干行。
        /// <para>开流前清空历史帧，避免上一次扫描的残留帧被当成本次结果取走。</para>
        /// </summary>
        public override Task<bool> ArmAsync(CancellationToken token = default)
        {
            if (_isGrabbing)
            {
                HardwareLogger.Debug($"[{DeviceName}] 已处于取流状态，忽略重复开流。");
                return Task.FromResult(true);
            }

            _frameChannel = CreateFrameChannel();

            if (IsSimulated)
            {
                _isGrabbing = true;
                StartSimulation();
                HardwareLogger.Info($"[{DeviceName}] 仿真模式：已开流。");
                return Task.FromResult(true);
            }

            var dev = _device;
            if (dev == null)
            {
                HardwareLogger.Error($"[{DeviceName}] 相机未连接，无法开流。");
                return Task.FromResult(false);
            }

            return Task.Run(() =>
            {
                // 缓存节点数必须在 StartGrabbing 之前设置
                int ret = dev.StreamGrabber.SetImageNodeNum(_imageNodeNum);
                if (ret != MvError.MV_OK)
                    HardwareLogger.Warn($"[{DeviceName}] 设置图像缓存节点数({_imageNodeNum})失败，"
                        + $"错误码=0x{ret:X8}，将使用 SDK 默认值。");

                dev.StreamGrabber.ClearImageBuffer();

                _isGrabbing = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = $"LineScanRecv-{DeviceName}",
                };
                _receiveThread.Start();

                ret = dev.StreamGrabber.StartGrabbing();
                if (ret != MvError.MV_OK)
                {
                    _isGrabbing = false;
                    _receiveThread.Join();
                    _receiveThread = null;
                    HardwareLogger.Error($"[{DeviceName}] 开流失败，错误码=0x{ret:X8}。");
                    return false;
                }

                HardwareLogger.Info($"[{DeviceName}] 已开流（缓存节点数={_imageNodeNum}）。");
                return true;
            }, token);
        }

        /// <summary>停止取流。先停线程再停流，确保停流后不会再有回调触碰已释放的缓冲区。</summary>
        public override Task StopAsync(CancellationToken token = default)
        {
            if (!_isGrabbing) return Task.CompletedTask;

            _isGrabbing = false;
            StopSimulation();

            var thread = _receiveThread;
            _receiveThread = null;
            var dev = _device;

            return Task.Run(() =>
            {
                thread?.Join();

                if (dev == null) return;

                int ret = dev.StreamGrabber.StopGrabbing();
                if (ret != MvError.MV_OK)
                    HardwareLogger.Warn($"[{DeviceName}] 停流失败，错误码=0x{ret:X8}。");
                else
                    HardwareLogger.Info($"[{DeviceName}] 已停流。");
            });
        }

        /// <summary>
        /// 取流线程：轮询 GetImageBuffer，把帧深拷贝成框架对象后投递，
        /// 并保留一份 SDK 克隆帧供存盘。
        /// </summary>
        private void ReceiveLoop()
        {
            while (_isGrabbing)
            {
                var dev = _device;
                if (dev == null) break;

                IFrameOut? frameOut = null;
                try
                {
                    int ret = dev.StreamGrabber.GetImageBuffer((uint)GrabPollTimeoutMs, out frameOut);
                    if (ret != MvError.MV_OK) continue;   // 超时属常态：帧触发模式下大多数轮询都取不到图

                    HandleFrame(dev, frameOut);
                }
                catch (Exception ex)
                {
                    HardwareLogger.Error($"[{DeviceName}] 取流线程异常：{ex.Message}", ex);
                }
                finally
                {
                    if (frameOut != null)
                    {
                        try { dev?.StreamGrabber.FreeImageBuffer(frameOut); }
                        catch (Exception ex) { HardwareLogger.Debug($"[{DeviceName}] 释放图像缓存异常：{ex.Message}", ex); }
                    }
                }
            }
        }

        /// <summary>处理一帧：必要时先解压，再深拷贝、留存克隆、投递。</summary>
        private void HandleFrame(IDevice dev, IFrameOut frameOut)
        {
            IFrameOut? decoded = null;
            try
            {
                // 开了无损压缩（HB）时，取到的是压缩流，必须先解码才是可按宽高解析的裸像素
                if (IsCompressed(frameOut.Image.PixelType))
                {
                    int ret = dev.ImageDecoder.HBDecode(frameOut, out decoded);
                    if (ret != MvError.MV_OK)
                    {
                        HardwareLogger.Warn($"[{DeviceName}] 无损压缩帧解码失败，错误码=0x{ret:X8}，本帧丢弃。");
                        return;
                    }
                }

                IFrameOut effective = decoded ?? frameOut;
                RetainForSave(effective);

                var frame = ToLineScanFrame(effective);
                _frameChannel.Writer.TryWrite(frame);
                OnFrameReceived(frame);
            }
            finally
            {
                // 克隆已单独留存，解码产物用完即释放，否则大帧下内存涨得极快
                try { decoded?.Dispose(); }
                catch (Exception ex) { HardwareLogger.Debug($"[{DeviceName}] 释放解码帧异常：{ex.Message}", ex); }
            }
        }

        /// <summary>把 SDK 帧深拷贝为框架帧对象。</summary>
        private static LineScanFrame ToLineScanFrame(IFrameOut frameOut)
        {
            IImage image = frameOut.Image;
            byte[] data = image.PixelData ?? Array.Empty<byte>();

            // PixelData 由 SDK 缓冲区托管，FreeImageBuffer 后即失效，必须拷出来
            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);

            return new LineScanFrame
            {
                Data = copy,
                Width = (int)image.Width,
                Height = (int)image.Height,
                PixelFormat = MvTypeMapper.ToPixelFormat(image.PixelType),
                FrameNumber = frameOut.FrameNum,
                Timestamp = DateTime.Now,
            };
        }

        /// <summary>留存一份克隆帧供存盘，并释放上一份。</summary>
        private void RetainForSave(IFrameOut frameOut)
        {
            IFrameOut? clone;
            try
            {
                clone = frameOut.Clone() as IFrameOut;
            }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 克隆帧失败，本帧将无法存盘：{ex.Message}", ex);
                return;
            }

            if (clone == null) return;

            IFrameOut? previous;
            lock (_saveLock)
            {
                previous = _frameForSave;
                _frameForSave = clone;
            }

            try { previous?.Dispose(); }
            catch (Exception ex) { HardwareLogger.Debug($"[{DeviceName}] 释放旧克隆帧异常：{ex.Message}", ex); }
        }

        /// <summary>
        /// 是否为无损压缩（HB）像素格式。按枚举名判定而不是逐个列举——
        /// HB 变体有数十个且随 SDK 版本增补，逐个列举必然漏。
        /// </summary>
        private static bool IsCompressed(MvGvspPixelType pixelType)
            => Enum.GetName(typeof(MvGvspPixelType), pixelType)?.Contains("_HB_", StringComparison.Ordinal) == true;

        #endregion

        #region 触发与取帧

        /// <summary>发送一次帧软触发，转交给当前帧控制策略（挂卡时发采集卡命令，直连时发相机命令）。</summary>
        public override Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default)
        {
            if (IsSimulated)
            {
                PublishSimulatedFrame();
                return Task.FromResult(true);
            }

            var fc = _frameControl;
            if (fc == null)
            {
                HardwareLogger.Warn($"[{DeviceName}] 相机未连接，无法发送帧软触发。");
                return Task.FromResult(false);
            }

            return fc.SoftwareTriggerFrameAsync(token);
        }

        /// <summary>
        /// 等待并取走一帧完整图像。超时抛 <see cref="TimeoutException"/> 并触发取帧超时报警。
        /// <para>超时是硬故障而非可忽略的空结果：线扫无位移即无行触发，超时通常意味着
        /// 轴没动、编码器没接上或帧长设置远大于实际行程，必须让上层看到。</para>
        /// </summary>
        public override async Task<LineScanFrame> WaitFrameAsync(int timeoutMs, CancellationToken token = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (timeoutMs > 0) timeoutCts.CancelAfter(timeoutMs);

            try
            {
                return await _frameChannel.Reader.ReadAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                RaiseAlarm(AlarmCodes.Hardware.LineScanFrameTimeout,
                    $"线阵相机[{DeviceName}]等待 {timeoutMs}ms 未收到完整帧。");

                throw new TimeoutException($"线阵相机[{DeviceName}]取帧超时（{timeoutMs}ms）。");
            }
            catch (ChannelClosedException)
            {
                throw new InvalidOperationException($"线阵相机[{DeviceName}]取流已结束，无法继续取帧。");
            }
        }

        #endregion

        #region 存盘与枚举

        /// <summary>
        /// 将最近一帧存盘。走 SDK 的 ImageSaver（它按像素格式做插值/位深转换），
        /// 因此依赖留存的克隆帧而非 <see cref="BaseLineScanCamera.LastImageData"/>。
        /// </summary>
        public override Task<bool> SaveLastImageAsync(string filePath, ImageFileFormat format,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return Task.FromResult(false);

            var dev = _device;
            if (dev == null)
            {
                HardwareLogger.Warn($"[{DeviceName}] 相机未连接，无法存盘。");
                return Task.FromResult(false);
            }

            return Task.Run(() =>
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                lock (_saveLock)
                {
                    if (_frameForSave == null)
                    {
                        HardwareLogger.Warn($"[{DeviceName}] 尚无可存盘的图像。");
                        return false;
                    }

                    int ret = dev.ImageSaver.SaveImageToFile(filePath, _frameForSave.Image,
                        MvTypeMapper.ToImageFormatInfo(format), CFAMethod.Equilibrated);

                    if (ret != MvError.MV_OK)
                    {
                        HardwareLogger.Error($"[{DeviceName}] 图像存盘失败，错误码=0x{ret:X8}，路径={filePath}。");
                        return false;
                    }
                }

                HardwareLogger.Info($"[{DeviceName}] 图像已存盘：{filePath}。");
                return true;
            }, token);
        }

        /// <summary>枚举在线相机。挂了采集卡时只枚举本卡下的，否则枚举全部传输层。</summary>
        public override Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverAsync(CancellationToken token = default)
        {
            if (IsSimulated)
                return Task.FromResult<IReadOnlyList<DiscoveredDeviceInfo>>(Array.Empty<DiscoveredDeviceInfo>());

            return Task.Run<IReadOnlyList<DiscoveredDeviceInfo>>(() =>
            {
                MvSdkLifetime.Acquire();
                try
                {
                    if (!EnumerateDevices(out List<IDeviceInfo> infoList))
                        return Array.Empty<DiscoveredDeviceInfo>();

                    return infoList
                        .Select(d => MvTypeMapper.ToDiscoveredDevice(d, Parent?.DeviceId))
                        .ToList();
                }
                finally
                {
                    MvSdkLifetime.Release();
                }
            }, token);
        }

        #endregion

        #region 仿真

        /// <summary>
        /// 启动仿真出图。仅在**未启用帧触发**时按周期自动出图；
        /// 启用帧触发时由 <see cref="SoftwareTriggerFrameAsync"/> 逐帧产生，与真机行为一致。
        /// </summary>
        private void StartSimulation()
        {
            if (_lastConfig?.FrameControl.TriggerEnable == true) return;

            var cts = new CancellationTokenSource();
            _simulationCts = cts;

            _ = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    PublishSimulatedFrame();
                    try { await Task.Delay(SimulatedFramePeriodMs, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, cts.Token);
        }

        /// <summary>停止仿真出图。</summary>
        private void StopSimulation()
        {
            var cts = _simulationCts;
            _simulationCts = null;

            if (cts == null) return;

            try { cts.Cancel(); } catch { /* 已释放 */ }
            cts.Dispose();
        }

        /// <summary>
        /// 合成一帧渐变灰度图并投递，使工站流程在无硬件时也能整条跑通。
        /// 尺寸刻意取小值（宽固定、高截断），仿真的目的是验证时序而不是复现真实数据量。
        /// </summary>
        private void PublishSimulatedFrame()
        {
            int height = _lastConfig?.FrameControl.ImageHeight ?? 0;
            if (height <= 0) height = 512;
            if (height > 2000) height = 2000;

            var data = new byte[SimulatedWidth * height];
            for (int y = 0; y < height; y++)
            {
                byte value = (byte)(y * 255 / height);
                Array.Fill(data, value, y * SimulatedWidth, SimulatedWidth);
            }

            var frame = new LineScanFrame
            {
                Data = data,
                Width = SimulatedWidth,
                Height = height,
                PixelFormat = ImagePixelFormat.Mono8,
                FrameNumber = unchecked((uint)Environment.TickCount),
                Timestamp = DateTime.Now,
            };

            _frameChannel.Writer.TryWrite(frame);
            OnFrameReceived(frame);
        }

        #endregion

        #region 私有辅助

        /// <summary>创建帧通道：容量 2、满时丢最旧。</summary>
        private static Channel<LineScanFrame> CreateFrameChannel()
            => Channel.CreateBounded<LineScanFrame>(new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = false,
            });

        /// <summary>枚举相机：挂了海康采集卡就只枚举本卡下的，否则枚举全部传输层。</summary>
        private bool EnumerateDevices(out List<IDeviceInfo> infoList)
        {
            int ret;
            if (Parent is HikFrameGrabberCard hik && hik.NativeInterface != null)
            {
                ret = hik.NativeInterface.EnumDevices(out infoList);
            }
            else
            {
                if (Parent != null)
                    HardwareLogger.Warn($"[{DeviceName}] 父采集卡 '{Parent.DeviceName}' 未就绪，"
                        + "回退为全传输层枚举（请确认采集卡先于相机完成初始化）。");

                ret = DeviceEnumerator.EnumDevices(MvTypeMapper.AllCameraLayers, out infoList);
            }

            if (ret != MvError.MV_OK)
            {
                HardwareLogger.Error($"[{DeviceName}] 枚举相机失败，错误码=0x{ret:X8}。");
                infoList = new List<IDeviceInfo>();
                return false;
            }

            if (infoList.Count == 0)
            {
                HardwareLogger.Error($"[{DeviceName}] 未发现任何相机"
                    + "（请确认相机供电、线缆连接，且未被 MVS 客户端等其他程序占用）。");
                return false;
            }

            return true;
        }

        /// <summary>按序列号优先、索引兜底的方式选定相机。</summary>
        private IDeviceInfo? SelectDevice(List<IDeviceInfo> infoList)
        {
            if (!string.IsNullOrWhiteSpace(_serialNumber))
            {
                var matched = infoList.FirstOrDefault(d =>
                    string.Equals(d.SerialNumber, _serialNumber, StringComparison.OrdinalIgnoreCase));

                if (matched != null) return matched;

                HardwareLogger.Warn($"[{DeviceName}] 未匹配到序列号为 '{_serialNumber}' 的相机，"
                    + $"回退按索引 {_index} 选定。");
            }

            return _index < infoList.Count ? infoList[_index] : null;
        }

        /// <summary>GigE 直连时探测并设置最佳包大小；非 GigE 设备直接跳过。</summary>
        private void TryOptimizeGigEPacketSize()
        {
            if (_device is not IGigEDevice gige) return;

            int ret = gige.GetOptimalPacketSize(out int optimal);
            if (ret != MvError.MV_OK)
            {
                HardwareLogger.Warn($"[{DeviceName}] 探测网络最佳包大小失败，错误码=0x{ret:X8}，沿用当前值。");
                return;
            }

            ret = _device.Parameters.SetIntValue("GevSCPSPacketSize", optimal);
            if (ret != MvError.MV_OK)
                HardwareLogger.Warn($"[{DeviceName}] 设置网络包大小({optimal})失败，错误码=0x{ret:X8}。");
            else
                HardwareLogger.Debug($"[{DeviceName}] 网络最佳包大小已设为 {optimal}。");
        }

        /// <summary>
        /// 帧内存体检：单帧过大时提前告警。
        /// 线扫大帧 × 缓存节点数很容易把内存打满，事后表现为 GC 抖动或直接 OOM，
        /// 与其等现场炸掉，不如在配置下发时就把估算值写进日志。
        /// </summary>
        private void WarnIfFrameMemoryHeavy(int imageHeight)
        {
            if (imageHeight <= 0 || LastImageWidth <= 0) return;

            long perFrame = (long)LastImageWidth * imageHeight;
            long total = perFrame * _imageNodeNum;

            if (total > 512L * 1024 * 1024)
            {
                HardwareLogger.Warn($"[{DeviceName}] 单帧约 {perFrame / 1024 / 1024}MB，"
                    + $"× 缓存节点数 {_imageNodeNum} ≈ {total / 1024 / 1024}MB，"
                    + "请评估是否需要减小帧长或缓存节点数。");
            }
        }

        /// <summary>释放相机实例、克隆帧，并归还一次 SDK 引用。</summary>
        private void DisposeDevice()
        {
            NodeAccessor = null;
            _frameControl = null;

            lock (_saveLock)
            {
                try { _frameForSave?.Dispose(); }
                catch (Exception ex) { HardwareLogger.Debug($"[{DeviceName}] 释放克隆帧异常：{ex.Message}", ex); }
                _frameForSave = null;
            }

            try
            {
                _device?.Dispose();
            }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 释放相机实例时异常：{ex.Message}", ex);
            }
            finally
            {
                _device = null;
                _modelName = string.Empty;
                _resolvedSerialNumber = string.Empty;
                _transportLayer = CameraTransportLayer.Unknown;
                MvSdkLifetime.Release();
            }
        }

        #endregion
    }
}
