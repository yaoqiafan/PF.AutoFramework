using System.Runtime.InteropServices;

namespace PF.Infrastructure.Hardware.Camera.Network.Hikvision
{
    /// <summary>
    /// HCNetSDK 结构体定义。<b>照抄 SDK Demo（CHCNetSDK.cs，SDK V6.1.11.5）的尺寸与排布，不要凭记忆改</b>——
    /// 登录/抓图结构体里固定长度数组与保留字节的总长错一个，封送就会错位，
    /// 轻则登录失败、重则进程崩溃（老项目 ef066e7 的 byRes 封送崩溃就是这么来的）。
    /// </summary>
    internal static class HCNetSdkStructs
    {
        /// <summary>序列号字段长度</summary>
        public const int SERIALNO_LEN = 48;

        /// <summary>登录设备地址最大长度</summary>
        public const int NET_DVR_DEV_ADDRESS_MAX_LEN = 129;

        /// <summary>登录用户名最大长度</summary>
        public const int NET_DVR_LOGIN_USERNAME_MAX_LEN = 64;

        /// <summary>登录密码最大长度</summary>
        public const int NET_DVR_LOGIN_PASSWD_MAX_LEN = 64;
    }

    /// <summary>设备信息 V30（V40 的内嵌部分，通道号/序列号都在这里）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_DEVICEINFO_V30
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = HCNetSdkStructs.SERIALNO_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] sSerialNumber;                // 序列号
        public byte byAlarmInPortNum;               // 报警输入个数
        public byte byAlarmOutPortNum;              // 报警输出个数
        public byte byDiskNum;                      // 硬盘个数
        public byte byDVRType;                      // 设备类型
        public byte byChanNum;                      // 模拟通道个数
        public byte byStartChan;                    // 起始通道号（IPC 通常为 1）
        public byte byAudioChanNum;                 // 语音通道数
        public byte byIPChanNum;                    // 数字通道个数，低位
        public byte byZeroChanNum;                  // 零通道编码个数
        public byte byMainProto;                    // 主码流传输协议类型
        public byte bySubProto;                     // 子码流传输协议类型
        public byte bySupport;                      // 能力集
        public byte bySupport1;                     // 能力集扩充 1
        public byte bySupport2;                     // 能力集扩充 2
        public ushort wDevType;                     // 设备型号
        public byte bySupport3;                     // 能力集扩充 3
        public byte byMultiStreamProto;             // 是否支持多码流
        public byte byStartDChan;                   // 起始数字通道号，0 表示无效
        public byte byStartDTalkChan;               // 起始数字对讲通道号，0 表示无效
        public byte byHighDChanNum;                 // 数字通道个数，高位（与 byIPChanNum 拼接：总数 = 低位 + 256 × 高位）
        public byte bySupport4;
        public byte byLanguageType;                 // 支持语种能力
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes2;
    }

    /// <summary>设备信息 V40（NET_DVR_Login_V40 的出参）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_DEVICEINFO_V40
    {
        public NET_DVR_DEVICEINFO_V30 struDeviceV30;
        public byte bySupportLock;                  // 设备是否支持锁定功能
        public byte byRetryLoginTime;               // 剩余可尝试登录次数（密码错误时有效）
        public byte byPasswordLevel;                // admin 密码安全等级
        public byte byProxyType;                    // 代理类型
        public uint dwSurplusLockTime;              // 用户锁定剩余时间（秒）
        public byte byCharEncodeType;               // 字符编码类型
        public byte bySupportDev5;                  // 是否支持 v50 版本设备参数获取
        public byte bySupport;                      // 能力集扩展
        public byte byLoginMode;                    // 登录模式 0-Private 1-ISAPI
        public int dwOEMCode;
        public int iResidualValidity;               // 该用户密码剩余有效天数
        public byte byResidualValidity;             // iResidualValidity 字段是否有效
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 243, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes2;                       // ← 必须分配 243 字节，否则 ref 封送时长度不符
    }

    /// <summary>异步登录结果回调（本框架只用同步登录，此委托仅为结构体封送完整而保留，始终为 null）。</summary>
    internal delegate void LOGINRESULTCALLBACK(int lUserID, int dwResult, IntPtr lpDeviceInfo, IntPtr pUser);

    /// <summary>登录入参（NET_DVR_Login_V40）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_USER_LOGIN_INFO
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = HCNetSdkStructs.NET_DVR_DEV_ADDRESS_MAX_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] sDeviceAddress;
        public byte byUseTransport;
        public ushort wPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = HCNetSdkStructs.NET_DVR_LOGIN_USERNAME_MAX_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] sUserName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = HCNetSdkStructs.NET_DVR_LOGIN_PASSWD_MAX_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] sPassword;
        public LOGINRESULTCALLBACK? cbLoginResult;
        public IntPtr pUser;
        public bool bUseAsynLogin;
        public byte byProxyType;
        public byte byUseUTCTime;
        public byte byLoginMode;                    // 0-Private 1-ISAPI 2-自适应
        public byte byHttps;                        // 0-不使用 TLS 1-使用 TLS 2-自适应
        public int iProxyID;
        public byte byVerifyMode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 119, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes3;
    }

    /// <summary>JPEG 抓图参数。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NET_DVR_JPEGPARA
    {
        /// <summary>0=CIF 1=QCIF 2=D1 3=UXGA 4=SVGA 5=HD720p 6=VGA…；0xFF = 自动按当前码流分辨率抓图。</summary>
        public ushort wPicSize;

        /// <summary>图片质量：0-最好 1-较好 2-一般。</summary>
        public ushort wPicQuality;
    }
}
