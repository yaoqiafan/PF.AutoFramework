using MvCodeReaderSDKNet;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.Hikvision
{
    /// <summary>
    /// 基于海康官方 MvCodeReaderSDK 托管封装（<see cref="MvCodeReader"/>，MvCodeReaderSDK.Net.dll）的条码扫描枪实现。
    /// <para>与同目录下其他扫码枪实现并列，均继承 <see cref="BaseBarcodeScan"/>。
    /// 直接引用官方托管 SDK（DLL 位于仓库根目录 DLL\MvCodeReaderSDK.Net.dll，来自随 SDK 发布的
    /// C:\Users\26040\Desktop\C#\BasicDemo\bin\win64），由官方封装负责结构体 Marshal，
    /// 避免手写 P/Invoke 时对未公开结构体字段（保留字节长度等）的猜测风险。</para>
    /// <para>取帧方式采用官方 Demo 中的图像回调 <see cref="MvCodeReader.cbOutputEx2delegate"/>
    /// （<c>MV_CODEREADER_RegisterImageCallBackEx2_NET</c>），而非后台轮询线程。</para>
    /// <para>⚠️ 部署要求：MvCodeReaderSDK.Net.dll 内部仍会 P/Invoke 原生 MvCodeReaderCtrl.dll，
    /// 需将 SDK 目录 win64 下的 MvCodeReaderCtrl.dll 及其依赖库一并复制到程序输出目录，否则运行时加载失败。</para>
    /// </summary>
    public class MvCodeReaderBarcodeScan : BaseBarcodeScan
    {
        /// <summary>
        /// 构造海康 MvCodeReaderSDK 扫码枪。
        /// </summary>
        /// <param name="ip">读码器 IP 地址（用于从枚举结果中选定设备）。</param>
        /// <param name="timeoutms">单次触发取码超时（毫秒）。</param>
        /// <param name="deviceId">设备唯一标识。</param>
        /// <param name="deviceName">设备名称。</param>
        /// <param name="isSimulated">是否仿真模式。</param>
        /// <param name="logger">日志服务。</param>
        public MvCodeReaderBarcodeScan(string ip, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            IPAdress = ip ?? string.Empty;
            TimeOutMs = timeoutms > 0 ? timeoutms : 5000;
        }

        /// <summary>读码器 IP 地址。</summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 触发端口（本 SDK 不基于端口通信，固定返回 0，仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员）。
        /// </summary>
        public override int TiggerPort => 0;

        /// <summary>
        /// 用户端口（本 SDK 不基于端口通信，固定返回 0，仅为满足 <see cref="BaseBarcodeScan"/> 抽象成员）。
        /// </summary>
        public override int UserPort => 0;

        /// <summary>单次触发取码超时（毫秒）。</summary>
        public override int TimeOutMs { get; }

        /// <summary>
        /// 最近一次触发采集到的全部条码详细信息（一帧可能包含多个条码）。
        /// </summary>
        public IReadOnlyList<BarcodeInfo> LastCodes { get; private set; } = Array.Empty<BarcodeInfo>();

        // 图像数据（LastImageData / LastImageWidth / LastImageHeight / LastImagePixelFormat）
        // 已提升为 IBarcodeScan 接口成员，定义于 BaseBarcodeScan（protected set），此处仅在回调中写入。

        // -------- SDK 设备实例与回调状态 --------

        /// <summary>SDK 设备实例。</summary>
        private MvCodeReader _device;

        /// <summary>是否已 StartGrabbing。</summary>
        private volatile bool _grabbing;

        /// <summary>图像回调委托，必须作为字段持有，防止 GC 回收导致回调时崩溃。</summary>
        private MvCodeReader.cbOutputEx2delegate _imageCallback;

        /// <summary>触发取码完成信号。</summary>
        private readonly ManualResetEventSlim _resultEvent = new ManualResetEventSlim(false);

        #region IBarcodeScan

        /// <summary>
        /// 触发扫码：发送软触发并等待图像回调采集到条码，返回条码列表与本次采集到的图像信息。
        /// </summary>
        public override async Task<BarcodeScanResult> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return BarcodeScanResult.Success(new[] { "当前设备模拟模式中，触发测试！" });

                if (_device == null || !_grabbing)
                {
                    HardwareLogger.Debug("海康MvCodeReader扫码枪未连接，无法触发。");
                    return BarcodeScanResult.Fail("海康MvCodeReader扫码枪未连接，无法触发。");
                }

                _resultEvent.Reset();
                LastCodes = Array.Empty<BarcodeInfo>();

                int ret = _device.MV_CODEREADER_SetCommandValue_NET("TriggerSoftware");
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪软触发失败，错误码={ret}。");
                    return BarcodeScanResult.Fail($"海康MvCodeReader扫码枪软触发失败，错误码={ret}。");
                }

                Task waitTask = Task.Run(() => _resultEvent.Wait(), token);
                Task timeoutTask = Task.Delay(TimeOutMs, token);
                Task finished = await Task.WhenAny(waitTask, timeoutTask);

                if (finished == waitTask && _resultEvent.IsSet)
                    return BarcodeScanResult.Success(LastCodes, LastImageData, LastImageWidth, LastImageHeight, LastImagePixelFormat);

                HardwareLogger.Debug($"海康MvCodeReader扫码枪触发取码超时（{TimeOutMs}ms）。");
                return BarcodeScanResult.Fail($"海康MvCodeReader扫码枪触发取码超时（{TimeOutMs}ms）。");
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return BarcodeScanResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 切换用户参数：加载相机内的用户配置组（GenICam 标准 UserSetSelector/UserSetLoad 节点）。
        /// <para>⚠️ 假设设备暴露标准 GenICam 用户参数组节点，<paramref name="userInfo"/> 为用户组序号（0~3 等，因型号而异）。</para>
        /// </summary>
        public override async Task<bool> ChangeUserParam(object userInfo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                    return true;

                if (_device == null)
                    throw new Exception("海康MvCodeReader扫码枪未连接，无法切换参数。");

                if (!int.TryParse(userInfo?.ToString(), out int userSetIndex))
                    throw new Exception("海康MvCodeReader扫码枪切换参数错误：用户组序号无效。");

                int ret = _device.MV_CODEREADER_SetEnumValue_NET("UserSetSelector", (uint)userSetIndex);
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                    throw new Exception($"海康MvCodeReader扫码枪设置用户组序号失败，错误码={ret}。");

                ret = _device.MV_CODEREADER_SetCommandValue_NET("UserSetLoad");
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                    throw new Exception($"海康MvCodeReader扫码枪加载用户组失败，错误码={ret}。");

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
        /// 内部连接：枚举设备 → 选定目标 → 创建句柄 → 打开 → 注册图像回调
        /// → 设置软触发模式 → 开始取流。
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                var devList = new MvCodeReader.MV_CODEREADER_DEVICE_INFO_LIST();
                int ret = MvCodeReader.MV_CODEREADER_EnumDevices_NET(ref devList, MvCodeReader.MV_CODEREADER_GIGE_DEVICE);
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪枚举设备失败，错误码={ret}。");
                    return false;
                }

                if (devList.nDeviceNum == 0)
                {
                    HardwareLogger.Debug("海康MvCodeReader扫码枪未发现任何在线读码器。");
                    return false;
                }

                MvCodeReader.MV_CODEREADER_DEVICE_INFO stDevInfo = SelectDevice(devList);

                _device = new MvCodeReader();

                ret = _device.MV_CODEREADER_CreateHandle_NET(ref stDevInfo);
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪创建句柄失败，错误码={ret}。");
                    _device = null;
                    return false;
                }

                ret = _device.MV_CODEREADER_OpenDevice_NET();
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪打开设备失败，错误码={ret}。");
                    DestroyHandle();
                    return false;
                }

                _imageCallback = new MvCodeReader.cbOutputEx2delegate(OnImageCallback);
                ret = _device.MV_CODEREADER_RegisterImageCallBackEx2_NET(_imageCallback, IntPtr.Zero);
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪注册图像回调失败，错误码={ret}。");
                    CloseAndDestroy();
                    return false;
                }

                _device.MV_CODEREADER_SetEnumValue_NET("TriggerMode", (uint)MvCodeReader.MV_CODEREADER_TRIGGER_MODE.MV_CODEREADER_TRIGGER_MODE_ON);
                _device.MV_CODEREADER_SetEnumValue_NET("TriggerSource", (uint)MvCodeReader.MV_CODEREADER_TRIGGER_SOURCE.MV_CODEREADER_TRIGGER_SOURCE_SOFTWARE);

                ret = _device.MV_CODEREADER_StartGrabbing_NET();
                if (ret != MvCodeReader.MV_CODEREADER_OK)
                {
                    HardwareLogger.Debug($"海康MvCodeReader扫码枪开始取流失败，错误码={ret}。");
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

        /// <summary>内部复位：读码器无机械复位，置空即可。</summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.CompletedTask;

        /// <summary>
        /// 内部健康检查：设备实例与取流状态即代表连接是否存活。
        /// 连接类报警，使用 <see cref="BaseDevice.UpdateAutoClearableHealth"/> 自动防抖消除。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool faulted = _device == null || !_grabbing;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.BarcodeScannerHeartbeatTimeout,
                    $"海康MvCodeReader扫码枪[{DeviceName}]连接中断（设备={_device != null}, 取流={_grabbing}）");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region 私有辅助：图像回调

        /// <summary>
        /// 图像回调：拷贝最新图像，解析全部条码。
        /// 官方托管封装已完成结构体 Marshal（含定长数组），此处直接整体 PtrToStructure，无需手工按偏移解析。
        /// </summary>
        private void OnImageCallback(IntPtr pData, IntPtr pstFrameInfoEx2, IntPtr pUser)
        {
            try
            {
                if (pData == IntPtr.Zero || pstFrameInfoEx2 == IntPtr.Zero)
                    return;

                var frameInfo = (MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2)Marshal.PtrToStructure(
                    pstFrameInfoEx2, typeof(MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2));

                if (frameInfo.nFrameLen <= 0)
                    return;

                int frameLen = (int)frameInfo.nFrameLen;
                byte[] imageData = new byte[frameLen];
                Marshal.Copy(pData, imageData, 0, frameLen);

                LastImageData = imageData;
                LastImageWidth = frameInfo.nWidth;
                LastImageHeight = frameInfo.nHeight;
                LastImagePixelFormat = MapPixelFormat(frameInfo.enPixelType);

                if (!frameInfo.bIsGetCode || frameInfo.UnparsedBcrList.pstCodeListEx2 == IntPtr.Zero)
                    return;

                var bcrResult = (MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2)Marshal.PtrToStructure(
                    frameInfo.UnparsedBcrList.pstCodeListEx2, typeof(MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2));

                if (bcrResult.nCodeNum <= 0 || bcrResult.stBcrInfoEx2 == null)
                    return;

                var codes = new List<BarcodeInfo>((int)bcrResult.nCodeNum);
                for (int i = 0; i < bcrResult.nCodeNum && i < bcrResult.stBcrInfoEx2.Length; i++)
                {
                    var bcrInfo = bcrResult.stBcrInfoEx2[i];
                    string code = DecodeCodeString(bcrInfo.chCode, (int)bcrInfo.nLen);
                    if (string.IsNullOrEmpty(code))
                        continue;

                    codes.Add(new BarcodeInfo
                    {
                        Code = code,
                        BarType = MapBarType((MvCodeReader.MV_CODEREADER_CODE_TYPE)bcrInfo.nBarType),
                        Position = MapPosition(bcrInfo.pt),
                        Angle = bcrInfo.nAngle / 10.0,
                        QualityScore = bcrInfo.stCodeQuality.nOverQuality,
                        ReadScore = (int)bcrInfo.nIDRScore
                    });
                }

                if (codes.Count > 0)
                {
                    LastCodes = codes;
                    _resultEvent.Set();
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug($"海康MvCodeReader扫码枪解析图像回调异常：{ex.Message}", ex);
            }
        }

        /// <summary>将条码原始字节按 UTF-8 解码为字符串，仅截取有效长度并去除首尾空白/结束符。</summary>
        private static string DecodeCodeString(byte[] chCode, int nLen)
        {
            if (chCode == null || chCode.Length == 0 || nLen <= 0)
                return string.Empty;

            int len = Math.Min(nLen, chCode.Length);
            return Encoding.UTF8.GetString(chCode, 0, len).Trim('\0', '\r', '\n', ' ');
        }

        /// <summary>将 SDK 专属像素格式映射为与厂商解耦的通用 <see cref="BarcodeImagePixelFormat"/>。</summary>
        private static BarcodeImagePixelFormat MapPixelFormat(MvCodeReader.MvCodeReaderGvspPixelType sdkPixelType)
        {
            return sdkPixelType switch
            {
                MvCodeReader.MvCodeReaderGvspPixelType.PixelType_CodeReader_Gvsp_Mono8 => BarcodeImagePixelFormat.Mono8,
                MvCodeReader.MvCodeReaderGvspPixelType.PixelType_CodeReader_Gvsp_Jpeg => BarcodeImagePixelFormat.Jpeg,
                _ => BarcodeImagePixelFormat.Unknown
            };
        }

        /// <summary>将 SDK 专属条码类型映射为与厂商解耦的通用 <see cref="BarcodeType"/>。</summary>
        private static BarcodeType MapBarType(MvCodeReader.MV_CODEREADER_CODE_TYPE sdkBarType)
        {
            return sdkBarType switch
            {
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_DM => BarcodeType.DataMatrix,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_QR => BarcodeType.QRCode,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_MICROQR => BarcodeType.MicroQRCode,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_EAN8 => BarcodeType.EAN8,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_UPCE => BarcodeType.UPCE,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_UPCA => BarcodeType.UPCA,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_EAN13 => BarcodeType.EAN13,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_ISBN13 => BarcodeType.ISBN13,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CODABAR => BarcodeType.Codabar,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_ITF25 => BarcodeType.Interleaved25,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CODE39 => BarcodeType.Code39,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CODE93 => BarcodeType.Code93,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CODE128 => BarcodeType.Code128,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_PDF417 => BarcodeType.PDF417,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_MATRIX25 => BarcodeType.Matrix25,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_MSI => BarcodeType.MSI,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CODE11 => BarcodeType.Code11,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_INDUSTRIAL25 => BarcodeType.Industrial25,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_CHINAPOST => BarcodeType.ChinaPost,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_ITF14 => BarcodeType.Interleaved14,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_PHARMACODE => BarcodeType.PharmaCode,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_BCR_PHARMACODE2D => BarcodeType.PharmaCode2D,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_ECC140 => BarcodeType.ECC140,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_AZTEC => BarcodeType.Aztec,
                MvCodeReader.MV_CODEREADER_CODE_TYPE.MV_CODEREADER_TDCR_HANXIN => BarcodeType.HanXin,
                _ => BarcodeType.Unknown
            };
        }

        /// <summary>将 SDK 条码角点数组映射为通用 <see cref="BarcodePoint"/> 列表。</summary>
        private static IReadOnlyList<BarcodePoint> MapPosition(MvCodeReader.MV_CODEREADER_POINT_I[] pt)
        {
            if (pt == null || pt.Length == 0)
                return Array.Empty<BarcodePoint>();

            var points = new List<BarcodePoint>(pt.Length);
            foreach (var p in pt)
                points.Add(new BarcodePoint { X = p.x, Y = p.y });
            return points;
        }

        #endregion

        #region 私有辅助：设备选择与清理

        /// <summary>
        /// 从枚举列表中选定目标设备：优先按 IP 匹配 GigE 设备，
        /// 命中失败或无法解析 IP 时回退到第一台并记录警告。
        /// </summary>
        private MvCodeReader.MV_CODEREADER_DEVICE_INFO SelectDevice(MvCodeReader.MV_CODEREADER_DEVICE_INFO_LIST devList)
        {
            int count = (int)devList.nDeviceNum;

            for (int i = 0; i < count; i++)
            {
                if (devList.pDeviceInfo[i] == IntPtr.Zero || string.IsNullOrEmpty(IPAdress))
                    continue;

                var info = (MvCodeReader.MV_CODEREADER_DEVICE_INFO)Marshal.PtrToStructure(
                    devList.pDeviceInfo[i], typeof(MvCodeReader.MV_CODEREADER_DEVICE_INFO));

                if (info.nTLayerType != MvCodeReader.MV_CODEREADER_GIGE_DEVICE)
                    continue;

                IntPtr gigeBuffer = Marshal.UnsafeAddrOfPinnedArrayElement(info.SpecialInfo.stGigEInfo, 0);
                var gigeInfo = (MvCodeReader.MV_CODEREADER_GIGE_DEVICE_INFO)Marshal.PtrToStructure(
                    gigeBuffer, typeof(MvCodeReader.MV_CODEREADER_GIGE_DEVICE_INFO));

                if (FormatIp(gigeInfo.nCurrentIp) == IPAdress)
                    return info;
            }

            if (count > 1)
                HardwareLogger.Debug($"海康MvCodeReader扫码枪发现 {count} 台设备，未匹配 IP={IPAdress}，回退使用第一台。");

            return (MvCodeReader.MV_CODEREADER_DEVICE_INFO)Marshal.PtrToStructure(
                devList.pDeviceInfo[0], typeof(MvCodeReader.MV_CODEREADER_DEVICE_INFO));
        }

        /// <summary>将网络字节序的 32 位整型 IP 格式化为点分十进制字符串。</summary>
        private static string FormatIp(uint ip)
        {
            return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
        }

        /// <summary>停止取流 + 关闭设备 + 销毁句柄的统一清理。</summary>
        private void CloseAndDestroy()
        {
            _grabbing = false;

            if (_device != null)
            {
                try { _device.MV_CODEREADER_StopGrabbing_NET(); } catch { /* 忽略 */ }
                try { _device.MV_CODEREADER_CloseDevice_NET(); } catch { /* 忽略 */ }
                DestroyHandle();
            }

            _imageCallback = null;
            _resultEvent.Reset();
        }

        /// <summary>仅销毁句柄（假定设备已关闭）。</summary>
        private void DestroyHandle()
        {
            if (_device != null)
            {
                try { _device.MV_CODEREADER_DestroyHandle_NET(); } catch { /* 忽略 */ }
                _device = null;
            }
        }

        #endregion

        #region 图像转换 / 保存

        /// <summary>
        /// 将 <c>LastImageData</c> 转换并保存为 PNG 文件。目标目录不存在时自动创建。
        /// 支持 Mono8（灰度）与 Jpeg（相机端压缩输出）两种像素格式，其余格式暂不支持。
        /// </summary>
        public bool SaveLastImageAsPng(string filePath)
        {
            try
            {
                if (LastImageData == null || LastImageData.Length == 0)
                {
                    HardwareLogger.Debug("海康MvCodeReader扫码枪保存图像失败：当前没有可用的图像数据。");
                    return false;
                }

                using Bitmap bitmap = LastImagePixelFormat switch
                {
                    BarcodeImagePixelFormat.Mono8 => BuildMono8Bitmap(LastImageData, LastImageWidth, LastImageHeight),
                    BarcodeImagePixelFormat.Jpeg => DecodeJpegBitmap(LastImageData),
                    _ => throw new NotSupportedException($"暂不支持将像素格式 {LastImagePixelFormat} 转换为 PNG。")
                };

                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                bitmap.Save(filePath, ImageFormat.Png);
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug($"海康MvCodeReader扫码枪保存图像异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>将 Mono8 灰度原始缓冲区构造为可保存的位图。</summary>
        private static Bitmap BuildMono8Bitmap(byte[] data, int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            ColorPalette palette = bitmap.Palette;
            for (int i = 0; i < 256; i++)
                palette.Entries[i] = Color.FromArgb(i, i, i);
            bitmap.Palette = palette;

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    IntPtr rowPtr = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
                    Marshal.Copy(data, y * width, rowPtr, width);
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
            return bitmap;
        }

        /// <summary>将相机端压缩输出的 Jpeg 原始字节解码为位图。</summary>
        private static Bitmap DecodeJpegBitmap(byte[] data)
        {
            using var ms = new MemoryStream(data);
            return new Bitmap(Image.FromStream(ms));
        }

        #endregion
    }
}
