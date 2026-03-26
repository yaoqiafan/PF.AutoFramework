namespace PF.Core.Constants
{
    /// <summary>
    /// 硬件输入类型字符串常量。
    /// 取代原 PhysicalButtonType 枚举，使外部工站可自由扩展而无需修改 PF.Core。
    /// </summary>
    public static class HardwareInputType
    {
        public const string Start        = "Start";
        public const string Pause        = "Pause";
        public const string Reset        = "Reset";
        public const string EStop        = "EStop";
        public const string SafeDoor     = "SafeDoor";
        public const string LightCurtain = "LightCurtain";
    }
}
