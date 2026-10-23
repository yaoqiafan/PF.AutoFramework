using Prism.Mvvm;
using PF.Core.Entities.Configuration;
using System;

namespace PF.Modules.Parameter.ViewModels
{
    /// <summary>参数类型 ViewModel</summary>
    public class ParamTypeViewModel : BindableBase
    {
        /// <summary>获取或设置类型实例</summary>
        public Type TypeInstence { get; set; }
        /// <summary>获取或设置名称</summary>
        public string Name { get; set; }

        private string[] _Category;
        /// <summary>获取或设置分类</summary>
        public string[] Category
        {
            get => _Category;
            set => SetProperty(ref _Category, value);
        }
    }

    /// <summary>参数项 ViewModel</summary>
    public class ParamItemViewModel : BindableBase
    {
        private string _name;
        /// <summary>获取或设置参数名称</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _description;
        /// <summary>获取或设置参数描述</summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _typeFullName;
        /// <summary>获取或设置类型全名</summary>
        public string TypeFullName
        {
            get => _typeFullName;
            set
            {
                if (SetProperty(ref _typeFullName, value))
                {
                    InvalidateDisplayValue();

                    // 【核心修改】：拦截数据类型的切换，自动防呆赋初始 JSON 值，防止崩溃
                    if (value == typeof(string).FullName)
                        JsonValue = "\"\"";
                    else if (value == typeof(int).FullName || value == typeof(double).FullName ||
                             value == typeof(float).FullName || value == typeof(long).FullName)
                        JsonValue = "0";
                    else if (value == typeof(bool).FullName)
                        JsonValue = "false";
                    else
                        JsonValue = "{}";
                }
            }
        }

        private string _jsonValue;
        /// <summary>获取或设置JSON值</summary>
        public string JsonValue
        {
            get => _jsonValue;
            set
            {
                if (SetProperty(ref _jsonValue, value))
                {
                    InvalidateDisplayValue();
                }
            }
        }

        private string _displayValue;
        /// <summary>
        /// 获取参数值的可读摘要（由 TypeFullName + JsonValue 派生，首次读取时计算并缓存）。
        /// 批量加载时在后台线程预先读取一次，避免在 UI 线程上做反序列化。
        /// </summary>
        public string DisplayValue => _displayValue ??= ParamValueFormatter.Format(TypeFullName, JsonValue);

        private void InvalidateDisplayValue()
        {
            _displayValue = null;
            RaisePropertyChanged(nameof(DisplayValue));
        }

        private string _category;
        /// <summary>获取或设置参数分类</summary>
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        /// <summary>获取或设置参数ID</summary>
        public string Id { get; set; }
        /// <summary>获取或设置更新时间</summary>
        public DateTime UpdateTime { get; set; }
        /// <summary>获取或设置参数类型</summary>
        public string ParamType { get; set; }
    }
}