using Prism.Mvvm;
using PF.Core.Entities.Configuration;
using System;

namespace PF.Modules.Parameter.ViewModels
{
    public class ParamTypeViewModel : BindableBase
    {
        public Type TypeInstence { get; set; }
        public string Name { get; set; }

        private string[] _Category;
        public string[] Category
        {
            get => _Category;
            set => SetProperty(ref _Category, value);
        }
    }

    public class ParamItemViewModel : BindableBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _typeFullName;
        public string TypeFullName
        {
            get => _typeFullName;
            set
            {
                if (SetProperty(ref _typeFullName, value))
                {
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
        public string JsonValue
        {
            get => _jsonValue;
            set => SetProperty(ref _jsonValue, value);
        }

        private string _category;
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public string Id { get; set; }
        public DateTime UpdateTime { get; set; }
        public string ParamType { get; set; }
    }
}