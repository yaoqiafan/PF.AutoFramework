using System.Runtime.InteropServices;

namespace PF.Infrastructure.Hardware.Camera.Network.Hikvision
{
    /// <summary>
    /// 海康 HCNetSDK 原生函数 P/Invoke 声明（只声明本框架用到的，全量声明见 SDK Demo 的 CHCNetSDK.cs）。
    ///
    /// <para><b>DllImport 一律用纯文件名</b>，不用 ".\HCNetSDK.dll"（依赖进程当前工作目录，
    /// WPF 打开过文件对话框后工作目录会漂移，是极隐蔽的偶发加载失败），
    /// 更不用绝对路径。纯文件名的解析顺序：已加载模块 → 应用目录（AppContext.BaseDirectory）→ 系统目录，稳定。</para>
    ///
    /// <para>SDK 仅有 Win64 版本（HCNetSDK.dll / HCCore.dll 的 PE Machine = 0x8664），
    /// 进程必须以 x64 运行（AnyCPU 在 64 位 Windows 上默认如此）；若改成 x86 / Prefer32Bit
    /// 会立刻 BadImageFormatException。部署要求见 PF.Infrastructure.csproj 内注释。</para>
    /// </summary>
    internal static partial class HCNetSdkApi
    {
        /// <summary>初始化 SDK（进程级，全局一次，由 HCNetSdkLifetime 引用计数管理）。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_Init();

        /// <summary>反初始化 SDK（进程级，退出时，由 HCNetSdkLifetime 引用计数管理）。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_Cleanup();

        // 刻意不声明 NET_DVR_SetLogToFile：SDK 自身日志会持续写盘且不在框架日志管理范围内，
        // 现场排查需要时再按 SDK 文档临时加回来，避免留下永远不调用的死声明。

        /// <summary>设置连接设备超时时间与重试次数（登录前调用，全局生效）。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_SetConnectTime(uint dwWaitTime, uint dwTryTimes);

        /// <summary>设置断线自动重连间隔与开关（全局生效）。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_SetReconnect(uint dwInterval, int bEnableRecon);

        /// <summary>用户登录 V40（当前推荐版本，替代 V30）。失败返回 -1，错误码查 <see cref="NET_DVR_GetLastError"/>。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern int NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO pLoginInfo, ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

        /// <summary>注销登录。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_Logout(int iUserID);

        /// <summary>
        /// 抓图到内存（JPEG）。缓冲区由调用方预分配，SDK 没有"先查询所需大小"的接口，
        /// 缓冲区不足时返回失败且错误码为 43。
        /// </summary>
        [DllImport("HCNetSDK.dll")]
        public static extern bool NET_DVR_CaptureJPEGPicture_NEW(
            int lUserID, int lChannel, ref NET_DVR_JPEGPARA lpJpegPara,
            byte[] sJpegPicBuffer, uint dwPicSize, ref uint lpSizeReturned);

        /// <summary>获取最近一次 SDK 调用的错误码。</summary>
        [DllImport("HCNetSDK.dll")]
        public static extern uint NET_DVR_GetLastError();
    }
}
