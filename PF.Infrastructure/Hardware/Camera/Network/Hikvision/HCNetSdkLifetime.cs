namespace PF.Infrastructure.Hardware.Camera.Network.Hikvision
{
    /// <summary>
    /// HCNetSDK 进程级生命周期管理（引用计数），对标 <see cref="Vision.Hikvision.MvSdkLifetime"/>。
    ///
    /// <para><see cref="HCNetSdkApi.NET_DVR_Init"/> / <see cref="HCNetSdkApi.NET_DVR_Cleanup"/> 是**进程级**的，
    /// 若每台相机各自在连接时 Init、断开时 Cleanup，先断开的那台会把整个 SDK 关掉，
    /// 其余相机的登录句柄集体失效。统一由本类做引用计数：第一台设备连接时初始化，
    /// 最后一台断开时才反初始化。</para>
    ///
    /// <para>初始化失败直接抛异常（让 BaseDevice 的重试机制接住并报警），
    /// 而不是像老项目那样静默吞掉——Init 失败后所有登录都会返回错误码 3，
    /// 现场看到的是"登录失败"，排查方向全错。</para>
    /// </summary>
    internal static class HCNetSdkLifetime
    {
        private static readonly object _gate = new();
        private static int _refCount;

        /// <summary>获取一次 SDK 引用（首次调用时 NET_DVR_Init 并设置超时/重连参数）。</summary>
        /// <exception cref="InvalidOperationException">SDK 初始化失败，或原生库未部署。</exception>
        public static void Acquire()
        {
            lock (_gate)
            {
                if (_refCount == 0)
                {
                    try
                    {
                        if (!HCNetSdkApi.NET_DVR_Init())
                            throw new InvalidOperationException(
                                $"HCNetSDK 初始化失败: {HCNetSdkError.GetLastError()}");

                        // 显式设置连接超时/重试与断线重连：不设时 SDK 用默认值，
                        // 网络异常的设备会让登录阻塞过久
                        HCNetSdkApi.NET_DVR_SetConnectTime(5000, 1);
                        HCNetSdkApi.NET_DVR_SetReconnect(10000, 1);
                    }
                    catch (DllNotFoundException ex)
                    {
                        throw new InvalidOperationException(
                            "未找到 HCNetSDK 原生库（HCNetSDK.dll 及 HCNetSDKCom\\ 子目录）。" +
                            "部署方法见 PF.Infrastructure.csproj 内注释：从 DLL\\Infrastructure\\HCNetSDK\\ " +
                            "复制全部 35 个文件到程序输出目录，且必须保持 HCNetSDKCom 子目录层级。", ex);
                    }
                }
                _refCount++;
            }
        }

        /// <summary>
        /// 释放一次引用（计数归零时 NET_DVR_Cleanup）。
        /// 允许重复调用：计数已为 0 时直接返回，不会把计数压成负数。
        /// </summary>
        public static void Release()
        {
            lock (_gate)
            {
                if (_refCount == 0) return;

                _refCount--;
                if (_refCount == 0)
                    HCNetSdkApi.NET_DVR_Cleanup();
            }
        }
    }
}
