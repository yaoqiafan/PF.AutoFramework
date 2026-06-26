using PF.Core.Constants;
using PF.UI.Shared.Data;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Application.Base.Configuration
{
    public enum ConfettiEffectType
    {
        BasicCannon,
        RandomDirection,
        RealisticLook,
        Fireworks,
        Stars,
        Snow,
        SchoolPride,
    }

    /// <summary>
    /// 系统公共参数设置（JSON 文件存储，支持 PropertyGrid 展示）
    /// </summary>
    public class CommonSettings
    {
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("1.开机自启动")]
        [BrowsableAttribute(true)]
        public bool AutoStart { get; set; } = false;

        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("2.公司名称")]
        [BrowsableAttribute(true)]
        public string COName { get; set; } = "聚力";

        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("3.公司名称(英文)")]
        [BrowsableAttribute(true)]
        public string COName_EN { get; set; } = "PowerFocus";

        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("4.软件名称")]
        [BrowsableAttribute(true)]
        public string SoftWareName { get; set; } = "聚力智能标准软件框架";

        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("5.软件名称(英文)")]
        [BrowsableAttribute(true)]
        public string SoftWareName_EN { get; set; } = "PowerFocus Standard Software Framework";

        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("6.软件主题")]
        [BrowsableAttribute(true)]
        public SkinType Skin { get; set; } = SkinType.Dark;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("1.启用操作员登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableOperatorAnimation { get; set; } = false;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("2.操作员登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType OperatorAnimationType { get; set; } = ConfettiEffectType.SchoolPride;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("3.启用工程师登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableEngineerAnimation { get; set; } = false;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("4.工程师登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType EngineerAnimationType { get; set; } = ConfettiEffectType.RealisticLook;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("5.启用管理员登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableAdministratorAnimation { get; set; } = false;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("6.管理员登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType AdministratorAnimationType { get; set; } = ConfettiEffectType.Fireworks;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("7.启用超级用户登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableSuperuserAnimation { get; set; } = true;

        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("8.超级用户登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType SuperuserAnimationType { get; set; } = ConfettiEffectType.Stars;

        [CategoryAttribute("C.配置参数")]
        [DisplayNameAttribute("1.无使用权限降级时间")]
        [BrowsableAttribute(true)]
        public double NoUseTime { get; set; } = 60.0;

        [Browsable(false)]
        [JsonIgnore]
        public static string ConfigFilePath => Path.Combine(ConstGlobalParam.ConfigPath, "user.config");

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConstGlobalParam.ConfigPath))
                    Directory.CreateDirectory(ConstGlobalParam.ConfigPath);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public static CommonSettings Load()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<CommonSettings>(json) ?? new CommonSettings();
                }
                catch
                {
                    return new CommonSettings();
                }
            }
            return new CommonSettings();
        }
    }
}
