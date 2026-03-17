namespace PF.Core.Enums
{
    /// <summary>
    /// 硬件设备分类枚举
    /// </summary>
    public enum HardwareCategory
    {
        /// <summary> 未分类/通用设备 </summary>
        General,
        /// <summary> 运动控制轴 (伺服/步进) </summary>
        Axis,
        /// <summary> IO 控制器 (板卡/PLC的IO模块) </summary>
        IOController,
        /// <summary> 视觉相机 </summary>
        Camera,
        /// <summary> 机械臂/机器人 </summary>
        Robot,
        /// <summary> 扫码枪/读卡器 </summary>
        Scanner,
        /// <summary> 仪表仪器 (万用表/测厚仪等) </summary>
        Instrument,
        /// <summary> 运动控制卡（管理多轴和IO的物理板卡）</summary>
        MotionCard,
        /// <summary> 光源控制器 </summary>
        LightController,
    }
}