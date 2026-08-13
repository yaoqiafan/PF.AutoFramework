namespace PF.Core.Enums.Hardware.Vision
{
    /// <summary>
    /// 相机传输层类型。由枚举结果判定，用于决定帧控制策略落在采集卡还是相机自身。
    /// </summary>
    public enum CameraTransportLayer
    {
        /// <summary>未知</summary>
        Unknown = 0,
        /// <summary>千兆网口（含 GenTL GigE）</summary>
        GigE,
        /// <summary>USB3 Vision</summary>
        Usb,
        /// <summary>CameraLink（必须经采集卡）</summary>
        CameraLink,
        /// <summary>CoaXPress（必须经采集卡）</summary>
        CoaXPress,
        /// <summary>XoF（必须经采集卡）</summary>
        XoF,
    }
}
