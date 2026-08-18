using PF.CommonTools.EnumRelated;
using PF.Core.Entities.Communication;
using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Enums.FileTransfer;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.UI.Shared.Data;
using PF.Workstation.AutoOcr.CostParam;
using System.Text.Json;

namespace PF.Application.Shell.CustomConfiguration.Param
{
    /// <summary>
    /// IDefaultParam
    /// </summary>
    public class DefaultParameters : IDefaultParam
    {

        /// <summary>
        /// 获取系统默认配置
        /// </summary>
        public Dictionary<string, UserLoginParam> GetUsersDefaults()
        {

            return new Dictionary<string, UserLoginParam>
            {

            };
        }



        /// <summary>
        /// 获取通讯实例默认配置。
        /// 说明：每条 CommunicationParam 的 Name = InstanceId，JsonValue = CommunicationConfig 的 JSON 序列化结果。
        ///
        /// 前 5 条是 HKBarcodeScan/KeyenceIntelligentCamera 底层 TCP 通道的配置——
        /// AutoStart=false，因为这些连接的生命周期交由对应硬件设备的 BaseDevice 逻辑驱动
        /// （通讯管理器只负责按配置实例化，不抢先连接，避免和硬件自己的 InternalConnectAsync 冲突）。
        /// IP/端口必须和 GetHardwareDefaults() 里 scancode1/scancode2/camera1 展示用的 IP/Port 保持一致。
        ///
        /// 中间的 FileTransferChannel 服务端/客户端两条——AutoStart=true，独立运行，不依附任何硬件，
        /// 通讯调试面板可以直接点开验证收发。
        ///
        /// 最后两条是 Modbus RTU/TCP 主站示例——不依附任何硬件工厂，纯粹演示 ConnectionParameters
        /// 该怎么填。AutoStart=false：开发机通常既没有接真实 RTU 从站的串口，也没有能连通的 Modbus TCP
        /// 从站，若设为 true 每次启动都会在日志里报一次连接失败。真机联调时把对应 PortName/BaudRate
        /// 或 IP/Port 改成实际值，需要的话再把 AutoStart 改 true，或者直接在通讯调试面板里手动
        /// 打开/连接测试。
        /// </summary>
        public Dictionary<string, CommunicationParam> GetCommunicationDefaults()
        {
            CommunicationConfig scanCode1Trigger = new()
            {
                InstanceId = "ScanCode1_Trigger",
                DisplayName = "工位1扫码枪-触发通道",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Client,
                ImplementationClassName = "TcpClient",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["ServerIp"] = "127.0.0.1", ["ServerPort"] = "9600" },
                Remarks = "海康扫码枪工位1触发通道底层TCP连接"
            };
            CommunicationConfig scanCode1UserPower = new()
            {
                InstanceId = "ScanCode1_UserPower",
                DisplayName = "工位1扫码枪-用户权限通道",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Client,
                ImplementationClassName = "TcpClient",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["ServerIp"] = "127.0.0.1", ["ServerPort"] = "21" },
                Remarks = "海康扫码枪工位1用户权限通道底层TCP连接"
            };
            CommunicationConfig scanCode2Trigger = new()
            {
                InstanceId = "ScanCode2_Trigger",
                DisplayName = "工位2扫码枪-触发通道",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Client,
                ImplementationClassName = "TcpClient",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["ServerIp"] = "127.0.0.1", ["ServerPort"] = "9700" },
                Remarks = "海康扫码枪工位2触发通道底层TCP连接"
            };
            CommunicationConfig scanCode2UserPower = new()
            {
                InstanceId = "ScanCode2_UserPower",
                DisplayName = "工位2扫码枪-用户权限通道",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Client,
                ImplementationClassName = "TcpClient",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["ServerIp"] = "127.0.0.1", ["ServerPort"] = "21" },
                Remarks = "海康扫码枪工位2用户权限通道底层TCP连接"
            };
            CommunicationConfig camera1Trigger = new()
            {
                InstanceId = "Camera1_Trigger",
                DisplayName = "OCR相机-触发通道",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Client,
                ImplementationClassName = "TcpClient",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["ServerIp"] = "127.0.0.1", ["ServerPort"] = "9800" },
                Remarks = "基恩士OCR智能相机触发通道底层TCP连接"
            };

            CommunicationConfig Severtest = new()
            {
                InstanceId = "Severtest",
                DisplayName = "服务器测试",
                Category = CommunicationCategory.Tcp,
                Role = CommunicationRole.Server,
                ImplementationClassName = "TcpServer",
                IsEnabled = true,
                AutoStart = true,
                ConnectionParameters = new Dictionary<string, string> { ["IP"] = "127.0.0.1", ["Port"] = "9900", ["Backlog"] = "10" },
                Remarks = "服务器测试调试"
            };

            CommunicationConfig fileTransferServer = new()
            {
                InstanceId = "VisionFileTransferServer",
                DisplayName = "视觉图像传输服务端(示例)",
                Category = CommunicationCategory.FileTransfer,
                Role = CommunicationRole.Server,
                ImplementationClassName = "FileTransferChannel",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["Role"] = nameof(FileTransferRole.Server),
                    ["LinksJson"] = JsonSerializer.Serialize(new List<FileTransferLinkEndpoint>
                    {
                        new() { LaneId = 0, LocalIp = "192.168.3.252", Port = 10000 },
                        new() { LaneId = 1, LocalIp = "192.168.3.252", Port = 10001 }
                    })
                },
                Remarks = "FileTransferChannel 服务端示例配置，单口回环，供通讯调试面板验证收发"
            };

            // 与 fileTransferServer 配对的客户端示例：LaneId 与端口都对齐，两者在同一进程内互连，
            // 供通讯调试面板在不接真实第二台设备的情况下做本地回环收发测试。
            CommunicationConfig fileTransferClient = new()
            {
                InstanceId = "VisionFileTransferClient",
                DisplayName = "视觉图像传输客户端(本地回环测试)",
                Category = CommunicationCategory.FileTransfer,
                Role = CommunicationRole.Client,
                ImplementationClassName = "FileTransferChannel",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["Role"] = nameof(FileTransferRole.Client),
                    ["LinksJson"] = JsonSerializer.Serialize(new List<FileTransferLinkEndpoint>
                    {
                        new() { LaneId = 0, LocalIp = "192.168.3.87", Port = 10000, RemoteIp = "192.168.3.252" },
                        new() { LaneId = 1, LocalIp = "192.168.3.87", Port = 10001, RemoteIp = "192.168.3.252" }
                    })
                },
                Remarks = "FileTransferChannel 客户端示例配置，连接本机 10000 端口，与 fileTransferServer 配对做本地回环测试"
            };

            // Modbus RTU 主站示例：ConnectionParameters 必填 PortName/BaudRate；可选键
            // Parity(None/Odd/Even/Mark/Space)/DataBits(5~8)/StopBits(One/Two/OnePointFive) 缺省 N81，
            // TimeoutMs 缺省 1000，AutoReconnect 缺省 true，ReconnectIntervalMs 缺省 5000。
            CommunicationConfig modbusRtuExample = new()
            {
                InstanceId = "ModbusRtu_Example",
                DisplayName = "Modbus RTU 主站(示例)",
                Category = CommunicationCategory.Modbus,
                Role = CommunicationRole.None,
                ImplementationClassName = "ModbusRtuMaster",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["PortName"] = "COM1", ["BaudRate"] = "9600" },
                Remarks = "Modbus RTU 主站示例配置，PortName/BaudRate 改成实际串口参数后可用；" +
                          "AutoStart 默认 false，避免开发机没接真实从站时启动报连接失败"
            };

            // Modbus TCP 主站示例：ConnectionParameters 必填 IP/Port（未配置时 Port 兜底取 502，Modbus TCP 标准端口）；
            // 可选键 TimeoutMs 缺省 1000，AutoReconnect 缺省 true，ReconnectIntervalMs 缺省 5000。
            CommunicationConfig modbusTcpExample = new()
            {
                InstanceId = "ModbusTcp_Example",
                DisplayName = "Modbus TCP 主站(示例)",
                Category = CommunicationCategory.Modbus,
                Role = CommunicationRole.Client,
                ImplementationClassName = "ModbusTcpMaster",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["IP"] = "192.168.1.100", ["Port"] = "502" },
                Remarks = "Modbus TCP 主站示例配置，IP/Port 改成实际从站地址后可用；" +
                          "AutoStart 默认 false，避免开发机连不到从站时启动报连接失败"
            };

            // 海康串口光源控制器的底层串口通道（示例）。ImplementationClassName = "SerialPort"，
            // 必填 PortName，可选 BaudRate（缺省 9600）与 Parity/DataBits/StopBits（缺省 N81）。
            // AutoStart=false：串口的打开时机交给光源设备的 InternalConnectAsync 驱动，
            // 与扫码枪/相机那几条 TCP 通道同一处理，避免通讯层与设备层抢着开口。
            CommunicationConfig hikLightSerial = new()
            {
                InstanceId = "HikLight_Serial",
                DisplayName = "海康光源控制器-串口通道",
                Category = CommunicationCategory.Serial,
                Role = CommunicationRole.None,
                ImplementationClassName = "SerialPort",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string> { ["PortName"] = "COM2", ["BaudRate"] = "9600" },
                Remarks = "海康串口光源控制器底层串口连接，PortName 改成实际串口号后可用"
            };

            // 裸串口示例：不挂任何设备，专供串口调试页试手用。
            // 上面那条 HikLight_Serial 虽然也是串口，但它归海康光源设备驱动开关，
            // 在调试页上手动开合会跟设备层抢串口；这条是独立的，随便开随便发。
            // 用法：通讯调试 → Serial 分类 → 本实例 → 参数设置里改成实际串口号 → 打开串口 → 收发。
            // 手上没有串口设备时，可用 com0com 之类的虚拟串口对（如 COM3↔COM4）自发自收验证。
            CommunicationConfig serialPortExample = new()
            {
                InstanceId = "SerialPort_Example",
                DisplayName = "串口(示例)",
                Category = CommunicationCategory.Serial,
                Role = CommunicationRole.None,
                ImplementationClassName = "SerialPort",
                IsEnabled = true,
                AutoStart = false,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["PortName"] = "COM3",
                    ["BaudRate"] = "9600",
                    ["Parity"] = "None",
                    ["DataBits"] = "8",
                    ["StopBits"] = "One"
                },
                Remarks = "裸串口示例配置，供串口调试页收发原始字节用；PortName 改成实际串口号后可用。" +
                          "AutoStart 默认 false，避免开发机上没有该串口时启动报错"
            };

            var configs = new[]
            {
                scanCode1Trigger, scanCode1UserPower, scanCode2Trigger, scanCode2UserPower, camera1Trigger,
                Severtest, fileTransferServer, fileTransferClient, modbusRtuExample, modbusTcpExample,
                hikLightSerial, serialPortExample
            };

            return configs.ToDictionary(c => c.InstanceId, c => new CommunicationParam
            {
                Name = c.InstanceId,
                Description = c.Remarks,
                TypeFullName = typeof(CommunicationConfig).FullName,
                JsonValue = JsonSerializer.Serialize(c),
                Category = "Communication",
                Version = 1
            });
        }


        /// <summary>
        /// 获取硬件设备默认配置
        ///
        /// 层级关系：
        ///   SIM_CARD_0（顶级板卡，ParentDeviceId 为空）
        ///   ├── SIM_X_AXIS_0（轴，ParentDeviceId = "SIM_CARD_0"）
        ///   └── SIM_VACUUM_IO（IO，ParentDeviceId = "SIM_CARD_0"）
        ///
        /// 说明：每条 HardwareParam 的 Name = DeviceId，JsonValue = HardwareConfig 的 JSON 序列化结果。
        /// </summary>
        public Dictionary<string, HardwareParam> GetHardwareDefaults()
        {
            HardwareConfig LYDMCCard = new()
            {
                DeviceId = "LTDMC_Card_0",
                DeviceName = "雷赛运动控制卡[0]",
                Category = "MotionCard",
                ImplementationClassName = "LTDMCMotionCard",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string> { ["CardIndex"] = "0"},
                Remarks = "雷赛运动控制卡，用于开发/调试"
            };

            HardwareConfig OcrYAxis = new()
            {
                DeviceId = E_AxisName.视觉Y轴.ToString(),
                DeviceName = "OCR模块Y轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "0" ,["AxisParam"]=System .Text .Json .JsonSerializer .Serialize (new AxisParam ())},
                Remarks = "OCR模块Y轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig OcrXAxis = new()
            {
                DeviceId = E_AxisName.视觉X轴.ToString(),
                DeviceName = "OCR模块X轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "1", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "OCR模块X轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig OcrZAxis = new()
            {
                DeviceId = E_AxisName.视觉Z轴.ToString(),
                DeviceName = "OCR模块Z轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "2", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "OCR模块Z轴，挂载于 LTDMC_Card_0"
            };

            HardwareConfig station2ZAxis = new()
            {
                DeviceId = E_AxisName.工位2上料Z轴.ToString(),
                DeviceName = "工位2上料Z轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "3", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位2上料Z轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig station2YAxis = new()
            {
                DeviceId = E_AxisName.工位2拉料Y轴.ToString(),
                DeviceName = "工位2晶圆拉料Y轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "4", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位2晶圆拉料Y轴，挂载于 LTDMC_Card_0"
            };


            HardwareConfig station1ZAxis = new()
            {
                DeviceId = E_AxisName.工位1上料Z轴.ToString(),
                DeviceName = "工位1上料Z轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "5", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位1上料Z轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig station1YAxis = new()
            {
                DeviceId = E_AxisName.工位1拉料Y轴.ToString(),
                DeviceName = "工位1晶圆拉料Y轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "6", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位1晶圆拉料Y轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig station1XAxis = new()
            {
                DeviceId = E_AxisName.工位1挡料X轴.ToString(),
                DeviceName = "工位1挡料X轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "7", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位1挡料X轴，挂载于 LTDMC_Card_0"
            };
            HardwareConfig station2XAxis = new()
            {
                DeviceId = E_AxisName.工位2挡料X轴.ToString(),
                DeviceName = "工位2挡料X轴",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "8", ["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(new AxisParam()) },
                Remarks = "工位2挡料X轴，挂载于 LTDMC_Card_0"
            };



            HardwareConfig IOControll = new()
            {
                DeviceId = "IO_Collectorll",
                DeviceName = "IO模块",
                Category = "IOController",
                ImplementationClassName = "EtherCatIO",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "LTDMC_Card_0",
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["InPutCount"] = Enum.GetNames(typeof(E_InPutName)).Length.ToString(),
                    ["OutPutCount"] = Enum.GetNames(typeof(E_OutPutName)).Length.ToString(),
                },
                Remarks = "IO耦合器，挂载于 LTDMC_Card_0"
            };


            HardwareConfig scancode1 = new HardwareConfig
            {
                DeviceId = E_ScanCode.工位1扫码枪.ToString(),
                DeviceName = "工位1扫码枪",
                Category = "ScanCode",
                ImplementationClassName = "HKBarcodeScan",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                // IP/端口不再由硬件配置提供——由下面两个 CommInstanceId 对应的 CommunicationConfig 决定，
                // 硬件层通过注入的 IClient.TargetServerIp/TargetServerPort 读取，避免两份数据源不一致
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["TimeOutMs"] = "5000",
                    ["TriggerCommInstanceId"] = "ScanCode1_Trigger", ["UserPowerCommInstanceId"] = "ScanCode1_UserPower"
                },
                Remarks = "雷赛运动控制卡，用于开发/调试"
            };
            HardwareConfig scancode2 = new HardwareConfig
            {
                DeviceId = E_ScanCode.工位2扫码枪.ToString(),
                DeviceName = "工位2扫码枪",
                Category = "ScanCode",
                ImplementationClassName = "KeyenceBarcodeScan",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["TimeOutMs"] = "5000",
                    ["IP"]="192.168.100.2"
                },
                Remarks = "雷赛运动控制卡，用于开发/调试"
            };


            HardwareConfig camera1 = new HardwareConfig
            {
                DeviceId = E_Camera.OCR相机.ToString(),
                DeviceName = "基恩士OCR智能相机",
                Category = "Camera",
                ImplementationClassName = "KeyenceIntelligentCamera",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["TimeOutms"] = "5000",
                    ["CommInstanceId"] = "Camera1_Trigger"
                },
                Remarks = "基恩士OCR智能相机，用于开发/调试"
            };

            HardwareConfig light = new HardwareConfig
            {
                DeviceId = E_LightController.康视达_COM.ToString(),
                DeviceName = "康视达Com口光源控制器",
                Category = "Light",
                ImplementationClassName = "CTS_LightController",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string> { ["COM"] = "COM1" },
                Remarks = "康视达光源控制器，用于开发/调试"
            };

            // ── 线阵相机示例（AutoOCR Demo 本身不使用，默认 IsEnabled=false，只作为配置样板）──────
            //
            // 两台设备演示的是"经采集卡"这一种拓扑：采集卡是顶级设备，相机把 ParentDeviceId
            // 指向卡的 DeviceId 挂在其下。若改为 GigE/USB 直连，把相机的 ParentDeviceId 留空、
            // 并删掉采集卡这条即可——设备层不需要任何"链路类型"开关，拓扑本身就表达了链路。
            //
            // 启用前须先确认：采集卡驱动(MVFG)已装、序列号填对（留空则按枚举索引选定）。
            HardwareConfig lineScanGrabber = new HardwareConfig
            {
                DeviceId = "LineScan_Grabber_0",
                DeviceName = "海康图像采集卡",
                Category = "FrameGrabber",
                ImplementationClassName = "HikFrameGrabberCard",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string>
                {
                    // 序列号优先；留空时回退按 Index 选定（插拔顺序变化会失配，正式部署建议填序列号）
                    ["SerialNumber"] = string.Empty,
                    ["Index"] = "0"
                },
                Remarks = "线阵相机采集卡示例（默认禁用）。CameraLink 的切帧发生在卡上，故建成独立顶级设备"
            };

            HardwareConfig lineScanCamera = new HardwareConfig
            {
                DeviceId = "LineScan_Camera_0",
                DeviceName = "海康线阵相机 MV-CL162-91F2M",
                Category = "Camera",
                ImplementationClassName = "HikLineScanCamera",
                IsSimulated = true,
                IsEnabled = true,
                // 指向采集卡 = CameraLink 链路；留空 = GigE/USB 直连
                ParentDeviceId = lineScanGrabber.DeviceId,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["SerialNumber"] = string.Empty,
                    ["Index"] = "0",
                    // SDK 图像缓存节点数：占用 ≈ 单帧字节数 × 本值。
                    // 16K 宽 × 10000 行 Mono8 单帧就约 160MB，大帧场景必须下调到 2
                    ["ImageNodeNum"] = "3"
                },
                Remarks = "线阵相机示例（默认禁用）。曝光/行触发/编码器等运行期参数不在此配置，由机构层按扫描任务下发"
            };

            // ── 直连拓扑示例：同一个工厂，ParentDeviceId 留空即为顶级设备 ─────────────
            //
            // 与上面那台的差别只有 ParentDeviceId 一项。没有采集卡时，
            // 帧长回到相机自身的 Height 节点、帧触发回到相机的帧触发节点，
            // 由设备层的帧控制策略自动切换——配置里不需要、也不存在"链路类型"字段。
            //
            // 注意：直连时相机侧没有与采集卡 FrameTimeoutTime 对应的节点，
            // "行数攒不满一帧"只能靠 WaitFrameAsync 的等待超时兜底。
            HardwareConfig lineScanCameraStandalone = new HardwareConfig
            {
                DeviceId = "LineScan_Camera_1",
                DeviceName = "海康线阵相机(网口直连)",
                Category = "Camera",
                ImplementationClassName = "HikLineScanCamera",
                IsSimulated = true,
                IsEnabled = true,
                // 留空 = 不挂任何父设备，作为顶级设备在第 1 层初始化
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string>
                {
                    ["SerialNumber"] = string.Empty,
                    ["Index"] = "0",
                    ["ImageNodeNum"] = "3"
                },
                Remarks = "线阵相机直连示例（默认禁用）。GigE 链路，打开后会自动探测并设置网络最佳包大小"
            };

            // ── 海康串口光源控制器示例 ─────────────────────────────────────────────
            //
            // 与上面康视达那条（CTS_LightController）的差别：串口不写在硬件配置里，
            // 而是用 CommInstanceId 指向一条通讯配置（见 GetCommunicationDefaults 的
            // HikLight_Serial）。波特率、校验位这些串口参数改通讯侧，硬件侧只留设备语义。
            HardwareConfig hikLight = new HardwareConfig
            {
                DeviceId = "HikLight_0",
                DeviceName = "海康Com口光源控制器",
                Category = "Light",
                ImplementationClassName = "HikComLightController",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string> { ["CommInstanceId"] = "HikLight_Serial" },
                Remarks = "海康串口光源控制器示例，通道 1~4，指令 S{通道字母}{4位亮度}#；串口参数见通讯配置 HikLight_Serial"
            };



            return new Dictionary<string, HardwareParam>
            {
                {
                    LYDMCCard.DeviceId, new HardwareParam
                    {
                        Name         = LYDMCCard.DeviceId,
                        Description  = LYDMCCard.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(LYDMCCard),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    OcrYAxis.DeviceId, new HardwareParam
                    {
                        Name         = OcrYAxis.DeviceId,
                        Description  = OcrYAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(OcrYAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                     OcrXAxis.DeviceId, new HardwareParam
                    {
                        Name         = OcrXAxis.DeviceId,
                        Description  = OcrXAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(OcrXAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                    OcrZAxis.DeviceId, new HardwareParam
                    {
                        Name         = OcrZAxis.DeviceId,
                        Description  = OcrZAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(OcrZAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                    station2ZAxis.DeviceId, new HardwareParam
                    {
                        Name         = station2ZAxis.DeviceId,
                        Description  = station2ZAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station2ZAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                 {
                    station2XAxis.DeviceId, new HardwareParam
                    {
                        Name         = station2XAxis.DeviceId,
                        Description  = station2XAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station2XAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                  {
                    station2YAxis.DeviceId, new HardwareParam
                    {
                        Name         = station2YAxis.DeviceId,
                        Description  = station2YAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station2YAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    station1ZAxis.DeviceId, new HardwareParam
                    {
                        Name         = station1ZAxis.DeviceId,
                        Description  = station1ZAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station1ZAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }  ,
                {
                    station1YAxis.DeviceId, new HardwareParam
                    {
                        Name         = station1YAxis.DeviceId,
                        Description  = station1YAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station1YAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    station1XAxis.DeviceId, new HardwareParam
                    {
                        Name         = station1XAxis.DeviceId,
                        Description  = station1XAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(station1XAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    IOControll .DeviceId, new HardwareParam
                    {
                        Name         = IOControll.DeviceId,
                        Description  = IOControll.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(IOControll),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    scancode1.DeviceId ,new HardwareParam
                    {
                         Name         = scancode1.DeviceId,
                        Description  = scancode1.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(scancode1),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    scancode2.DeviceId ,new HardwareParam
                    {
                         Name         = scancode2.DeviceId,
                        Description  = scancode2.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(scancode2),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                    camera1.DeviceId ,new HardwareParam
                    {
                         Name         = camera1.DeviceId,
                        Description  = camera1.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(camera1),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    light .DeviceId ,new HardwareParam
                    {
                         Name         = light .DeviceId,
                        Description  = light.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(light),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                // 采集卡必须排在相机之前只是可读性考虑：真正的"父先于子"由
                // HardwareManagerService 按 ParentDeviceId 分层保证，与本字典的顺序无关
                {
                    lineScanGrabber.DeviceId, new HardwareParam
                    {
                        Name         = lineScanGrabber.DeviceId,
                        Description  = lineScanGrabber.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(lineScanGrabber),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    lineScanCamera.DeviceId, new HardwareParam
                    {
                        Name         = lineScanCamera.DeviceId,
                        Description  = lineScanCamera.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(lineScanCamera),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    lineScanCameraStandalone.DeviceId, new HardwareParam
                    {
                        Name         = lineScanCameraStandalone.DeviceId,
                        Description  = lineScanCameraStandalone.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(lineScanCameraStandalone),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    hikLight.DeviceId, new HardwareParam
                    {
                        Name         = hikLight.DeviceId,
                        Description  = hikLight.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(hikLight),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
            };
        }

        

        /// <summary>
        /// 获取系统默认配置（动态遍历枚举自动生成）
        /// </summary>
        public Dictionary<string, SystemConfigParam> GetSystemDefaults()
        {
            var defaultConfigDict = new Dictionary<string, SystemConfigParam>();


            foreach (E_Params param in Enum.GetValues(typeof(E_Params)))
            {

                string paramName = param.ToString();


                EnumParamInfo info = param.GetParamInfo();


                string typeFullName = info.TypeFullName ?? typeof(string).FullName;


                string jsonValue = info.DefaultValue != null
                    ? JsonSerializer.Serialize(info.DefaultValue)
                    : JsonSerializer.Serialize("");


                defaultConfigDict.Add(paramName, new SystemConfigParam
                {
                    Name = paramName,
                    Description = info.Description,
                    Category = info.Category,
                    TypeFullName = typeFullName,
                    JsonValue = jsonValue,
                    Version = 1 // 默认初始版本号为 1
                });
            }

            return defaultConfigDict;
        }

    }
}
