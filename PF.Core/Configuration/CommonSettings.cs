using System.ComponentModel;
using System.Configuration;

namespace PF.Core.Configuration
{
    /// <summary>
    /// 系统公共参数设置（文件存储，支持 PropertyGrid 展示）
    /// </summary>
    public class CommonSettings : ApplicationSettingsBase
    {
        [UserScopedSetting]
        [DefaultSettingValue("False")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("1.开机自启动")]
        [BrowsableAttribute(true)]
        public virtual bool AutoStart
        {
            get { return (bool)(this["AutoStart"] ?? false); }
            set { this["AutoStart"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("127.0.0.1")]
        [CategoryAttribute("B.通信参数")]
        [DisplayNameAttribute("1.服务器IP")]
        [BrowsableAttribute(true)]
        public virtual string ServerIP
        {
            get { return (string)(this["ServerIP"] ?? "127.0.0.1"); }
            set { this["ServerIP"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("聚力")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("2.公司名称")]
        [BrowsableAttribute(true)]
        public virtual string COName
        {
            get { return (string)(this["COName"] ?? "聚力"); }
            set { this["COName"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("PowerFocus")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("3.公司名称(英文)")]
        [BrowsableAttribute(true)]
        public virtual string COName_EN
        {
            get { return (string)(this["COName_EN"] ?? "PowerFocus"); }
            set { this["COName_EN"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("聚力智能标准软件框架")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("4.软件名称")]
        [BrowsableAttribute(true)]
        public virtual string SoftWareName
        {
            get { return (string)(this["SoftWareName"] ?? "聚力智能标准软件框架"); }
            set { this["SoftWareName"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("PowerFocus Standard Software Framework")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("5.软件名称(英文)")]
        [BrowsableAttribute(true)]
        public virtual string SoftWareName_EN
        {
            get { return (string)(this["SoftWareName_EN"] ?? "PowerFocus Standard Software Framework"); }
            set { this["SoftWareName_EN"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("Dark")]
        [CategoryAttribute("A.系统参数")]
        [DisplayNameAttribute("6.软件主题")]
        [BrowsableAttribute(true)]
        public virtual string Skin
        {
            get { return (string)(this["Skin"] ?? "Dark"); }
            set { this["Skin"] = value; }
        }
    }
}
