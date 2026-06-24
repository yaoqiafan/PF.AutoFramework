using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.HKRobot
{
    /// <summary>
    /// 基于海康机器人官方 SDK (MvIdApi) 的条码扫描枪实现。
    /// <para>与同目录下 <see cref="HKBarcodeScan"/>（TCP 透传协议版）并列，二者均继承
    /// <see cref="BaseBarcodeScan"/>。区别在于：TCP 版直接用网口协议触发/取码；
    /// 本类通过官方 SDK 完成"枚举设备 → 创建句柄 → 打开 → 软触发 → 回调取码"全流程。</para>
    /// <para>原生调用集中在 <see cref="HKRobotIdSdk"/>，便于 SDK 升级时统一替换。
    /// ⚠️ 所有原生签名/结构体需按实际 MvIdApi.h 校对，详见该文件头部说明。</para>
    /// </summary>
    public class HKSdkBarcodeScan : BaseBarcodeScan
    {
        /// <summary>
        /// 构造海康 SDK 扫码枪。
        /// </summary>
        /// <param name="ip">读码器 IP 地址（用于从枚举结果中选定设备）。</param>
        /// <param name="timeoutms">单次触发取码超时（毫秒）。</param>
        /// <param name="deviceId">设备唯一标识。</param>
        /// <param name="deviceName">设备名称。</param>
        /// <param name="isSimulated">是否仿真模式。</param>
        /// <param name="logger">日志服务。</param>
        public HKSdkBarcodeScan(string ip, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            IPAdress = ip ?? string.Empty;
            TimeOutMs = timeoutms > 0 ? timeoutms : 5000;
        }

        /// <summary>读码器 IP 地址。</summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 触发端口（仅 TCP 版使用）。SDK 版不基于端口通信，固定返回 0，
        /// 仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员。
        /// </summary>
        public override int TiggerPort => 0;

        /// <summary>
        /// 用户端口（仅 TCP 版使用）。SDK 版不基于端口通信，固定返回 0，
        /// 仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员。
        /// </summary>
        public override int UserPort => 0;

        /// <summary>单次触发取码超时（毫秒）。</summary>
        public override int TimeOutMs { get; }

        // -------- SDK 句柄与同步状态 --------

        /// <summary>SDK 设备句柄（0 表示未创建）。</summary>
        private IntPtr _handle = IntPtr.Zero;

        /// <summary>是否已 StartGrabbing。</summary>
        private bool _grabbing;

        /// <summary>结果回调委托，必须作为字段持有，防止 GC 回收导致回调时崩溃。</summary>
        private HKRobotIdSdk.MV_ID_RESULT_CB _resultCallback;

        /// <summary>触发取码完成信号。</summary>
        private readonly ManualResetEventSlim _resultEvent = new ManualResetEventSlim(false);

        /// <summary>最近一次回调取到的条码。</summary>
        private string _lastCode = string.Empty;

        #region IBarcodeScan

        /// <summary>
        /// 触发扫码：发送软触发并等待结果回调，超时返回 null。
        /// </summary>
        public override async Task<string> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return "当前设备模拟模式中，触发测试！";

                if (_handle == IntPtr.Zero || !_grabbing)
                {
                    HardwareLogger.Debug("海康SDK扫码枪未连接，无法触发。");
                    return null;
                }

                _resultEvent.Reset();
                _lastCode = string.Empty;

                int ret = HKRobotIdSdk.MV_ID_TriggerSoftwareExecute(_handle);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪软触发失败，错误码={ret}。");
                    return null;
                }

                // 等待结果回调，或触发超时
                Task waitTask = Task.Run(() => _resultEvent.Wait(), token);
                Task timeoutTask = Task.Delay(TimeOutMs, token);
                Task finished = await Task.WhenAny(waitTask, timeoutTask);

                if (finished == waitTask && _resultEvent.IsSet)
                    return _lastCode;

                HardwareLogger.Debug($"海康SDK扫码枪触发取码超时（{TimeOutMs}ms）。");
                return null;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 切换用户参数：导入一份 IDMVS 导出的参数配置文件 (.cfg)。
        /// <para><paramref name="userInfo"/> 为配置文件的绝对路径。</para>
        /// </summary>
        public override async Task<bool> ChangeUserParam(object userInfo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return true;

                if (_handle == IntPtr.Zero)
                    throw new Exception("海康SDK扫码枪未连接，无法切换参数。");

                string cfgPath = userInfo?.ToString();
                if (string.IsNullOrWhiteSpace(cfgPath) || !System.IO.File.Exists(cfgPath))
                    throw new Exception("海康SDK扫码枪切换参数错误：配置文件路径无效。");

                int ret = HKRobotIdSdk.MV_ID_LoadCfg(_handle, cfgPath);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                    throw new Exception($"海康SDK扫码枪导入参数失败，错误码={ret}。");

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }

        #endregion

        #region BaseDevice 钩子

        /// <summary>
        /// 内部连接：枚举设备 → 选定目标 → 创建句柄 → 打开 → 注册结果回调
        /// → 设置软触发模式 → 开始取流。
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                // 1. 枚举 GigE 设备
                var devList = new HKRobotIdSdk.MV_ID_DEVICE_INFO_LIST();
                int ret = HKRobotIdSdk.MV_ID_EnumDevices(HKRobotIdSdk.MV_ID_GIGE_DEVICE, ref devList);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪枚举设备失败，错误码={ret}。");
                    return false;
                }

                if (devList.nDeviceNum == 0 || devList.pDeviceInfo == null || devList.pDeviceInfo.Length == 0)
                {
                    HardwareLogger.Debug("海康SDK扫码枪未发现任何在线读码器。");
                    return false;
                }

                // 2. 选定目标设备（按 IP；未命中或无法判定时回退到首台并告警）
                IntPtr devInfoPtr = SelectDevice(devList);
                if (devInfoPtr == IntPtr.Zero)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪未匹配到 IP={IPAdress} 的设备。");
                    return false;
                }

                // 3. 创建句柄
                ret = HKRobotIdSdk.MV_ID_CreateHandle(out _handle, devInfoPtr);
                if (ret != HKRobotIdSdk.MV_ID_OK || _handle == IntPtr.Zero)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪创建句柄失败，错误码={ret}。");
                    _handle = IntPtr.Zero;
                    return false;
                }

                // 4. 打开设备
                ret = HKRobotIdSdk.MV_ID_OpenDevice(_handle);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪打开设备失败，错误码={ret}。");
                    DestroyHandle();
                    return false;
                }

                // 5. 注册结果回调（保持委托存活）
                _resultCallback = new HKRobotIdSdk.MV_ID_RESULT_CB(OnResultReceived);
                ret = HKRobotIdSdk.MV_ID_RegisterResultCallBack(_handle, _resultCallback, IntPtr.Zero);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪注册结果回调失败，错误码={ret}。");
                    CloseAndDestroy();
                    return false;
                }

                // 6. 软触发模式：TriggerMode=On, TriggerSource=Software
                HKRobotIdSdk.MV_ID_SetEnumValue(_handle, "TriggerMode", HKRobotIdSdk.TRIGGER_MODE_ON);
                HKRobotIdSdk.MV_ID_SetEnumValue(_handle, "TriggerSource", HKRobotIdSdk.TRIGGER_SOURCE_SOFTWARE);

                // 7. 开始取流
                ret = HKRobotIdSdk.MV_ID_StartGrabbing(_handle);
                if (ret != HKRobotIdSdk.MV_ID_OK)
                {
                    HardwareLogger.Debug($"海康SDK扫码枪开始取流失败，错误码={ret}。");
                    CloseAndDestroy();
                    return false;
                }

                _grabbing = true;
                return true;
            }, token);
        }

        /// <summary>内部断开：停止取流 → 关闭设备 → 销毁句柄。</summary>
        protected override async Task InternalDisconnectAsync()
        {
            await Task.Run(() => CloseAndDestroy());
        }

        /// <summary>内部复位：SDK 读码器无机械复位，置空即可。</summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.CompletedTask;

        /// <summary>
        /// 内部健康检查：句柄有效性即代表连接是否存活。
        /// 连接类报警，使用 <see cref="BaseDevice.UpdateAutoClearableHealth"/> 自动防抖消除。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool faulted = _handle == IntPtr.Zero || !_grabbing;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.BarcodeScannerHeartbeatTimeout,
                    $"海康SDK扫码枪[{DeviceName}]连接中断（句柄={_handle != IntPtr.Zero}, 取流={_grabbing}）");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region 私有辅助

        /// <summary>
        /// 结果回调：解析第一条条码字符串并释放等待。
        /// 结构体布局若与真实 SDK 不符，Marshal 会抛异常 —— 此处吞掉异常，
        /// 退化表现为触发取码超时（返回 null），避免崩溃进程。
        /// </summary>
        private int OnResultReceived(IntPtr pstResultOut, IntPtr pUser)
        {
            try
            {
                if (pstResultOut == IntPtr.Zero)
                {
                    _resultEvent.Set();
                    return 0;
                }

                var result = Marshal.PtrToStructure<HKRobotIdSdk.MV_ID_RESULT_OUT>(pstResultOut);
                if (result.nCodeNum > 0 && result.stCodeInfo != null && result.stCodeInfo.Length > 0)
                {
                    string code = result.stCodeInfo[0].chCodeStr;
                    _lastCode = code?.Trim('\0', '\r', '\n', ' ') ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                // 结构体布局与 SDK 不符时在此暴露，便于定位；不中断取流循环。
                HardwareLogger.Debug($"海康SDK扫码枪解析结果异常：{ex.Message}", ex);
            }
            finally
            {
                _resultEvent.Set();
            }

            return 0;
        }

        /// <summary>
        /// 从枚举列表中选定目标设备：优先按 IP 匹配 GigE 设备，
        /// 命中失败或无法解析 IP 时回退到第一台并记录警告。
        /// </summary>
        private IntPtr SelectDevice(HKRobotIdSdk.MV_ID_DEVICE_INFO_LIST devList)
        {
            int count = (int)Math.Min(devList.nDeviceNum, (uint)devList.pDeviceInfo.Length);
            if (count == 0)
                return IntPtr.Zero;

            // 仅一台设备时直接使用
            if (count == 1)
                return devList.pDeviceInfo[0];

            // 多台：尝试按 IP 匹配
            for (int i = 0; i < count; i++)
            {
                if (devList.pDeviceInfo[i] == IntPtr.Zero)
                    continue;

                if (string.IsNullOrEmpty(IPAdress))
                    continue;

                try
                {
                    var info = Marshal.PtrToStructure<HKRobotIdSdk.MV_ID_DEVICE_INFO>(devList.pDeviceInfo[i]);
                    if (info.Body == null)
                        continue;

                    // GigE 设备当前 IP 通常位于 Body 偏移 8（网络字节序，4 字节）。⚠️校对偏移
                    if (TryReadGigeIp(info.Body, 8, out string ip) && ip == IPAdress)
                        return devList.pDeviceInfo[i];
                }
                catch
                {
                    // 结构体解析失败，跳过本台
                }
            }

            // 未按 IP 命中：回退首台并告警（避免静默选错设备）
            HardwareLogger.Debug($"海康SDK扫码枪发现 {count} 台设备，未匹配 IP={IPAdress}，回退使用第一台。");
            return devList.pDeviceInfo[0];
        }

        /// <summary>从缓冲区指定偏移读取 4 字节网络字节序 IP。⚠️校对偏移与字节序。</summary>
        private static bool TryReadGigeIp(byte[] body, int offset, out string ip)
        {
            ip = null;
            if (body == null || offset < 0 || offset + 4 > body.Length)
                return false;

            ip = $"{body[offset]}.{body[offset + 1]}.{body[offset + 2]}.{body[offset + 3]}";
            return true;
        }

        /// <summary>停止取流 + 关闭设备 + 销毁句柄的统一清理。</summary>
        private void CloseAndDestroy()
        {
            _grabbing = false;

            if (_handle != IntPtr.Zero)
            {
                try { HKRobotIdSdk.MV_ID_StopGrabbing(_handle); } catch { /* 忽略 */ }
                try { HKRobotIdSdk.MV_ID_CloseDevice(_handle); } catch { /* 忽略 */ }
                DestroyHandle();
            }

            _resultCallback = null;
            _resultEvent.Reset();
        }

        /// <summary>仅销毁句柄（假定设备已关闭）。</summary>
        private void DestroyHandle()
        {
            if (_handle != IntPtr.Zero)
            {
                try { HKRobotIdSdk.MV_ID_DestroyHandle(_handle); } catch { /* 忽略 */ }
                _handle = IntPtr.Zero;
            }
        }

        #endregion
    }
}
