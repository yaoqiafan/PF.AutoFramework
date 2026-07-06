using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan
{
    /// <summary>
    /// 扫码器基类
    /// </summary>
    public abstract class BaseBarcodeScan : BaseDevice, IBarcodeScan
    {
        /// <summary>
        /// 构造扫码器
        /// </summary>
        protected  BaseBarcodeScan(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Scanner;
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public abstract string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        public abstract  int TiggerPort { get; }

        /// <summary>
        /// 用户端口
        /// </summary>
        public abstract    int UserPort{ get; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public abstract  int TimeOutMs { get; }

        /// <summary>
        /// 最近一次触发采集到的原始图像数据。默认实现返回空数组；
        /// 支持图像采集的子类（如 <see cref="Hikvision.MvCodeReaderBarcodeScan"/>）在回调中写入 protected setter。
        /// </summary>
        public byte[] LastImageData { get; protected set; } = Array.Empty<byte>();

        /// <summary>最近一帧图像宽度。默认 0（不支持图像采集）。</summary>
        public int LastImageWidth { get; protected set; }

        /// <summary>最近一帧图像高度。默认 0（不支持图像采集）。</summary>
        public int LastImageHeight { get; protected set; }

        /// <summary>最近一帧图像的像素/编码格式。默认 <see cref="BarcodeImagePixelFormat.Unknown"/>。</summary>
        public BarcodeImagePixelFormat LastImagePixelFormat { get; protected set; }

        /// <summary>
        /// 切换用户参数
        /// </summary>
        public abstract Task<bool> ChangeUserParam(object UserInfo,CancellationToken token = default);
        /// <summary>
        /// 触发扫码
        /// </summary>
        public abstract Task<string> Tigger(CancellationToken token = default);


    }
}
