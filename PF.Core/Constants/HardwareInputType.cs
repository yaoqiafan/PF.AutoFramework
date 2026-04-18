namespace PF.Core.Constants
{
    /// <summary>
    /// 硬件输入类型字符串常量。
    /// 取代原 PhysicalButtonType 枚举，使外部工站可自由扩展而无需修改 PF.Core。
    /// </summary>
    public static class HardwareInputType
    {
        /// <summary>启动按钮</summary>
        public const string Start        = "Start";
        /// <summary>暂停按钮</summary>
        public const string Pause        = "Pause";
        /// <summary>复位按钮</summary>
        public const string Reset        = "Reset";
        /// <summary>急停按钮</summary>
        public const string EStop        = "EStop";
        /// <summary>安全门</summary>
        public const string SafeDoor     = "SafeDoor";
        /// <summary>光幕</summary>
        public const string LightCurtain = "LightCurtain";
    }
}
