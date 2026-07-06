using PF.Core.Constants;
using PF.UI.Shared.Data;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Application.Base.Configuration
{
    /// <summary>登录庆祝动画特效类型。</summary>
    public enum ConfettiEffectType
    {
        /// <summary>基础炮弹礼花。</summary>
        BasicCannon,
        /// <summary>随机方向礼花。</summary>
        RandomDirection,
        /// <summary>写实风格礼花。</summary>
        RealisticLook,
        /// <summary>烟花特效。</summary>
        Fireworks,
        /// <summary>星星特效。</summary>
        Stars,
        /// <summary>雪花特效。</summary>
        Snow,
        /// <summary>彩带特效。</summary>
        SchoolPride,
    }

    /// <summary>
    /// 系统公共参数设置（JSON 文件存储，支持 PropertyGrid 展示）
    /// </summary>
    public class CommonSettings
    {
        /// <summary>开机时自动启动软件。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("1.开机自启动")]
        [BrowsableAttribute(true)]
        public bool AutoStart { get; set; } = false;

        /// <summary>公司名称（中文，显示在主界面标题栏）。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("2.公司名称")]
        [BrowsableAttribute(true)]
        public string COName { get; set; } = "聚力";

        /// <summary>公司名称（英文）。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("3.公司名称(英文)")]
        [BrowsableAttribute(true)]
        public string COName_EN { get; set; } = "PowerFocus";

        /// <summary>软件名称（中文，显示在主界面标题栏）。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("4.软件名称")]
        [BrowsableAttribute(true)]
        public string SoftWareName { get; set; } = "聚力智能标准软件框架";

        /// <summary>软件名称（英文）。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("5.软件名称(英文)")]
        [BrowsableAttribute(true)]
        public string SoftWareName_EN { get; set; } = "PowerFocus Standard Software Framework";

        /// <summary>软件主题（Dark / Light）。</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("6.软件主题")]
        [BrowsableAttribute(true)]
        public SkinType Skin { get; set; } = SkinType.Dark;

        /// <summary>是否启用加载界面详细日志</summary>
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("7.启用加载界面详细日志")]
        [BrowsableAttribute(true)]
        public bool EnableDetailedLog { get; set; } = true;

        /// <summary>操作员登录时是否播放庆祝动画。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("1.启用操作员登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableOperatorAnimation { get; set; } = false;

        /// <summary>操作员登录庆祝动画类型。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("2.操作员登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType OperatorAnimationType { get; set; } = ConfettiEffectType.SchoolPride;

        /// <summary>工程师登录时是否播放庆祝动画。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("3.启用工程师登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableEngineerAnimation { get; set; } = false;

        /// <summary>工程师登录庆祝动画类型。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("4.工程师登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType EngineerAnimationType { get; set; } = ConfettiEffectType.RealisticLook;

        /// <summary>管理员登录时是否播放庆祝动画。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("5.启用管理员登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableAdministratorAnimation { get; set; } = false;

        /// <summary>管理员登录庆祝动画类型。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("6.管理员登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType AdministratorAnimationType { get; set; } = ConfettiEffectType.Fireworks;

        /// <summary>超级用户登录时是否播放庆祝动画。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("7.启用超级用户登录动画")]
        [BrowsableAttribute(true)]
        public bool EnableSuperuserAnimation { get; set; } = true;

        /// <summary>超级用户登录庆祝动画类型。</summary>
        [CategoryAttribute("B.登录参数")]
        [DisplayNameAttribute("8.超级用户登录主题")]
        [BrowsableAttribute(true)]
        public ConfettiEffectType SuperuserAnimationType { get; set; } = ConfettiEffectType.Stars;

        /// <summary>无操作自动降权倒计时（秒）。</summary>
        [CategoryAttribute("C.配置参数")]
        [DisplayNameAttribute("1.无使用权限降级时间")]
        [BrowsableAttribute(true)]
        public double NoUseTime { get; set; } = 60.0;

        /// <summary>user.config 文件完整路径。</summary>
        [Browsable(false)]
        [JsonIgnore]
        public static string ConfigFilePath => Path.Combine(ConstGlobalParam.ConfigPath, "user.config");

        /// <summary>将当前配置序列化为 JSON 并写入 <see cref="ConfigFilePath"/>。</summary>
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

        /// <summary>从 <see cref="ConfigFilePath"/> 读取 JSON 并反序列化；文件不存在或损坏时返回默认实例。</summary>
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
