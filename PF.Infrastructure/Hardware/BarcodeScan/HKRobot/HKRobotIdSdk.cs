using System;
using System.Runtime.InteropServices;

namespace PF.Infrastructure.Hardware.BarcodeScan.HKRobot
{
    // =====================================================================================
    //  海康机器人 ID 读码器 官方 SDK (MvIdApi) 的 P/Invoke 封装。
    //
    //  角色定位：对标同目录下 CardAPI/LTMC.cs（雷赛运动卡 SDK 封装）。
    //           把原生 C API 集中暴露在一个静态类里，上层设备类 (HKSdkBarcodeScan)
    //           只依赖本类，不直接散落 DllImport，便于 SDK 版本升级时统一替换。
    //
    //  ⚠️  重要（上线前必读）：
    //     本文件中的 DLL 文件名、入口函数名、结构体字段与布局，均依据海康
    //     《机器视觉智能读码器 C API 开发参考 (MvIdApi)》公开资料整理，属于
    //     "代表性"签名。请在编译/上机前，对照你本机 IDMVS 安装目录下的
    //         C:\Program Files (x86)\MVS\IDMVS\Development\Includes\MvIdApi.h
    //     逐项校对：
    //       1) DllName        —— 实际动态库名（通常为 MvIdApi.dll，部分版本叫 MvIdApiWrapper）；
    //       2) 函数 EntryPoint —— 函数名大小写、下划线需完全一致；
    //       3) 结构体布局      —— SizeConst / 字段顺序 / 联合体偏移，错误会导致内存读取错乱；
    //       4) 部署            —— 将 MvIdApi.dll 及其依赖 DLL 拷贝到程序输出目录
    //                            （与 LTDMC.dll 的部署方式一致）。
    //     凡标注 ⚠️校对 的位置，均为需要按真实头文件确认之处。
    // =====================================================================================

    /// <summary>
    /// 海康机器人 ID 读码器 (MvIdApi) 原生接口封装。
    /// </summary>
    public static class HKRobotIdSdk
    {
        /// <summary>SDK 动态库名。⚠️校对：以实际安装目录的 DLL 名为准。</summary>
        public const string DllName = "MvIdApi.dll";

        /// <summary>调用成功返回值（MV_OK）。</summary>
        public const int MV_ID_OK = 0;

        /// <summary>GigE 接口设备。⚠️校对：MvIdApi.h 中 MV_ID_GIGE_DEVICE 的值。</summary>
        public const uint MV_ID_GIGE_DEVICE = 0x01;

        /// <summary>USB3 接口设备。⚠️校对：MvIdApi.h 中 MV_ID_USB_DEVICE 的值。</summary>
        public const uint MV_ID_USB_DEVICE = 0x02;

        /// <summary>枚举设备列表中最多可返回的设备数。⚠️校对：MV_ID_MAX_DEVICE_NUM。</summary>
        public const int MAX_DEVICE_INFO_NUM = 16;

        /// <summary>TriggerMode 枚举值：关闭（连续模式）。</summary>
        public const uint TRIGGER_MODE_OFF = 0;

        /// <summary>TriggerMode 枚举值：打开（触发模式）。</summary>
        public const uint TRIGGER_MODE_ON = 1;

        /// <summary>TriggerSource 枚举值：软触发。⚠️校对：MvIdApi.h 中 Software 的序号。</summary>
        public const uint TRIGGER_SOURCE_SOFTWARE = 7;

        #region 设备信息结构体

        /// <summary>设备枚举结果列表。⚠️校对：MV_ID_DEVICE_INFO_LIST。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MV_ID_DEVICE_INFO_LIST
        {
            /// <summary>发现的设备数量。</summary>
            public uint nDeviceNum;

            /// <summary>设备信息指针数组（每个元素指向一个 MV_ID_DEVICE_INFO）。</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_INFO_NUM)]
            public IntPtr[] pDeviceInfo;
        }

