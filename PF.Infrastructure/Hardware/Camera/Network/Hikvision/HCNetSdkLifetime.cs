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

        /// <summary>
        /// SDK 代次号：每次 <see cref="HCNetSdkApi.NET_DVR_Init"/> 成功后自增。
        /// <para><b>用途是防"句柄串号"</b>：<see cref="HCNetSdkApi.NET_DVR_Cleanup"/> 会杀掉本进程**所有**
        /// 登录会话，但各设备实例手里的 userID 并不会跟着失效——它只是个整数，SDK 重新 Init 后，
        /// 新登录的设备会拿到刚刚空出来的槽位号。此时旧设备拿自己的老 userID 去抓图，
        /// 实际抓的是新设备那台相机的画面，且不报任何错。
        /// 设备在登录时记下当时的代次，抓图前比对：代次变了就说明句柄已失效，必须重新登录。</para>
        /// </summary>
        private static int _epoch;

        /// <summary>当前 SDK 代次号。设备登录时记录，抓图前比对以识别失效句柄。</summary>
        public static int Epoch
        {
            get { lock (_gate) return _epoch; }
        }

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

                        // 新一代 SDK：此前所有登录句柄全部作废（见 Epoch 注释）
                        _epoch++;
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
