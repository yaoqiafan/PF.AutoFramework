using PF.Core.Enums;
using System;
using System.Collections.Generic;

namespace PF.Core.Models
{
    /// <summary>
    /// 条码位置坐标点。
    /// </summary>
    public class BarcodePoint
    {
        /// <summary>列坐标</summary>
        public int X { get; set; }

        /// <summary>行坐标</summary>
        public int Y { get; set; }
    }

    /// <summary>
    /// 单个条码的详细信息（内容 + 类型 + 位置 + 角度 + 质量评价 + 读码评分）。
    /// 不支持这些细节的扫码枪（如 TCP 透传协议版）仅填充 <see cref="Code"/>，其余字段保持默认值。
    /// </summary>
    public class BarcodeInfo
    {
        /// <summary>条码字符内容</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>条码类型。不支持识别类型的扫码枪固定为 <see cref="BarcodeType.Unknown"/>。</summary>
        public BarcodeType BarType { get; set; } = BarcodeType.Unknown;

        /// <summary>条码位置（通常为 4 个角点，按设备坐标系）。不支持定位的扫码枪固定为空集合。</summary>
        public IReadOnlyList<BarcodePoint> Position { get; set; } = Array.Empty<BarcodePoint>();

        /// <summary>条码角度（单位：度，0~360）。不支持的扫码枪固定为 0。</summary>
        public double Angle { get; set; }

        /// <summary>条码质量评价（等级 0~4，越高质量越好）。不支持质量评级的扫码枪固定为 0。</summary>
        public int QualityScore { get; set; }

        /// <summary>读码评分。不支持的扫码枪固定为 0。</summary>
        public int ReadScore { get; set; }
    }
}
