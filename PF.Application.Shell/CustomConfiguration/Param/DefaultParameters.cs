using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.UI.Shared.Data;
using PF.Workstation.AutoOcr.CostParam;
using System.Text.Json;

namespace PF.Application.Shell.CustomConfiguration.Param
{
    public class DefaultParameters:IDefaultParam
    {
        /// <summary>
        /// 获取系统默认配置
        /// </summary>
        public  Dictionary<string, CommonParam> GetCommonDefaults()
        {
            return new Dictionary<string, CommonParam>
            {
                {
                    "AutoStart", new CommonParam
                    {
                       Name = "AutoStart",
                       Description = "系统开机自启动",
                       TypeFullName = typeof(bool).FullName,
                       JsonValue = JsonSerializer.Serialize(false),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },
                {
                    "COName", new CommonParam
                    {
                       Name = "COName",
                       Description = "公司名称",
                       TypeFullName = typeof(string).FullName,
                       JsonValue = JsonSerializer.Serialize("聚力"),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },
                {
                    "COName_EN", new CommonParam
                    {
                       Name = "COName_EN",
                       Description = "公司名称(英文)",
                       TypeFullName = typeof(string).FullName,
                       JsonValue = JsonSerializer.Serialize("PowerFocus"),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },
                {
                    "SoftWareName", new CommonParam
                    {
                       Name = "SoftWareName",
                       Description = "软件名称",
                       TypeFullName = typeof(string).FullName,
                       JsonValue = JsonSerializer.Serialize("聚力智能标准软件框架"),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },
                {
                    "SoftWareName_EN", new CommonParam
                    {
                       Name = "SoftWareName_EN",
                       Description = "软件名称(英文)",
                       TypeFullName = typeof(string).FullName,
                       JsonValue = JsonSerializer.Serialize("PowerFocus Standard Software Framework"),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },
                {
                    "Skin", new CommonParam
                    {
                       Name = "Skin",
                       Description = "软件主题",
                       TypeFullName = typeof(SkinType).FullName,
                       JsonValue = JsonSerializer.Serialize(SkinType.Dark),
                       Category = "系统基本参数",
                       Version = 1,
                    }
                },

            };
        }



        /// <summary>
        /// 获取系统默认配置
        /// </summary>
        public  Dictionary<string, UserLoginParam> GetUsersDefaults()
        {
            //UserInfo Operator = new UserInfo() { Password = "PF111", Root = UserLevel.Operator, UserId = "Operator", UserName = "Operator" };
            //UserInfo Engineer = new UserInfo() { Password = "PF222", Root = UserLevel.Engineer, UserId = "Engineer", UserName = "Engineer" };
            //UserInfo Administrator = new UserInfo() { Password = "PF333", Root = UserLevel.Administrator, UserId = "Administrator", UserName = "Administrator" };
            //UserInfo SuperUser = new UserInfo() { Password = "PF88888", Root = UserLevel.SuperUser, UserId = "SuperUser", UserName = "SuperUser" };

            return new Dictionary<string, UserLoginParam>
            {
                //{
                //    "Operator", new UserLoginParam
                //    {
                //       Name = "Operator",
                //       Description = "默认用户Operator",
                //       TypeFullName = typeof(UserInfo).FullName,
                //       JsonValue = JsonSerializer.Serialize(Operator),
                //       Category = "默认用户参数",
                //       Version = 1,
                //    }
                //},
                //{
                //    "Engineer", new UserLoginParam
                //    {
                //       Name = "Engineer",
                //       Description = "默认用户Engineer",
                //       TypeFullName = typeof(UserInfo).FullName,
                //       JsonValue = JsonSerializer.Serialize(Engineer),
                //       Category = "默认用户参数",
                //       Version = 1,
                //    }
                //},
                //{
                //    "Administrator", new UserLoginParam
                //    {
                //       Name = "Administrator",
                //       Description = "默认用户Administrator",
                //       TypeFullName = typeof(UserInfo).FullName,
                //       JsonValue = JsonSerializer.Serialize(Administrator),
                //       Category = "默认用户参数",
                //       Version = 1,
                //    }
                //},
                //{
                //    "SuperUser", new UserLoginParam
                //    {
                //       Name = "SuperUser",
                //       Description = "默认用户SuperUser",
                //       TypeFullName = typeof(UserInfo).FullName,
                //       JsonValue = JsonSerializer.Serialize(SuperUser),
                //       Category = "默认用户参数",
                //       Version = 1,
                //    }
                //},

            };
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
                ConnectionParameters = new Dictionary<string, string> { ["CardIndex"] = "0" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "0" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "1" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "2" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "3" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "4" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "5" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "6" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "7" },
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
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "8" },
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
                ConnectionParameters = new Dictionary<string, string> { ["IP"] = "127.0.0.1", ["TiggerPort"] = "9600", ["UserPort"] = "21", ["TimeOutMs"] = "5000" },
                Remarks = "雷赛运动控制卡，用于开发/调试"
            };
            HardwareConfig scancode2 = new HardwareConfig
            {
                DeviceId = E_ScanCode.工位2扫码枪.ToString(),
                DeviceName = "工位2扫码枪",
                Category = "ScanCode",
                ImplementationClassName = "HKBarcodeScan",
                IsSimulated = false,
                IsEnabled = true,
                ParentDeviceId = string.Empty,
                ConnectionParameters = new Dictionary<string, string> { ["IP"] = "127.0.0.1", ["TiggerPort"] = "9700", ["UserPort"] = "21", ["TimeOutMs"]= "5000" },
                Remarks = "雷赛运动控制卡，用于开发/调试"
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
            };
        }

        /// <summary>
        /// 获取系统默认配置
        /// </summary>
        public  Dictionary<string, SystemConfigParam> GetSystemDefaults()
        {
            return new Dictionary<string, SystemConfigParam>
            {
                {
                    "test1", new SystemConfigParam
                    {
                       Name = "test1",
                       Description = "服务器IP",
                       TypeFullName = typeof(string).FullName,
                       JsonValue = JsonSerializer.Serialize("127.0.0.1"),
                       Category = "测试string",
                       Version = 1,
                    }
                },

                 {
                    "test2", new SystemConfigParam
                    {
                       Name = "test2",
                       Description = "服务器IP",
                       TypeFullName = typeof(int).FullName,
                       JsonValue = JsonSerializer.Serialize(123),
                       Category = "测试int",
                       Version = 1,
                    }
                },

                  {
                    "test3", new SystemConfigParam
                    {
                       Name = "test3",
                       Description = "服务器IP",
                       TypeFullName = typeof(double).FullName,
                       JsonValue = JsonSerializer.Serialize(3.14),
                       Category = "测试double",
                       Version = 1,
                    }
                },

                  {
                    "test4", new SystemConfigParam
                    {
                       Name = "test4",
                       Description = "服务器IP",
                       TypeFullName = typeof(float).FullName,
                       JsonValue = JsonSerializer.Serialize(3.14f),
                       Category = "测试float",
                       Version = 1,
                    }
                },

                   {
                    "test5", new SystemConfigParam
                    {
                       Name = "test5",
                       Description = "服务器IP",
                       TypeFullName = typeof(long).FullName,
                       JsonValue = JsonSerializer.Serialize(10000000000000000),
                       Category = "测试long",
                       Version = 1,
                    }
                },

            };
        }

    }
}
