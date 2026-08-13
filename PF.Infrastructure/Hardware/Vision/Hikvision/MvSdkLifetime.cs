using MvCameraControl;

namespace PF.Infrastructure.Hardware.Vision.Hikvision
{
    /// <summary>
    /// 海康 MVS SDK 进程级生命周期管理（引用计数）。
    ///
    /// <para><see cref="SDKSystem.Initialize"/> / <see cref="SDKSystem.Finalize"/> 是**进程级**的，
    /// 官方示例都是单设备程序，直接在 Main 里成对调用即可。但框架里同时存在多台设备
    /// （采集卡 + 挂在其下的相机，乃至多路相机），若每台设备各自在连接时 Initialize、
    /// 断开时 Finalize，先断开的那台会把整个 SDK 关掉，导致其余设备的句柄集体失效。</para>
    ///
    /// <para>因此统一由本类做引用计数：第一台设备连接时初始化，最后一台断开时才反初始化。</para>
    /// </summary>
    internal static class MvSdkLifetime
    {
        private static readonly object _gate = new();
        private static int _refCount;

        /// <summary>获取一次 SDK 引用（首次调用时执行 SDKSystem.Initialize）。</summary>
        public static void Acquire()
        {
            lock (_gate)
            {
                if (_refCount == 0)
                    SDKSystem.Initialize();

                _refCount++;
            }
        }

        /// <summary>
        /// 释放一次 SDK 引用（计数归零时执行 SDKSystem.Finalize）。
        /// 允许重复调用：计数已为 0 时直接返回，不会把计数压成负数。
        /// </summary>
        public static void Release()
        {
            lock (_gate)
            {
                if (_refCount == 0) return;

                _refCount--;
                if (_refCount == 0)
                    SDKSystem.Finalize();
            }
        }
    }
}
