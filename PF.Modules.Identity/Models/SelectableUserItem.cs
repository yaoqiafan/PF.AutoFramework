using PF.Core.Entities.Identity;
using Prism.Mvvm;

namespace PF.Modules.Identity.Models
{
    /// <summary>
    /// 用户管理页面卡片视图 / 列表视图共用的勾选包装，不污染 UserInfo 领域实体（避免被
    /// JsonSerializer 一起序列化进参数库）。
    /// </summary>
    public class SelectableUserItem : BindableBase
    {
        /// <summary>底层用户实体</summary>
        public UserInfo User { get; }

        private bool _isSelected;
        /// <summary>是否已勾选（批量删除用）</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _canSelect;
        /// <summary>是否允许勾选：仅权限等级严格低于当前登录用户时才可选中（同级/更高级不可选，含用户自己）</summary>
        public bool CanSelect
        {
            get => _canSelect;
            set => SetProperty(ref _canSelect, value);
        }

        /// <summary>初始化实例</summary>
        public SelectableUserItem(UserInfo user, bool canSelect)
        {
            User = user;
            _canSelect = canSelect;
        }
    }
}
