using PF.Core.Constants;
using PF.UI.Shared.Data;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Application.Shell.CustomConfiguration.Param
{
    /// <summary>
    /// 系统公共参数设置（JSON 文件存储，支持 PropertyGrid 展示）
    /// </summary>
    public class CommonSettings
    {
        // ==========================================
        // 1. 配置属性区 (PropertyGrid 会读取这里的标签)
        // ==========================================

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
        public SkinType Skin { get; set; } =  SkinType.Dark;


        // ==========================================
        // 2. 文件存取逻辑区
        // ==========================================

        /// <summary>
        /// 获取配置文件路径。
        /// [Browsable(false)] 确保它不会显示在 PropertyGrid 中。
        /// [JsonIgnore] 确保它不会被写入到 JSON 文件里。
        /// </summary>
        [Browsable(false)]
        [JsonIgnore]
        public static string ConfigFilePath => Path.Combine(ConstGlobalParam.ConfigPath, "user.config");

        /// <summary>
        /// 将当前设置保存到 JSON 文件
        /// </summary>
        public void Save()
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(ConstGlobalParam.ConfigPath))
                {
                    Directory.CreateDirectory(ConstGlobalParam.ConfigPath);
                }

                // 序列化为 JSON，WriteIndented = true 表示格式化输出（带换行和缩进，方便人工查看）
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                // 此处可接入你的日志系统
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 文件加载设置。如果文件不存在或解析失败，则返回默认设置。
        /// </summary>
        public static CommonSettings Load()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var settings = JsonSerializer.Deserialize<CommonSettings>(json);
                    return settings ?? new CommonSettings();
                }
                catch
                {
                    // 解析失败（比如文件损坏），返回一套默认的新配置
                    return new CommonSettings();
                }
            }

            // 文件不存在，返回默认配置
            return new CommonSettings();
        }
    }
}
