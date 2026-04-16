using PF.Core.Enums;

namespace PF.Core.Events
{
    /// <summary>
    /// 子工站状态机发生跳转时的事件参数。
    /// 由 <c>StationBase.StationStateChanged</c> 事件携带，
    /// 供 <c>BaseMasterController</c> 实施防撕裂守卫逻辑。
    /// </summary>
    public class StationStateChangedEventArgs : EventArgs
    {
        /// <summary>跳转前的旧状态</summary>
        public MachineState OldState { get; init; }

        /// <summary>跳转后的新状态</summary>
        public MachineState NewState { get; init; }
    }
}
