using PF.Core.Entities.Hardware.Vision;

namespace PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera
{
    /// <summary>
    /// 网络摄像机（海康 HCNetSDK 私有协议，IPC / NVR）。
    ///
    /// <para>与 <see cref="IntelligentCamera.IIntelligentCamera"/> 的区别：智能相机是
    /// "触发一次 → 相机内部跑完算法 → 返回结果字符串"的语义（如基恩士 OCR 相机）；
    /// 网络摄像机吐的是**整幅图像**（JPEG），算法在上位机侧做，两者不复用同一接口。</para>
    ///
    /// <para>与 <see cref="LineScan.ILineScanCamera"/> 的区别：线阵相机吐原始像素流、
    /// 配合运动轴扫描才能拼出完整图像；网络摄像机是面阵设备，单次抓取即得整幅图片，
    /// 不感知任何运动。因此本接口没有开流/帧触发/编码器概念，只有"登录 → 抓图"。</para>
    ///
    /// <para>抓图结果为 JPEG 字节而非解码后的像素：设备层只交原样数据（与
    /// <see cref="LineScanFrame"/> 同一约定），转位图/存盘/送视觉库全由调用方决定。</para>
    /// </summary>
    public interface INetworkCamera : IHardwareDevice
    {
        /// <summary>设备 IP 地址（不含端口）。</summary>
        string IPAddress { get; }

        /// <summary>SDK 私有协议端口，海康默认 8000。</summary>
        int Port { get; }

        /// <summary>登录用户名。</summary>
        string UserName { get; }

        /// <summary>
        /// 设备序列号（登录成功后由 NET_DVR_DEVICEINFO_V30.sSerialNumber 填充；
        /// 未登录时为空字符串）。
        /// </summary>
        string SerialNumber { get; }

        /// <summary>
        /// 可用通道列表（登录成功后填充；NVR 下含模拟通道与 IP 通道，IPC 通常只有一个）。
        /// 未登录时为空集合。
        /// </summary>
        IReadOnlyList<NetworkCameraChannel> Channels { get; }

        /// <summary>
        /// 当前抓图使用的通道号（登录后为设备枚举/配置指定的通道；未登录时为 0）。
        /// </summary>
        int ActiveChannel { get; }

        /// <summary>
        /// 抓取一帧 JPEG 图片。
        /// <para>成功返回 JPEG 字节（含完整文件头尾，可直接写盘或交位图解码）；
        /// 失败抛 <see cref="InvalidOperationException"/>（含 SDK 错误码的中文描述）；
        /// 取消抛 <see cref="OperationCanceledException"/>。</para>
        /// </summary>
        /// <param name="channel">通道号；null 表示使用 <see cref="ActiveChannel"/>。</param>
        /// <param name="token">取消令牌。</param>
        Task<NetworkCameraFrame> CaptureJpegAsync(int? channel = null, CancellationToken token = default);

        /// <summary>存图格式（设备配置项；未配置时为 <see cref="Enums.Hardware.Vision.ImageFileFormat.Jpeg"/>）。</summary>
        Enums.Hardware.Vision.ImageFileFormat SaveFormat { get; }

        /// <summary>
        /// 存图根目录（设备配置项）。未配置时为
        /// <c>{ConfigPath}\NetworkCamera\{DeviceId}</c>。
        /// </summary>
        string SaveDirectory { get; }

        /// <summary>
        /// 抓图并直接落盘：SDK 抓到内存中的 JPEG 按 <see cref="SaveFormat"/> 转码后写文件。
        /// <para>文件名格式 <c>{通道}_{yyyyMMdd_HHmmss_fff}.{扩展名}</c>，落在 <see cref="SaveDirectory"/> 下。</para>
        /// <para>存储格式为 Jpeg 时<b>不重新编码</b>，直接把 SDK 返回的字节写盘（避免二次有损压缩）；
        /// 其余格式经 System.Drawing 解码再编码。</para>
        /// </summary>
        /// <param name="channel">通道号；null 表示使用 <see cref="ActiveChannel"/>。</param>
        /// <param name="fileNameWithoutExtension">
        /// 可选的文件名主体（不含扩展名）。传 null 时用 <c>{通道}_{时间戳}</c>；
        /// 批量采集需要业务侧命名时（如条码、批号）可传自定义值，非法文件名字符会被替换为下划线。
        /// </param>
        /// <param name="token">取消令牌。</param>
        /// <returns>实际写入的完整文件路径。</returns>
        /// <exception cref="InvalidOperationException">抓图失败或写盘失败。</exception>
        Task<string> CaptureAndSaveAsync(int? channel = null, string? fileNameWithoutExtension = null,
            CancellationToken token = default);
    }
}
