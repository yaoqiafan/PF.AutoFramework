namespace PF.Core.Enums
{
    /// <summary>
    /// 条码类型（与具体厂商 SDK 解耦的通用枚举）。
    /// </summary>
    public enum BarcodeType
    {
        /// <summary> 未知/未识别 </summary>
        Unknown,
        /// <summary> DataMatrix 码 </summary>
        DataMatrix,
        /// <summary> QR 码 </summary>
        QRCode,
        /// <summary> Micro QR 码 </summary>
        MicroQRCode,
        /// <summary> EAN8 码 </summary>
        EAN8,
        /// <summary> UPCE 码 </summary>
        UPCE,
        /// <summary> UPCA 码 </summary>
        UPCA,
        /// <summary> EAN13 码 </summary>
        EAN13,
        /// <summary> ISBN13 码 </summary>
        ISBN13,
        /// <summary> 库德巴码 </summary>
        Codabar,
        /// <summary> 交叉25码 </summary>
        Interleaved25,
        /// <summary> Code 39 码 </summary>
        Code39,
        /// <summary> Code 93 码 </summary>
        Code93,
        /// <summary> Code 128 码 </summary>
        Code128,
        /// <summary> PDF417 码 </summary>
        PDF417,
        /// <summary> Matrix25 码 </summary>
        Matrix25,
        /// <summary> MSI 码 </summary>
        MSI,
        /// <summary> Code 11 码 </summary>
        Code11,
        /// <summary> Industrial25 码 </summary>
        Industrial25,
        /// <summary> 中国邮政码 </summary>
        ChinaPost,
        /// <summary> 交叉14码 </summary>
        Interleaved14,
        /// <summary> Pharmacode 码 </summary>
        PharmaCode,
        /// <summary> 二维 Pharmacode 码 </summary>
        PharmaCode2D,
        /// <summary> ECC140 码 </summary>
        ECC140,
        /// <summary> Aztec 码 </summary>
        Aztec,
        /// <summary> 汉信码 </summary>
        HanXin,
    }
}