        /// <summary>
        /// 单个设备信息（联合体）。取码场景只需把指针原样回传给 MV_ID_CreateHandle，
        /// 因此这里仅用原始字节缓冲保存其余字段，便于按偏移读取 GigE IP。
        /// ⚠️校对：MV_ID_DEVICE_INFO 实际总大小，确保 BodySize ≥ 真实 sizeof - 8。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MV_ID_DEVICE_INFO
        {
            /// <summary>设备主类型（GigE/USB...）。⚠️校对字段含义与偏移。</summary>
            public uint nMajorType;

            /// <summary>设备次类型。⚠️校对。</summary>
            public uint nMinorType;

            /// <summary>
            /// 接口相关设备信息区（联合体）原始字节。
            /// GigE 设备的当前 IP (nCurrentIp) 通常位于本缓冲区偏移 8 处（网络字节序）。
            /// ⚠️校对：缓冲区长度需覆盖真实联合体大小。
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = BODY_SIZE)]
            public byte[] Body;
        }

        private const int BODY_SIZE = 540;

        #endregion

        #region 结果回调结构体

        /// <summary>结果回调委托原型。⚠️校对：MV_ID_RegisterResultCallBack 的回调签名。</summary>
        /// <param name="pstResultOut">指向 MV_ID_RESULT_OUT 的指针。</param>
        /// <param name="pUser">用户自定义上下文。</param>
        /// <returns>保留，通常返回 0。</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int MV_ID_RESULT_CB(IntPtr pstResultOut, IntPtr pUser);

        /// <summary>单条条码信息。⚠️校对：MV_ID_CODE_INFO 字段顺序与长度。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MV_ID_CODE_INFO
        {
            /// <summary>码制类型（1D/QR/DataMatrix...）。</summary>
            public uint enCodeType;

            /// <summary>码内容长度。</summary>
            public uint nCodeLen;

            /// <summary>码字符串。⚠️校对 SizeConst：MvIdApi.h 中 MV_ID_MAX_CODE_COUNT_BUF_SIZE（通常 256）。</summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string chCodeStr;
        }

        /// <summary>一次触发读出的结果。⚠️校对：MV_ID_RESULT_OUT 字段顺序。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MV_ID_RESULT_OUT
        {
            /// <summary>本次读到的码数量。</summary>
            public ushort nCodeNum;

            /// <summary>码信息数组。⚠️校对 SizeConst：单次最多可读码数（MvIdApi.h 中常量）。</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public MV_ID_CODE_INFO[] stCodeInfo;
        }

        #endregion

        #region 设备枚举 / 句柄生命周期

        /// <summary>枚举指定接口类型的在线读码器。⚠️校对入口名与参数。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_EnumDevices(uint nTLayerType, ref MV_ID_DEVICE_INFO_LIST pstDevList);

        /// <summary>根据设备信息创建设备句柄。⚠️校对：第二参为 MV_ID_DEVICE_INFO*。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_CreateHandle(out IntPtr handle, IntPtr pstDevInfo);

        /// <summary>打开设备（建立连接）。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_OpenDevice(IntPtr handle);

        /// <summary>关闭设备。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_CloseDevice(IntPtr handle);

        /// <summary>销毁设备句柄，释放资源。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_DestroyHandle(IntPtr handle);

        #endregion

        #region 取流 / 触发 / 参数

        /// <summary>开始取流。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_StartGrabbing(IntPtr handle);

        /// <summary>停止取流。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_StopGrabbing(IntPtr handle);

        /// <summary>设置枚举型节点（如 TriggerMode / TriggerSource）。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int MV_ID_SetEnumValue(IntPtr handle, string strKey, uint nValue);

        /// <summary>发送一次软触发命令。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_TriggerSoftwareExecute(IntPtr handle);

        /// <summary>注册结果回调（读到的码通过回调异步推送）。⚠️校对入口名。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int MV_ID_RegisterResultCallBack(IntPtr handle, MV_ID_RESULT_CB cb, IntPtr pUser);

        /// <summary>导入参数配置文件（IDMVS 导出的 .cfg）。⚠️校对入口名与文件格式。</summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int MV_ID_LoadCfg(IntPtr handle, string strCfgPath);

        #endregion
    }
}
