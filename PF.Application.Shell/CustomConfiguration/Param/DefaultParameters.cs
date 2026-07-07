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
        /// 最后一条是 FileTransferChannel 服务端示例——AutoStart=true，独立运行，不依附任何硬件，
        /// 通讯调试面板可以直接点开验证收发。
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
                AutoStart = true,
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
                AutoStart = true,
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

            var configs = new[] { scanCode1Trigger, scanCode1UserPower, scanCode2Trigger, scanCode2UserPower, camera1Trigger, Severtest, fileTransferServer, fileTransferClient };

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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                IsSimulated = false,
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
                Category = "Canera",
                ImplementationClassName = "KeyenceIntelligentCamera",
                IsSimulated = false,
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
                ImplementationClassName = "CTS_LightControoller",
                IsSimulated = false,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string> { ["COM"] = "COM1" },
                Remarks = "康视达光源控制器，用于开发/调试"
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
