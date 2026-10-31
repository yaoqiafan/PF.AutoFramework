using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;
using PF.Core.Interfaces.Logging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace PF.Infrastructure.Hardware.Camera.Network
{
    /// <summary>
    /// 网络摄像机抽象基类：承载连接参数、通道/序列号等与厂商无关的状态，
    /// 以及仿真模式下的合成图生成。登录/抓图的 SDK 细节由厂商子类实现
    /// （同一约定见 <see cref="LineScan.BaseLineScanCamera"/> → HikLineScanCamera 的分层）。
    /// </summary>
    public abstract class BaseNetworkCamera : BaseDevice, INetworkCamera
    {
        /// <summary>
        /// 构造网络摄像机基类。构造函数只存参数，不做任何设备解析/连接
        /// （连接延迟到 <see cref="BaseDevice.InternalConnectAsync"/>，遵循框架约定）。
        /// </summary>
        /// <param name="ip">设备 IP（不含端口）</param>
        /// <param name="port">SDK 私有协议端口（&lt;=0 时按海康默认 8000）</param>
        /// <param name="userName">登录用户名</param>
        /// <param name="password">登录密码（仅设备层可见，接口不暴露）</param>
        /// <param name="channel">配置指定的通道号，0 = 自动选第一个可用通道</param>
        /// <param name="captureBufferMb">抓图缓冲区起始大小（MB，&lt;1 时按 8）</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备显示名称</param>
        /// <param name="isSimulated">是否仿真模式</param>
        /// <param name="logger">日志服务</param>
        /// <param name="saveFormat">存图格式（默认 JPEG）</param>
        /// <param name="saveDirectory">
        /// 存图根目录；null/空白时用 <c>{ConfigPath}\NetworkCamera\{deviceId}</c>。
        /// <para>注意不能在静态字段初始化器里取 <see cref="ConstGlobalParam.ConfigPath"/>——
        /// 会先于 <c>Initialize</c> 求值抛 TypeInitializationException；此处是构造函数，安全。</para>
        /// </param>
        protected BaseNetworkCamera(string ip, int port, string userName, string password,
            int channel, int captureBufferMb,
            string deviceId, string deviceName, bool isSimulated, ILogService logger,
            ImageFileFormat saveFormat = ImageFileFormat.Jpeg, string? saveDirectory = null)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            IPAddress = ip ?? string.Empty;
            Port = port > 0 ? port : 8000;
            UserName = userName ?? string.Empty;
            Password = password ?? string.Empty;
            ConfiguredChannel = channel > 0 ? channel : 0;
            CaptureBufferBytes = (captureBufferMb > 0 ? captureBufferMb : 8) * 1024 * 1024;

            SaveFormat    = saveFormat;
            SaveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
                ? Path.Combine(ConstGlobalParam.ConfigPath, "NetworkCamera", deviceId)
                : saveDirectory!;

            Category = HardwareCategory.Camera;
        }

        /// <summary>设备 IP 地址（不含端口）。</summary>
        public string IPAddress { get; }

        /// <summary>SDK 私有协议端口。</summary>
        public int Port { get; }

        /// <summary>登录用户名。</summary>
        public string UserName { get; }

        /// <summary>登录密码（仅设备层内部使用，INetworkCamera 刻意不暴露）。</summary>
        protected string Password { get; }

        /// <summary>配置指定的通道号；0 = 自动选第一个可用通道。</summary>
        protected int ConfiguredChannel { get; }

        /// <summary>抓图缓冲区起始大小（字节）。SDK 无"查询所需大小"接口，不足时由子类按错误码 43 翻倍重试。</summary>
        protected int CaptureBufferBytes { get; }

        /// <inheritdoc/>
        public ImageFileFormat SaveFormat { get; }

        /// <inheritdoc/>
        public string SaveDirectory { get; }

        private string _serialNumber = string.Empty;
        /// <inheritdoc/>
        public string SerialNumber
        {
            get => _serialNumber;
            protected set
            {
                _serialNumber = value;
                RaisePropertyChanged();
            }
        }

        private IReadOnlyList<NetworkCameraChannel> _channels = Array.Empty<NetworkCameraChannel>();
        /// <inheritdoc/>
        public IReadOnlyList<NetworkCameraChannel> Channels
        {
            get => _channels;
            protected set
            {
                _channels = value ?? Array.Empty<NetworkCameraChannel>();
                RaisePropertyChanged();
            }
        }

        private int _activeChannel;
        /// <inheritdoc/>
        public int ActiveChannel
        {
            get => _activeChannel;
            protected set
            {
                _activeChannel = value;
                RaisePropertyChanged();
            }
        }

        /// <inheritdoc/>
        public abstract Task<NetworkCameraFrame> CaptureJpegAsync(int? channel = null, CancellationToken token = default);

        /// <summary>
        /// 抓图并直接落盘。厂商子类不必各自实现——所有厂商都走"抓 JPEG 字节 → 按配置格式写盘"这一条路。
        /// </summary>
        public virtual async Task<string> CaptureAndSaveAsync(int? channel = null,
            string? fileNameWithoutExtension = null, CancellationToken token = default)
        {
            var frame = await CaptureJpegAsync(channel, token).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(SaveDirectory);

                    string name = string.IsNullOrWhiteSpace(fileNameWithoutExtension)
                        ? $"{frame.Channel}_{frame.Timestamp:yyyyMMdd_HHmmss_fff}"
                        : SanitizeFileName(fileNameWithoutExtension!);

                    string path = Path.Combine(SaveDirectory, name + GetExtension(SaveFormat));

                    // JPEG 不重新编码：SDK 返回的就是 JPEG 字节，再解一遍只是白搭一次有损压缩
                    if (SaveFormat == ImageFileFormat.Jpeg)
                    {
                        File.WriteAllBytes(path, frame.Data);
                    }
                    else
                    {
                        using var ms = new MemoryStream(frame.Data);
                        using var bitmap = new Bitmap(ms);
                        bitmap.Save(path, GetImageFormat(SaveFormat));
                    }

                    HardwareLogger.Info($"[{DeviceName}] 抓图已存盘: {path}（{frame.SizeBytes / 1024.0:F1} KB，{SaveFormat}）");
                    return path;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"相机[{DeviceName}]存图失败（目录 {SaveDirectory}，格式 {SaveFormat}）: {ex.Message}", ex);
                }
            }, token).ConfigureAwait(false);
        }

        /// <summary>存图格式 → 文件扩展名。</summary>
        private static string GetExtension(ImageFileFormat format) => format switch
        {
            ImageFileFormat.Bmp  => ".bmp",
            ImageFileFormat.Tiff => ".tif",
            ImageFileFormat.Png  => ".png",
            _                    => ".jpg",
        };

        /// <summary>存图格式 → System.Drawing 编码格式（仅非 JPEG 路径使用）。</summary>
        private static ImageFormat GetImageFormat(ImageFileFormat format) => format switch
        {
            ImageFileFormat.Bmp  => ImageFormat.Bmp,
            ImageFileFormat.Tiff => ImageFormat.Tiff,
            ImageFileFormat.Png  => ImageFormat.Png,
            _                    => ImageFormat.Jpeg,
        };

        /// <summary>替换文件名字符集里的非法字符（外部传入的批号/条码可能带 / : * 等）。</summary>
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars   = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }

        /// <summary>
        /// 仿真连接：填充一个虚拟通道与序列号，让调试页在无硬件时也有完整可演示的状态。
        /// </summary>
        protected override async Task<bool> InternalConnectSimulatedAsync(CancellationToken token)
        {
            await base.InternalConnectSimulatedAsync(token);

            SerialNumber = "SIM-000000";
            Channels = new[]
            {
                new NetworkCameraChannel { ChannelNo = 1, Name = "模拟通道 1" }
            };
            ActiveChannel = ConfiguredChannel > 0 ? ConfiguredChannel : 1;
            return true;
        }

        /// <summary>
        /// 生成一张合成 JPEG（仿真模式抓图用）：画上设备名/通道/时间戳，
        /// 让"抓图 → 预览"全链路在无硬件时也能验证。
        /// </summary>
        protected static byte[] BuildSimulatedJpeg(string deviceName, int channel)
        {
            using var bitmap = new Bitmap(1280, 720);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(24, 32, 48));
                using var titleFont = new Font("Microsoft YaHei UI", 42, FontStyle.Bold);
                using var bodyFont = new Font("Microsoft YaHei UI", 24);
                g.DrawString("网络摄像机 · 仿真模式", titleFont, Brushes.White, 60, 200);
                g.DrawString($"设备: {deviceName}", bodyFont, Brushes.LightGray, 60, 320);
                g.DrawString($"通道: {channel}", bodyFont, Brushes.LightGray, 60, 380);
                g.DrawString($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}", bodyFont, Brushes.LightGray, 60, 440);
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        /// <summary>把字符串按 ASCII 编码进固定长度缓冲区（超出截断，余位补 0）。</summary>
        protected static byte[] BuildFixedBytes(string text, int length)
        {
            var buffer = new byte[length];
            var bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
            Array.Copy(bytes, buffer, Math.Min(bytes.Length, length - 1));
            return buffer;
        }

        /// <summary>读取 SDK 固定长度字节串（在首个 0x00 处截断，序列号等均为 ASCII）。</summary>
        protected static string ReadFixedString(byte[]? buffer)
        {
            if (buffer is null || buffer.Length == 0) return string.Empty;

            int end = Array.IndexOf(buffer, (byte)0);
            return Encoding.ASCII.GetString(buffer, 0, end < 0 ? buffer.Length : end).Trim();
        }
    }
}
