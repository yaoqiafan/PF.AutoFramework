using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PF.Core.Models
{
    /// <summary>
    /// 扫码枪单次触发的详细结果：识别到的条码详细信息列表 + 本次采集到的图像信息。
    /// </summary>
    public class BarcodeScanResult
    {
        /// <summary>是否成功读到至少一个条码</summary>
        public bool IsSuccess { get; set; }

        /// <summary>本次触发识别到的全部条码详细信息（一帧可能包含多个条码，未读到时为空集合）</summary>
        public IReadOnlyList<BarcodeInfo> Codes { get; set; } = Array.Empty<BarcodeInfo>();

        /// <summary>全部条码内容以 <c>&amp;</c> 拼接的字符串，兼容既有 <c>Split('&amp;')</c> 调用方式</summary>
        public string CodesText => string.Join('&', Codes.Select(c => c.Code));

        /// <summary>失败时的描述信息</summary>
        public string ErrorMessage { get; set; }

        /// <summary>本次触发采集到的原始图像数据；不支持图像采集的扫码枪固定为空数组</summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        /// <summary>图像宽度</summary>
        public int ImageWidth { get; set; }

        /// <summary>图像高度</summary>
        public int ImageHeight { get; set; }

        /// <summary>图像像素/编码格式</summary>
        public BarcodeImagePixelFormat ImagePixelFormat { get; set; }

        /// <summary>创建成功结果</summary>
        public static BarcodeScanResult Success(IReadOnlyList<BarcodeInfo> codes, byte[] imageData = null,
            int imageWidth = 0, int imageHeight = 0, BarcodeImagePixelFormat imagePixelFormat = BarcodeImagePixelFormat.Unknown)
            => new()
            {
                IsSuccess = true,
                Codes = codes ?? Array.Empty<BarcodeInfo>(),
                ImageData = imageData ?? Array.Empty<byte>(),
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                ImagePixelFormat = imagePixelFormat
            };

        /// <summary>
        /// 创建成功结果：仅有纯文本条码内容（不支持类型/位置/角度/质量等细节的扫码枪使用）。
        /// </summary>
        public static BarcodeScanResult Success(IEnumerable<string> plainCodes)
            => Success((plainCodes ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => new BarcodeInfo { Code = c })
                .ToList());

        /// <summary>创建失败结果</summary>
        public static BarcodeScanResult Fail(string errorMessage) => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
