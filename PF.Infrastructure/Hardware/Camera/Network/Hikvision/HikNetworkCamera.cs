using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Hardware.Camera.Network.Hikvision
{
    /// <summary>
    /// 海康网络摄像机实现（HCNetSDK 私有协议，IPC / NVR）。
    /// 只做"登录 + JPEG 抓图"两件事：抓图走 NET_DVR_CaptureJPEGPicture_NEW 直接取内存 JPEG，
    /// 不做实时预览（那需要 RealPlay + PlayCtrl 解码链路，见方案文档决策 3）。
    ///
    /// <para>SDK 进程级生命周期由 <see cref="HCNetSdkLifetime"/> 引用计数管理：
    /// 本设备在 <see cref="InternalConnectAsync"/> 成功路径持有引用、
    /// <see cref="InternalDisconnectAsync"/> 释放；复位（登出重登）不触碰计数。</para>
    /// </summary>
    public class HikNetworkCamera : BaseNetworkCamera
    {
        /// <summary>抓图缓冲区翻倍重试上限（8MB 起步 → 16 → 32 → 64MB 共 4 轮内解决）。</summary>
        private const int MaxCaptureBufferBytes = 64 * 1024 * 1024;

        /// <summary>缓冲区不足（错误码 43）时的最大翻倍重试轮数。</summary>
        private const int MaxCaptureRetries = 4;

        /// <summary>登录成功后的用户句柄；-1 = 未登录。</summary>
        private int _userId = -1;

        /// <summary>
        /// 登录时 SDK 的代次号（见 <see cref="HCNetSdkLifetime.Epoch"/>）。
        /// <para>与当前代次不一致，说明期间执行过 NET_DVR_Cleanup——本实例的 userID 已经作废，
        /// 但数值上可能正好等于别的设备新登录占用的槽位。此时必须拒绝抓图，否则会静默抓到别人的画面。</para>
        /// </summary>
        private int _loginEpoch;

        /// <summary>
        /// 构造海康网络摄像机。构造函数只存配置，登录延迟到 InternalConnectAsync。
        /// </summary>
        /// <param name="ip">设备 IP（不含端口）</param>
        /// <param name="port">SDK 端口，海康默认 8000（&lt;=0 时取默认）</param>
        /// <param name="userName">登录用户名（默认 admin）</param>
        /// <param name="password">登录密码</param>
        /// <param name="channel">通道号，0 = 自动选第一个可用通道</param>
        /// <param name="captureBufferMb">抓图缓冲区起始大小 MB（默认 8）</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备显示名称</param>
        /// <param name="isSimulated">是否仿真模式</param>
        /// <param name="logger">日志服务</param>
        /// <param name="saveFormat">存图格式（默认 JPEG）</param>
        /// <param name="saveDirectory">存图根目录；null/空白时用 {ConfigPath}\NetworkCamera\{deviceId}</param>
        public HikNetworkCamera(string ip, int port, string userName, string password,
            int channel, int captureBufferMb,
            string deviceId, string deviceName, bool isSimulated, ILogService logger,
            ImageFileFormat saveFormat = ImageFileFormat.Jpeg, string? saveDirectory = null)
            : base(ip, port, userName, password, channel, captureBufferMb,
                  deviceId, deviceName, isSimulated, logger, saveFormat, saveDirectory)
        {
        }

        // ── 登录 / 登出 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 连接：获取 SDK 引用并登录。失败返回 false（由 BaseDevice 重试 3 次），
        /// 抛异常时同步释放引用，避免计数泄漏导致 SDK 永不 Cleanup。
        /// </summary>
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
            => Task.Run(() =>
            {
                HCNetSdkLifetime.Acquire();
                try
                {
                    if (!LoginCore())
                    {
                        HCNetSdkLifetime.Release();
                        return false;
                    }
                    return true;
                }
                catch
                {
                    HCNetSdkLifetime.Release();
                    throw;
                }
            }, token);

        /// <summary>
        /// 纯登录逻辑（不触碰 SDK 引用计数）。
        /// InternalConnectAsync 与 InternalResetAsync 共用——复位时连接已持有引用，
        /// 若直接复用 InternalConnectAsync 会多 Acquire 一次造成计数泄漏。
        /// </summary>
        private bool LoginCore()
        {
            var loginInfo = new NET_DVR_USER_LOGIN_INFO
            {
                sDeviceAddress = BuildFixedBytes(IPAddress, HCNetSdkStructs.NET_DVR_DEV_ADDRESS_MAX_LEN),
                wPort          = (ushort)Port,
                sUserName      = BuildFixedBytes(UserName, HCNetSdkStructs.NET_DVR_LOGIN_USERNAME_MAX_LEN),
                sPassword      = BuildFixedBytes(Password, HCNetSdkStructs.NET_DVR_LOGIN_PASSWD_MAX_LEN),
                cbLoginResult  = null,               // 同步登录
                pUser          = IntPtr.Zero,
                bUseAsynLogin  = false,
                byLoginMode    = 0,                  // 0 = Private 私有协议
                byHttps        = 0,
                byRes3         = new byte[119],
            };

            // 结构体内的 ByValArray 必须预先分配，否则 ref 封送时长度不符
            var deviceInfo = new NET_DVR_DEVICEINFO_V40
            {
                struDeviceV30 = new NET_DVR_DEVICEINFO_V30
                {
                    sSerialNumber = new byte[HCNetSdkStructs.SERIALNO_LEN],
                    byRes2        = new byte[9],
                },
                byRes2 = new byte[243],
            };

            // 登录前取一次代次：登录调用期间若 SDK 被 Cleanup + 重新 Init（正常路径不可能——
            // 本设备登录时自持引用，计数不会归零；只有引用计数被错账虚减时才会发生），
            // 登录后比对不一致就按失败处理，让 BaseDevice 重试拿干净句柄。
            // 不能对这个句柄补 Logout：数值可能已被新代次的其他设备复用，Logout 会误踢对方。
            int epochBefore = HCNetSdkLifetime.Epoch;
            int userId = HCNetSdkApi.NET_DVR_Login_V40(ref loginInfo, ref deviceInfo);
            if (userId < 0)
            {
                string error = HCNetSdkError.GetLastError();
                HardwareLogger.Error($"[{DeviceName}] 登录失败 {IPAddress}:{Port} {error}");
                RaiseAlarm(AlarmCodes.Hardware.NetworkCameraLoginFailed,
                    $"网络摄像机[{DeviceName}]登录失败（{IPAddress}:{Port}）{error}");
                return false;
            }

            if (epochBefore != HCNetSdkLifetime.Epoch)
            {
                HardwareLogger.Warn($"[{DeviceName}] 登录期间 SDK 被重新初始化（代次 {epochBefore} → " +
                    $"{HCNetSdkLifetime.Epoch}），本次句柄归属无法判定，按登录失败处理等待重试");
                return false;
            }

            _userId = userId;
            _loginEpoch = epochBefore;   // 记下句柄属于哪一代 SDK
            SerialNumber = ReadFixedString(deviceInfo.struDeviceV30.sSerialNumber);
            Channels = BuildChannelList(deviceInfo.struDeviceV30);
            ActiveChannel = ResolveChannel();
            HardwareLogger.Info($"[{DeviceName}] 登录成功 {IPAddress}:{Port}，" +
                $"序列号 {SerialNumber}，通道 {Channels.Count} 个，抓图通道 {ActiveChannel}");
            return true;
        }

        /// <summary>
        /// 当前 userID 是否仍然有效（登录后没经历过 NET_DVR_Cleanup）。
        /// 失效时它只是一个普通整数，数值上很可能已被别的设备的新会话占用。
        /// <para>抓图拒绝、登出跳过、健康监控报警三处共用本判定，是句柄有效性的唯一事实源。</para>
        /// </summary>
        private bool IsHandleAlive => _userId >= 0 && _loginEpoch == HCNetSdkLifetime.Epoch;

        /// <summary>仅登出（不动 SDK 引用计数），供断开与复位共用。</summary>
        private void LogoutInternal()
        {
            if (_userId < 0) return;

            // 句柄已失效就必须放弃：这个数值此刻可能正被别的相机使用，
            // 直接 NET_DVR_Logout 会把对方当场踢下线（对方随后抓图全失败）。
            if (!IsHandleAlive)
            {
                HardwareLogger.Warn($"[{DeviceName}] 登录句柄已随 SDK 重新初始化而失效，跳过登出" +
                    $"（当前句柄值 {_userId} 可能已被其他设备复用）");
                _userId = -1;
                return;
            }

            HCNetSdkApi.NET_DVR_Logout(_userId);
            _userId = -1;
        }

        /// <summary>断开：登出并释放 SDK 引用。</summary>
        protected override Task InternalDisconnectAsync()
        {
            LogoutInternal();
            HCNetSdkLifetime.Release();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 复位：登出后重新登录（引用计数在连接期间已持有，此处不再 Acquire）。
        ///
        /// <para><b>必须先判 IsConnected</b>：<see cref="BaseDevice.ResetAsync"/> 不做连接态检查，
        /// 而断开时 <see cref="InternalDisconnectAsync"/> 已 Release 掉 SDK 引用（计数可能已归零、
        /// NET_DVR_Cleanup 已执行）。此时若照常登录，会拿到一个"野" userID——
        /// 设备仍标记为未连接，BaseDevice.DisconnectAsync 开头的 IsConnected 短路会跳过 InternalDisconnectAsync，
        /// 这个 userID 永远登不出去；且健康监控只在已连接时跑，泄漏是静默的。</para>
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.Run(() =>
            {
                if (!IsConnected)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 设备当前未连接，复位已忽略（请先点「登录」）");
                    return;
                }

                LogoutInternal();
                if (!LoginCore())
                    HardwareLogger.Error($"[{DeviceName}] 复位后重新登录失败，设备处于未登录状态");
            }, token);

        // ── 通道 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 由登录返回的设备信息构建通道列表。
        /// NVR 下数字通道总数要拼高低位（byIPChanNum + byHighDChanNum × 256，只取低位会漏 256 的倍数），
        /// 起始数字通道号 byStartDChan 为 0 时回退"起始模拟通道 + 模拟通道数"。
        /// </summary>
        private static IReadOnlyList<NetworkCameraChannel> BuildChannelList(in NET_DVR_DEVICEINFO_V30 v30)
        {
            var list = new List<NetworkCameraChannel>();

            int analogStart = v30.byStartChan;
            for (int i = 0; i < v30.byChanNum; i++)
                list.Add(new NetworkCameraChannel
                {
                    ChannelNo = analogStart + i,
                    Name = $"模拟通道 {analogStart + i}",
                });

            int ipTotal = v30.byIPChanNum + v30.byHighDChanNum * 256;
            if (ipTotal > 0)
            {
                int ipStart = v30.byStartDChan > 0
                    ? v30.byStartDChan
                    : analogStart + v30.byChanNum;
                for (int i = 0; i < ipTotal; i++)
                    list.Add(new NetworkCameraChannel
                    {
                        ChannelNo = ipStart + i,
                        Name = $"IP 通道 {ipStart + i}",
                        IsIpChannel = true,
                    });
            }

            return list;
        }

        /// <summary>
        /// 解析抓图通道：配置指定且存在 → 用配置值；否则取第一个可用通道。
        /// <para>通道列表为空（IPC 常见：设备一个通道都不上报）时不发警告——
        /// 那条路径本来就该回退，报"（可用: ）"只会刷噪音。</para>
        /// </summary>
        private int ResolveChannel()
        {
            if (ConfiguredChannel > 0)
            {
                if (Channels.Any(c => c.ChannelNo == ConfiguredChannel))
                    return ConfiguredChannel;

                if (Channels.Count > 0)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 配置通道 {ConfiguredChannel} 不在设备通道列表中，" +
                        $"回退第一个可用通道（可用: {string.Join(", ", Channels.Select(c => c.ChannelNo))}）");
                }
            }

            // 设备没上报任何通道时回退 1：IPC 的私有协议通道号恒为 1，
            // 真正不支持时抓图会返回错误码 4（通道号错误），由调用方看到具体原因。
            return Channels.FirstOrDefault()?.ChannelNo ?? 1;
        }

        // ── 抓图 ─────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>方法体标 async，保证"未登录"这类前置失败也走 faulted Task 而不是同步抛出
        /// （与 <see cref="IntelligentCamera.Keyence.KeyenceIntelligentCamera.Trigger"/> 一致）。</remarks>
        public override async Task<NetworkCameraFrame> CaptureJpegAsync(int? channel = null, CancellationToken token = default)
        {
            int ch = channel ?? ActiveChannel;

            if (IsSimulated)
            {
                return new NetworkCameraFrame
                {
                    Data = BuildSimulatedJpeg(DeviceName, ch),
                    Channel = ch,
                    SerialNumber = SerialNumber,
                };
            }

            if (_userId < 0)
                throw new InvalidOperationException($"相机[{DeviceName}]未登录，无法抓图（先连接/复位）");

            // 句柄失效必须显式拒绝，不能照常抓图：
            // 失效的 userID 数值上可能已被别的相机的新会话占用，照抓会静默返回**别人的画面**。
            // 这种情况只能是 SDK 被提前 Cleanup 了（引用计数错账），属于必须修掉的缺陷，
            // 所以这里直接报错并留下可追踪的日志，而不是悄悄自动重登把问题掩盖掉。
            if (!IsHandleAlive)
            {
                HardwareLogger.Error($"[{DeviceName}] 登录句柄已失效（SDK 曾被重新初始化），" +
                    $"拒绝抓图以避免抓到其他相机的画面。请重新登录本相机。");
                RaiseAlarm(AlarmCodes.Hardware.NetworkCameraCaptureFailed,
                    $"网络摄像机[{DeviceName}]登录句柄失效，需重新登录");
                throw new InvalidOperationException(
                    $"相机[{DeviceName}]登录句柄已失效（SDK 被重新初始化），" +
                    "继续抓图可能返回其他相机的画面，已拒绝。请重新登录本相机。");
            }

            // 抓图串行化：SDK 不保证同一登录句柄的并发调用安全，而生产流程的判定
            // 与调试页预览可能同时抓同一台相机——真实抓图共用 _captureGate 排队
            await _captureGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    var para = new NET_DVR_JPEGPARA
                    {
                        wPicSize    = 0xFF,   // 自动按当前码流分辨率
                        wPicQuality = 0,      // 最好质量
                    };

                    // SDK 无"查询所需大小"接口：按配置起始大小分配，
                    // 错误码 43（缓冲区太小）时翻倍重试至上限
                    int size = CaptureBufferBytes;
                    for (int attempt = 0; attempt < MaxCaptureRetries; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        var buffer = new byte[size];
                        uint returned = 0;

                        if (HCNetSdkApi.NET_DVR_CaptureJPEGPicture_NEW(
                                _userId, ch, ref para, buffer, (uint)buffer.Length, ref returned))
                        {
                            if (returned == 0)
                                throw new InvalidOperationException($"相机[{DeviceName}]抓图返回空数据（通道 {ch}）");

                            return new NetworkCameraFrame
                            {
                                Data = buffer[..(int)returned],
                                Channel = ch,
                                SerialNumber = SerialNumber,
                            };
                        }

                        uint error = HCNetSdkApi.NET_DVR_GetLastError();
                        if (error != 43)
                            throw new InvalidOperationException(
                                $"相机[{DeviceName}]抓图失败（通道 {ch}）: {HCNetSdkError.GetMessage(error)}");

                        size *= 2;
                        HardwareLogger.Warn($"[{DeviceName}] 抓图缓冲区不足，翻倍至 {size / 1024 / 1024}MB 重试");
                        if (size > MaxCaptureBufferBytes)
                            throw new InvalidOperationException(
                                $"相机[{DeviceName}]抓图失败: 缓冲区已达上限 {MaxCaptureBufferBytes / 1024 / 1024}MB 仍不足");
                    }

                    throw new InvalidOperationException($"相机[{DeviceName}]抓图失败: 缓冲区重试用尽");
                }, token).ConfigureAwait(false);
            }
            finally
            {
                _captureGate.Release();
            }
        }

        // ── 健康监控 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 连接类健康判定：未登录（如复位重登失败）或句柄已随 SDK 重新初始化而失效，
        /// 均属"需重新登录"的状态——用 <see cref="IsHandleAlive"/> 判定而非只看 _userId &lt; 0：
        /// 失效句柄是个正整数，只看符号发现不了，会拖到第一次抓图才暴露；
        /// 这里让健康监控每秒主动发现并报警，重新登录后防抖自动消警。
        /// 网络瞬断由 SDK 内部重连 + 抓图时的错误码兜底，此处只看登录态。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                UpdateAutoClearableHealth(!IsHandleAlive, AlarmCodes.Hardware.CameraHeartbeatTimeout,
                    _userId < 0
                        ? $"相机[{DeviceName}]未登录（{IPAddress}:{Port}），连接可能已断开"
                        : $"相机[{DeviceName}]登录句柄已失效（SDK 被重新初始化），需重新登录");
            }
            return Task.CompletedTask;
        }
    }
}
