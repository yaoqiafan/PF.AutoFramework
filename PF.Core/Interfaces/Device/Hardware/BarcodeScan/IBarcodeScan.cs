using PF.Core.Enums;
using PF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Device.Hardware.BarcodeScan
{
    /// <summary>
    /// 条码扫描枪接口
    /// </summary>
    public interface IBarcodeScan : IHardwareDevice
    {


        /// <summary>
        /// 扫码枪IP地址
        /// </summary>
        string IPAdress { get; }

        /// <summary>
        /// 最近一次触发采集到的原始图像数据。不支持图像采集的扫码枪固定返回空数组。
        /// </summary>
        byte[] LastImageData { get; }

        /// <summary>
        /// 最近一帧图像宽度。不支持图像采集的扫码枪固定返回 0。
        /// </summary>
        int LastImageWidth { get; }

        /// <summary>
        /// 最近一帧图像高度。不支持图像采集的扫码枪固定返回 0。
        /// </summary>
        int LastImageHeight { get; }

        /// <summary>
        /// 最近一帧图像的像素/编码格式。不支持图像采集的扫码枪固定返回 <see cref="BarcodeImagePixelFormat.Unknown"/>。
        /// </summary>
        BarcodeImagePixelFormat LastImagePixelFormat { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        int TiggerPort { get; }


        /// <summary>
        /// 管理端口
        /// </summary>
        int UserPort { get; }



        /// <summary>
        /// 扫码枪通讯超时时间，单位毫秒
        /// </summary>
        int TimeOutMs { get ; }


        /// <summary>
        /// 触发扫码枪：返回本次触发识别到的条码列表与采集到的图像信息。
        /// </summary>
        /// <returns>详细扫码结果，见 <see cref="BarcodeScanResult"/></returns>
        Task<BarcodeScanResult> Tigger(CancellationToken token =default );


        /// <summary>
        /// 修改扫码枪用户参数集
        /// </summary>
        /// <param name="UserInfo">用户集</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> ChangeUserParam(object UserInfo, CancellationToken token = default);

    }
}
