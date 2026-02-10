using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Data.Entity.Category;
using PF.Data.Params;
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
