using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController.CTS
{

#if _WIN32
    using ControllerHandleType = Int32;
#else
    using ControllerHandleType = Int64;
#endif
    /// <summary>CTS控制器API接口</summary>
    public class CtsAPI
    {

        const string CommonToolDll = "CommonToolDll.dll";
        //const string ControllerDll = "ExampleDll.dll";

        /// <summary>以太网模式</summary>
        public const int EthernetMode = 0;
        /// <summary>RS232串口模式</summary>
        public const int Rs232Mode = 1;
        /// <summary>操作成功</summary>
        public const int SUCCESS = 10000;
        /// <summary>初始化错误</summary>
        public const int ERROR_INIT = 10001;
        /// <summary>关闭错误</summary>
        public const int ERROR_CLOSE = 10002;
        /// <summary>配置错误</summary>
        public const int ERROR_CFG = 10003;
        /// <summary>连接错误</summary>
        public const int ERROR_CONNECT = 10004;
        /// <summary>接收错误</summary>
        public const int ERROR_RX = 10005;
        /// <summary>发送错误</summary>
        public const int ERROR_TX = 10006;
        /// <summary>数据错误</summary>
        public const int ERROR_DATA = 10007;
        /// <summary>碰撞错误</summary>
        public const int ERROR_COLLISION = 10008;
        /// <summary>IP地址错误</summary>
        public const int ERROR_IP_ADDRESS = 10009;
        /// <summary>子网掩码地址错误</summary>
        public const int ERROR_SM_ADDRESS = 10010;
        /// <summary>网关地址错误</summary>
        public const int ERROR_GW_ADDRESS = 10011;
        /// <summary>获取适配器错误</summary>
        public const int ERROR_GET_ADAPTER = 10012;

        /// <summary>获取数字量错误</summary>
        public const int ERROR_GET_DIG_VAl = 10013;
        /// <summary>设置数字量错误</summary>
        public const int ERROR_SET_DIG_VAl = 10014;
        /// <summary>设置多通道数字量错误</summary>
        public const int ERROR_SET_MUL_DIG_VAl = 10015;

        /// <summary>设置数字时间错误</summary>
        public const int ERROR_SET_DigitalTime = 10016;
        /// <summary>获取数字时间错误</summary>
        public const int ERROR_GET_DigitalTime = 10017;
        /// <summary>设置相机信号错误</summary>
        public const int ERROR_SET_CameraSignal = 10018;
        /// <summary>获取相机信号错误</summary>
        public const int ERROR_GET_CameraSignal = 10019;
        /// <summary>设置相机延时错误</summary>
        public const int ERROR_SET_CameraDelay = 10020;
        /// <summary>获取相机延时错误</summary>
        public const int ERROR_GET_CameraDelay = 10021;
        /// <summary>设置触发模式错误</summary>
        public const int ERROR_SET_TriggerMode_VAl = 10022;
        /// <summary>获取触发模式错误</summary>
        public const int ERROR_GET_TriggerMode_VAl = 10023;
        /// <summary>设置触发周期错误</summary>
        public const int ERROR_SET_TriggerCycle_VAl = 10024;
        /// <summary>获取触发周期错误</summary>
        public const int ERROR_GET_TriggerCycle_VAl = 10025;
        /// <summary>设置通道切换错误</summary>
        public const int ERROR_SET_ChannelSwitch_VAl = 10026;
        /// <summary>获取通道切换错误</summary>
        public const int ERROR_GET_ChannelSwitch_VAl = 10027;
        /// <summary>保存数据错误</summary>
        public const int ERROR_SET_SaveData = 10028;
        /// <summary>获取软件触发错误</summary>
        public const int ERROR_GET_SoftwareTrigger = 10029;

        /// <summary>适配器参数</summary>
        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Adapter_prm
        {
            /// <summary>序列号</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)]
            public char[] cSn;
            /// <summary>IP地址</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
        }

        /// <summary>主机参数</summary>
        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Host_prm
        {
            /// <summary>序列号</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
            public char[] cSn;
            /// <summary>IP地址</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
            /// <summary>MAC地址</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] cMac;
        }

        /// <summary>控制器参数</summary>
        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Controller_prm
        {
            /// <summary>序列号</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
            public char[] cSn;
            /// <summary>IP地址</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
            /// <summary>子网掩码</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cSm;
            /// <summary>网关</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cGw;
            /// <summary>DHCP使能</summary>
            public char DHCP;
        }

        /// <summary>多通道光强值</summary>
        public struct MulIntensityValue
        {
            /// <summary>通道索引</summary>
            public int channelIndex;
            /// <summary>光强值</summary>
            public int intensity;
        }


        ////////////////////////////////////////////CommonToolDll//////////////////////////////////////////////////////
        /// <summary>获取适配器信息</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetAdapter(ref int AdatterCnt, IntPtr mAdapterPrm);

        /// <summary>获取主机信息</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetHost(ref int controllerCnt, IntPtr mHostPrm, string AdapterIP);

        /// <summary>获取配置</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetConfigure(byte[] mMAC, IntPtr mConPrm, string AdapterIP);

        /// <summary>设置配置</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetConfigure(byte[] mMAC, ref Controller_prm mConPrm, string AdapterIP);

        /// <summary>通过IP连接</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int ConnectIP(string ipAddress, int mTimeOut, ref ControllerHandleType controllerHandle);

        /// <summary>断开IP连接</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int DestroyIpConnection(ControllerHandleType controllerHandle);

        /// <summary>创建串口</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int CreateSerialPort(int serialPortIndex, ref ControllerHandleType controllerHandle);

        /// <summary>创建指定波特率串口</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int CreateSerialPort_Baud(int serialPortIndex, int baud, ref ControllerHandleType controllerHandle);

        /// <summary>释放串口</summary>
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int ReleaseSerialPort(ControllerHandleType controllerHandle);

        /// <summary>设置数字量值</summary>
        [DllImport("ControllerDll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetDigitalValue(int connectType, int ChannelIndex, int intensity, long controllerHandle);


        //////////////////////////////////////////////ControllerDll//////////////////////////////////////////////////////
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetDigitalValue(int connectType, ref int intensity, int ChannelIndex, ControllerHandleType controllerHandle);


        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetDigitalValue(int connectType, int ChannelIndex, int intensity, ControllerHandleType controllerHandle);


        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetMulDigitalValue(int connectType, MulIntensityValue[] MulIntensityValueArray, int length, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetDigitalTime(int connectType, int intensity, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetDigitalTime(int connectType, ref int intensity, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetCameraSignalValue(int connectType, int CameraSignal, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetCameraSignalValue(int connectType, ref int CameraSignal, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetCameraDelayValue(int connectType, int CameraDelay, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetCameraDelayValue(int connectType, ref int CameraDelay, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetTriggerModeValue(int connectType, int TriggerMode, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetTriggerModeValue(int connectType, ref int TriggerMode, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetTriggerCycleValue(int connectType, int TriggerCycle, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetTriggerCycleValue(int connectType, ref int TriggerCycle, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SetChannelSwitchValue(int connectType, int ChannelIndex, int SwitchValue, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int GetChannelSwitchValue(int connectType, int ChannelIndex, ref int SwitchValue, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SaveData(int connectType, ControllerHandleType controllerHandle);
        //[DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern
        //int SoftwareTrigger(int connectType, ControllerHandleType controllerHandle);

    }
}
