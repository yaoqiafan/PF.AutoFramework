using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.UI.Shared.Data;
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
            UserInfo Operator = new UserInfo() { Password = "PF111", Root = UserLevel.Operator, UserId = "Operator", UserName = "Operator" };
            UserInfo Engineer = new UserInfo() { Password = "PF222", Root = UserLevel.Engineer, UserId = "Engineer", UserName = "Engineer" };
            UserInfo Administrator = new UserInfo() { Password = "PF333", Root = UserLevel.Administrator, UserId = "Administrator", UserName = "Administrator" };
            UserInfo SuperUser = new UserInfo() { Password = "PF88888", Root = UserLevel.SuperUser, UserId = "SuperUser", UserName = "SuperUser" };

            return new Dictionary<string, UserLoginParam>
            {
                {
                    "Operator", new UserLoginParam
                    {
                       Name = "Operator",
                       Description = "默认用户Operator",
                       TypeFullName = typeof(UserInfo).FullName,
                       JsonValue = JsonSerializer.Serialize(Operator),
                       Category = "默认用户参数",
                       Version = 1,
                    }
                },
                {
                    "Engineer", new UserLoginParam
                    {
                       Name = "Engineer",
                       Description = "默认用户Engineer",
                       TypeFullName = typeof(UserInfo).FullName,
                       JsonValue = JsonSerializer.Serialize(Engineer),
                       Category = "默认用户参数",
                       Version = 1,
                    }
                },
                {
                    "Administrator", new UserLoginParam
                    {
                       Name = "Administrator",
                       Description = "默认用户Administrator",
                       TypeFullName = typeof(UserInfo).FullName,
                       JsonValue = JsonSerializer.Serialize(Administrator),
                       Category = "默认用户参数",
                       Version = 1,
                    }
                },
                {
                    "SuperUser", new UserLoginParam
                    {
                       Name = "SuperUser",
                       Description = "默认用户SuperUser",
                       TypeFullName = typeof(UserInfo).FullName,
                       JsonValue = JsonSerializer.Serialize(SuperUser),
                       Category = "默认用户参数",
                       Version = 1,
                    }
                },

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
            HardwareConfig simCard = new()
            {
                DeviceId                = "SIM_CARD_0",
                DeviceName              = "模拟运动控制卡[0]",
                Category                = "MotionCard",
                ImplementationClassName = "LTDMCMotionCard",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = string.Empty,
                ConnectionParameters    = new Dictionary<string, string> { ["CardIndex"] = "0" },
                Remarks                 = "模拟运动控制卡，用于开发/调试"
            };

            HardwareConfig simXAxis = new()
            {
                DeviceId                = "SIM_X_AXIS_0",
                DeviceName              = "模拟X轴[0]",
                Category                = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = "SIM_CARD_0",
                ConnectionParameters    = new Dictionary<string, string> { ["AxisIndex"] = "0" },
                Remarks                 = "模拟X轴，挂载于 SIM_CARD_0"
            };

            HardwareConfig simYAxis = new()
            {
                DeviceId = "SIM_Y_AXIS_1",
                DeviceName = "模拟Y轴[1]",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "SIM_CARD_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "1" },
                Remarks = "模拟Y轴，挂载于 SIM_CARD_0"
            };

            HardwareConfig simZAxis = new()
            {
                DeviceId = "SIM_Z_AXIS_2",
                DeviceName = "模拟Z轴[2]",
                Category = "Axis",
                ImplementationClassName = "EtherCatAxis",
                IsSimulated = true,
                IsEnabled = true,
                ParentDeviceId = "SIM_CARD_0",
                ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "2" },
                Remarks = "模拟Z轴，挂载于 SIM_CARD_0"
            };

            HardwareConfig simIO = new()
            {
                DeviceId                = "SIM_VACUUM_IO",
                DeviceName              = "模拟真空IO卡",
                Category                = "IOController",
                ImplementationClassName = "EtherCatIO",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = "SIM_CARD_0",
                Remarks                 = "模拟真空IO卡，挂载于 SIM_CARD_0"
            };

            return new Dictionary<string, HardwareParam>
            {
                {
                    simCard.DeviceId, new HardwareParam
                    {
                        Name         = simCard.DeviceId,
                        Description  = simCard.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(simCard),
                        Category     = "Hardware",
                        Version      = 1
                    }
                },
                {
                    simXAxis.DeviceId, new HardwareParam
                    {
                        Name         = simXAxis.DeviceId,
                        Description  = simXAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(simXAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                    simYAxis.DeviceId, new HardwareParam
                    {
                        Name         = simYAxis.DeviceId,
                        Description  = simYAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(simYAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                
                {
                    simZAxis.DeviceId, new HardwareParam
                    {
                        Name         = simZAxis.DeviceId,
                        Description  = simZAxis.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(simZAxis),
                        Category     = "Hardware",
                        Version      = 1
                    }
                }
                ,
                {
                    simIO.DeviceId, new HardwareParam
                    {
                        Name         = simIO.DeviceId,
                        Description  = simIO.Remarks,
                        TypeFullName = typeof(HardwareConfig).FullName,
                        JsonValue    = JsonSerializer.Serialize(simIO),
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
