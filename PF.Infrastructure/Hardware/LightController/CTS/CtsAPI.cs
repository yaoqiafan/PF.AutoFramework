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
    public class CtsAPI
    {

        const string CommonToolDll = "CommonToolDll.dll";
        const string ControllerDll = "ExampleDll.dll";

        public const int EthernetMode = 0;
        public const int Rs232Mode = 1;
        public const int SUCCESS = 10000;
        public const int ERROR_INIT = 10001;
        public const int ERROR_CLOSE = 10002;
        public const int ERROR_CFG = 10003;
        public const int ERROR_CONNECT = 10004;
        public const int ERROR_RX = 10005;
        public const int ERROR_TX = 10006;
        public const int ERROR_DATA = 10007;
        public const int ERROR_COLLISION = 10008;
        public const int ERROR_IP_ADDRESS = 10009;
        public const int ERROR_SM_ADDRESS = 10010;
        public const int ERROR_GW_ADDRESS = 10011;
        public const int ERROR_GET_ADAPTER = 10012;

        public const int ERROR_GET_DIG_VAl = 10013;
        public const int ERROR_SET_DIG_VAl = 10014;
        public const int ERROR_SET_MUL_DIG_VAl = 10015;

        public const int ERROR_SET_DigitalTime = 10016;
        public const int ERROR_GET_DigitalTime = 10017;
        public const int ERROR_SET_CameraSignal = 10018;
        public const int ERROR_GET_CameraSignal = 10019;
        public const int ERROR_SET_CameraDelay = 10020;
        public const int ERROR_GET_CameraDelay = 10021;
        public const int ERROR_SET_TriggerMode_VAl = 10022;
        public const int ERROR_GET_TriggerMode_VAl = 10023;
        public const int ERROR_SET_TriggerCycle_VAl = 10024;
        public const int ERROR_GET_TriggerCycle_VAl = 10025;
        public const int ERROR_SET_ChannelSwitch_VAl = 10026;
        public const int ERROR_GET_ChannelSwitch_VAl = 10027;
        public const int ERROR_SET_SaveData = 10028;
        public const int ERROR_GET_SoftwareTrigger = 10029;


        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Adapter_prm
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)]
            public char[] cSn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
        }

        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Host_prm
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
            public char[] cSn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] cMac;
        }

        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Controller_prm
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
            public char[] cSn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cIp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cSm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] cGw;
            public char DHCP;
        }

        public struct MulIntensityValue
        {
            public int channelIndex;
            public int intensity;
        }


        ////////////////////////////////////////////CommonToolDll//////////////////////////////////////////////////////
        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetAdapter(ref int AdatterCnt, IntPtr mAdapterPrm);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetHost(ref int controllerCnt, IntPtr mHostPrm, string AdapterIP);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetConfigure(byte[] mMAC, IntPtr mConPrm, string AdapterIP);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetConfigure(byte[] mMAC, ref Controller_prm mConPrm, string AdapterIP);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int ConnectIP(string ipAddress, int mTimeOut, ref ControllerHandleType controllerHandle);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int DestroyIpConnection(ControllerHandleType controllerHandle);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int CreateSerialPort(int serialPortIndex, ref ControllerHandleType controllerHandle);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int CreateSerialPort_Baud(int serialPortIndex, int baud, ref ControllerHandleType controllerHandle);


        [DllImport(CommonToolDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int ReleaseSerialPort(ControllerHandleType controllerHandle);


        ////////////////////////////////////////////ControllerDll//////////////////////////////////////////////////////
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetDigitalValue(int connectType, ref int intensity, int ChannelIndex, ControllerHandleType controllerHandle);


        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetDigitalValue(int connectType, int ChannelIndex, int intensity, ControllerHandleType controllerHandle);


        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetMulDigitalValue(int connectType, MulIntensityValue[] MulIntensityValueArray, int length, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetDigitalTime(int connectType, int intensity, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetDigitalTime(int connectType, ref int intensity, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetCameraSignalValue(int connectType, int CameraSignal, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetCameraSignalValue(int connectType, ref int CameraSignal, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetCameraDelayValue(int connectType, int CameraDelay, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetCameraDelayValue(int connectType, ref int CameraDelay, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetTriggerModeValue(int connectType, int TriggerMode, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetTriggerModeValue(int connectType, ref int TriggerMode, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetTriggerCycleValue(int connectType, int TriggerCycle, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetTriggerCycleValue(int connectType, ref int TriggerCycle, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SetChannelSwitchValue(int connectType, int ChannelIndex, int SwitchValue, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int GetChannelSwitchValue(int connectType, int ChannelIndex, ref int SwitchValue, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SaveData(int connectType, ControllerHandleType controllerHandle);
        [DllImport(ControllerDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern
        int SoftwareTrigger(int connectType, ControllerHandleType controllerHandle);

    }
}
