namespace PF.Core.Interfaces.TowerLight
{
    /// <summary>
    /// 三色灯 DO 点写入抽象。
    /// 将逻辑 tag（字符串标识）映射到物理 IO 板卡端口并执行写入，
    /// 使 TowerLightService 与具体硬件型号完全解耦。
    /// </summary>
    public interface ITowerLightDoWriter
    {
        /// <summary>向指定逻辑 tag 对应的 DO 点写入值</summary>
        /// <param name="tag">逻辑标识，如 "Red"、"Green"（由配置层定义）</param>
        /// <param name="value">true = 输出高（亮/鸣叫），false = 输出低（灭/静止）</param>
        void Write(string tag, bool value);
    }
}
