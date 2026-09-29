using Keyence.AutoID.SDK;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.Keyence
{
    /// <summary>
    /// 基于基恩士官方 SR 系列读码器 SDK（<see cref="ReaderAccessor"/>，Keyence.AutoID.SDK.dll）的条码扫描枪实现。
    /// <para>与同目录下其他扫码枪实现并列，均继承 <see cref="BaseBarcodeScan"/>，接口/返回结构（<see cref="BarcodeScanResult"/>）
    /// 与海康 <see cref="Hikvision.MvCodeReaderBarcodeScan"/> 保持一致。</para>
    /// <para>DLL 引用来自 SDK 目录 SDK\x64（真正的 x64 构建，与 SampleApplication\...\bin\x86 下的 x86-only 构建不同——
    /// 项目已存在的 LTDMC.dll（运动控制卡驱动）是 x64-only，若误引用 x86 版会在运行时抛 BadImageFormatException）。
    /// DLL 存放于仓库根目录 DLL\Keyence.AutoID.SDK.dll + DLL\Communication.dll（后者是前者的直接依赖）。</para>
    /// <para>⚠️ 该 SDK 本质是基于 TCP 的 ASCII 文本命令/响应协议（默认端口 9004），
    /// 官方 Demo（FirstStepApp）中触发读码使用的命令是 "LON"，其 <c>ExecCommand</c> 调用会同步返回读码结果字符串。
    /// SDK 未提供结构化的条码类型/位置/角度/质量评分（仅 FTP 历史数据 JSON 中含 code corner 位置信息，且无类型/质量字段），
    /// 因此 <see cref="BarcodeInfo"/> 仅填充 <see cref="BarcodeInfo.Code"/>，其余字段保持默认值——
    /// 这与同目录下 <see cref="HKRobot.HKBarcodeScan"/>（TCP 透传协议版）的能力边界一致。</para>
    /// <para>图像获取通过 FTP（<c>OpenFtp/GetFileList/GetFile/CloseFtp</c>）从设备 IMAGE 目录拉取最新一张读码图片（不区分成功/失败图）。
    /// 默认不在 <see cref="Tigger"/> 内执行，按需调用 <see cref="FetchLatestImageAsync"/>；
    /// 将 <see cref="AutoFetchImage"/> 置 true 后，每次触发读到/未读到码都带图返回，与海康实现一致。</para>
    /// </summary>
    public class KeyenceBarcodeScan : BaseBarcodeScan
    {
        /// <summary>触发读码命令（对照官方 FirstStepApp Demo）。</summary>
        private const string TriggerCommand = "LON";

        /// <summary>设备返回的错误标识（对照官方 ErrorImageGetter Demo 中的 CONST_ERROR_STRING）。</summary>
        private const string ErrorSentinel = "ERROR";

        /// <summary>设备内图像存储目录。</summary>
        private const string ImageDirectory = "IMAGE";

        /// <summary>触发后等待设备写出新图的重试次数。</summary>
        private const int ImageFetchRetryCount = 5;

        /// <summary>触发后等待设备写出新图的重试间隔（毫秒）。</summary>
        private const int ImageFetchRetryIntervalMs = 100;

        /// <summary>
        /// 构造基恩士 SR 系列扫码枪。
        /// </summary>
        /// <param name="ip">读码器 IP 地址。</param>
        /// <param name="timeoutms">单次触发取码超时（毫秒）。</param>
        /// <param name="deviceId">设备唯一标识。</param>
        /// <param name="deviceName">设备名称。</param>
        /// <param name="isSimulated">是否仿真模式。</param>
        /// <param name="logger">日志服务。</param>
        public KeyenceBarcodeScan(string ip, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            IPAdress = ip ?? string.Empty;
            TimeOutMs = timeoutms > 0 ? timeoutms : 5000;
        }

        /// <summary>读码器 IP 地址。</summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 触发端口（本 SDK 通过 <see cref="ReaderAccessor.CommandPort"/> 管理，固定返回 0，仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员）。
        /// </summary>
        public override int TiggerPort => 0;

        /// <summary>
        /// 用户端口（本 SDK 不区分独立管理端口，固定返回 0，仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员）。
        /// </summary>
        public override int UserPort => 0;

        /// <summary>单次触发取码超时（毫秒）。</summary>
        public override int TimeOutMs { get; }

        // -------- SDK 设备实例与连接状态 --------

        /// <summary>SDK 设备访问实例。</summary>
        private ReaderAccessor _reader;

        /// <summary>是否已成功 Connect。</summary>
        private volatile bool _connected;

        #region IBarcodeScan

        /// <summary>
        /// 触发时是否顺带通过 FTP 拉取本次触发的图像（默认 false，避免每次触发都产生 FTP 往返）。
        /// <para>置 true 后读到/未读到码都带图返回，与海康 MvCodeReader 行为一致；为 false 时可按需调用 <see cref="FetchLatestImageAsync"/>。</para>
        /// <para>⚠️ 需在读码器侧开启"图像保存"且包含读码失败(NG)图像，否则未读到码时拿不到本次图像。</para>
        /// </summary>
        public bool AutoFetchImage { get; set; } = false;

        /// <summary>上一次成功拉取的图像文件名，用于判断设备是否产生了新图，避免把旧图当成本次结果返回。</summary>
        private string _lastFetchedImageFile;

        /// <summary>
        /// 触发扫码：发送 "LON" 命令并同步等待设备返回读码结果。
        /// 本 SDK 不提供条码类型/位置/角度/质量评分，<see cref="BarcodeInfo"/> 仅填充 <see cref="BarcodeInfo.Code"/>。
        /// <para>设备返回 "ERROR"（未读到码）时视为触发成功、条码为空集合；只有无响应/命令错误才返回失败。</para>
        /// <para><see cref="AutoFetchImage"/> 为 true 时，读到/未读到码都会附带本次图像。</para>
        /// </summary>
        public override async Task<BarcodeScanResult> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return BarcodeScanResult.Success(new[] { "当前设备模拟模式中，触发测试！" });

                if (_reader == null || !_connected)
                {
                    HardwareLogger.Debug("基恩士扫码枪未连接，无法触发。");
                    return BarcodeScanResult.Fail("基恩士扫码枪未连接，无法触发。");
                }

                // 清掉上一次的图像，避免本次未取到图时误返回旧图
                ClearLastImage();

                string response = await Task.Run(() => _reader.ExecCommand(TriggerCommand, TimeOutMs), token);

                if (string.IsNullOrWhiteSpace(response))
                {
                    HardwareLogger.Debug($"基恩士扫码枪触发取码超时或无响应（{TimeOutMs}ms）。");
                    return BarcodeScanResult.Fail($"基恩士扫码枪触发取码超时或无响应（{TimeOutMs}ms）。");
                }

                var lines = response
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .ToList();

                // "ER,命令,错误码" 为命令执行错误（非读码结果），属于真正的失败
                if (lines.Any(line => line.StartsWith("ER,", StringComparison.OrdinalIgnoreCase)))
                {
                    HardwareLogger.Debug($"基恩士扫码枪触发命令执行错误：{response.Trim()}");
                    return BarcodeScanResult.Fail($"基恩士扫码枪触发命令执行错误：{response.Trim()}");
                }

                // "ERROR" 为未读到码（多码读取时可能部分行为 ERROR），过滤掉后剩下的即有效条码
                var codes = lines
                    .Where(line => !string.Equals(line, ErrorSentinel, StringComparison.OrdinalIgnoreCase))
                    .Select(code => new BarcodeInfo { Code = code })
                    .ToList();

                if (codes.Count == 0)
                    HardwareLogger.Debug($"基恩士扫码枪本次触发未识别到条码，返回空条码，原始响应：{response.Trim()}");

                if (AutoFetchImage)
                {
                    bool gotImage = await FetchLatestImageAsync(token);
                    if (!gotImage)
                        HardwareLogger.Debug("基恩士扫码枪本次触发未取到新图像（请确认读码器已开启图像保存且包含NG图像）。");
                }

                return BarcodeScanResult.Success(codes, LastImageData, LastImageWidth, LastImageHeight, LastImagePixelFormat);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return BarcodeScanResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 切换用户参数：本 SDK 未提供结构化的"参数组切换"接口，此处将 <paramref name="userInfo"/>
        /// 作为原始命令字符串直接下发（<see cref="ReaderAccessor.ExecCommand(string, int)"/>）。
        /// <para>⚠️ 具体命令字符串需按目标设备实际支持的命令集自行拼装，本方法仅做透传。</para>
        /// </summary>
        public override async Task<bool> ChangeUserParam(object userInfo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return true;

                if (_reader == null || !_connected)
                    throw new Exception("基恩士扫码枪未连接，无法切换参数。");

                string command = userInfo?.ToString();
                if (string.IsNullOrWhiteSpace(command))
                    throw new Exception("基恩士扫码枪切换参数错误：命令字符串无效。");

                string response = await Task.Run(() => _reader.ExecCommand(command, TimeOutMs), token);
                if (response != null && response.IndexOf(ErrorSentinel, StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception($"基恩士扫码枪切换参数失败：{response.Trim()}");

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

        /// <summary>内部连接：建立 TCP 连接（含异步推送数据回调，用于诊断日志）。</summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                _reader = new ReaderAccessor(IPAdress);

                bool ok = _reader.Connect(OnDataReceived);
                if (!ok)
                {
                    HardwareLogger.Debug($"基恩士扫码枪连接失败，LastErrorInfo={_reader.LastErrorInfo}。");
                    _reader = null;
                    return false;
                }

                _connected = true;
                return true;
            }, token);
        }

        /// <summary>内部断开：断开 TCP 连接。</summary>
        protected override async Task InternalDisconnectAsync()
        {
            await Task.Run(() =>
            {
                _connected = false;
                if (_reader != null)
                {
                    try { _reader.Disconnect(); } catch { /* 忽略 */ }
                    _reader = null;
                }
            });
        }

        /// <summary>内部复位：读码器无机械复位，置空即可。</summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.CompletedTask;

        /// <summary>
        /// 内部健康检查：连接状态即代表设备是否存活。
        /// 连接类报警，使用 <see cref="BaseDevice.UpdateAutoClearableHealth"/> 自动防抖消除。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool faulted = _reader == null || !_connected;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.BarcodeScannerHeartbeatTimeout,
                    $"基恩士扫码枪[{DeviceName}]连接中断（已连接={_connected}）");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region 私有辅助

        /// <summary>
        /// 异步推送数据回调：设备侧（如硬件触发/连续模式）主动上报的读码事件。
        /// 本类采用软触发 + <see cref="ReaderAccessor.ExecCommand(string, int)"/> 同步取结果的模式，
        /// 此回调仅用于诊断日志，不驱动 <see cref="Tigger"/> 的返回。
        /// </summary>
        private void OnDataReceived(byte[] data)
        {
            try
            {
                string text = Encoding.ASCII.GetString(data);
                HardwareLogger.Debug($"基恩士扫码枪异步推送数据：{text.Trim()}");
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug($"基恩士扫码枪解析异步推送数据异常：{ex.Message}", ex);
            }
        }

        #endregion

        #region 图像获取（按需，非 Tigger 内自动执行）

        /// <summary>
        /// 通过 FTP 从设备 IMAGE 目录拉取最新一张读码图片（不区分成功 "_S_" / 失败图，保证未读到码时也拿到本次图像），
        /// 更新 <c>LastImageData</c>/<c>LastImageWidth</c>/<c>LastImageHeight</c>/<c>LastImagePixelFormat</c>（Jpeg）。
        /// <para>若最新文件与上次拉取的相同（设备未产生新图），视为无新图返回 false，不更新图像。</para>
        /// 涉及额外的 FTP 网络往返。
        /// </summary>
        public override async Task<bool> FetchLatestImageAsync(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated || _reader == null || !_connected)
                    return false;

                return await Task.Run(() =>
                {
                    if (!_reader.OpenFtp())
                    {
                        HardwareLogger.Debug("基恩士扫码枪打开FTP失败，无法获取图像。");
                        return false;
                    }

                    try
                    {
                        // LON 返回后设备可能还在写图，短暂重试等待新文件出现
                        string targetFile = null;
                        for (int attempt = 0; attempt < ImageFetchRetryCount; attempt++)
                        {
                            token.ThrowIfCancellationRequested();

                            targetFile = GetNewestImageFile();
                            if (!string.IsNullOrEmpty(targetFile) && targetFile != _lastFetchedImageFile)
                                break;

                            targetFile = null;
                            Thread.Sleep(ImageFetchRetryIntervalMs);
                        }

                        if (string.IsNullOrEmpty(targetFile))
                            return false;

                        string tempPath = Path.Combine(Path.GetTempPath(), $"keyence_{DeviceId}_{Guid.NewGuid():N}.jpg");
                        try
                        {
                            if (!_reader.GetFile(ImageDirectory + "\\" + targetFile, tempPath))
                            {
                                HardwareLogger.Debug($"基恩士扫码枪下载图像文件失败：{targetFile}");
                                return false;
                            }

                            byte[] bytes = File.ReadAllBytes(tempPath);
                            using (var img = Image.FromFile(tempPath))
                            {
                                LastImageWidth = img.Width;
                                LastImageHeight = img.Height;
                            }
                            LastImageData = bytes;
                            LastImagePixelFormat = BarcodeImagePixelFormat.Jpeg;
                            _lastFetchedImageFile = targetFile;
                            return true;
                        }
                        finally
                        {
                            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* 忽略 */ }
                        }
                    }
                    finally
                    {
                        try { _reader.CloseFtp(); } catch { /* 忽略 */ }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug($"基恩士扫码枪获取图像异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取 IMAGE 目录下最新的一张图片文件名（需在 OpenFtp 之后调用）。
        /// 设备文件名带时间序号，按名称倒序即时间倒序（沿用原实现的排序假设）。
        /// </summary>
        private string GetNewestImageFile()
        {
            List<string> files = _reader.GetFileList(ImageDirectory);
            if (files == null || files.Count == 0)
                return null;

            return files
                .Where(f => !string.Equals(f, "LIVE.BIN", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(f, "EXTRALIVE.BIN", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>清空上一次的图像缓存。</summary>
        private void ClearLastImage()
        {
            LastImageData = Array.Empty<byte>();
            LastImageWidth = 0;
            LastImageHeight = 0;
            LastImagePixelFormat = BarcodeImagePixelFormat.Unknown;
        }

        #endregion
    }
}
