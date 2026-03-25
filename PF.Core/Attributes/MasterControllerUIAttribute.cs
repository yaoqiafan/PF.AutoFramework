
using System;

namespace PF.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MasterControllerUIAttribute : Attribute
    {
        public string ViewName { get; }
        public string ViewModelName { get; } // 新增

        public MasterControllerUIAttribute(string viewName, string viewModelName = null)
        {
            ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));
            // 如果不传，默认按 "ViewName + ViewModel" 规则推导
            ViewModelName = string.IsNullOrWhiteSpace(viewModelName) ? $"{viewName}Model" : viewModelName;
        }
    }
}