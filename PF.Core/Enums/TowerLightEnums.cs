namespace PF.Core.Enums
{
    /// <summary>三色灯/蜂鸣器通道标识</summary>
    public enum LightColor
    {
        Red,
        Yellow,
        Green,
        Buzzer
    }

    /// <summary>灯光/蜂鸣器工作状态</summary>
    public enum LightState
    {
        /// <summary>关闭</summary>
        Off,
        /// <summary>常亮/持续鸣叫</summary>
        On,
        /// <summary>软件频闪/间歇鸣叫</summary>
        Blinking
    }
}
