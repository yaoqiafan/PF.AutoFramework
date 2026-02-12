using PF.CommonTools.Reflection;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Data.Entity;
using PF.Data.Entity.Category.Basic;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace PF.Modules.Parameter.ViewModels
{
    public class ParameterViewModel : RegionViewModelBase
    {
        private readonly IParamService _paramService;
        private readonly IEventAggregator _eventAggregator;
        private readonly UserInfo _currentUser;
        private readonly IDefaultParam _defaultParam;

        public ParameterViewModel(
            IParamService paramService,
            IEventAggregator eventAggregator,
            UserInfo currentUser,
            IDefaultParam defaultParam)
        {
            _paramService = paramService;
            _eventAggregator = eventAggregator;
            _currentUser = currentUser;
            _defaultParam = defaultParam;

            InitializeCommands();

            // 初始化参数类型列表（不立即加载数据，等待导航触发）
            InitializeParamTypes();

            // 订阅参数变更事件，用于多端同步刷新
            _paramService.ParamChanged += OnParamChanged;
        }

        #region Navigation (核心修改区域)

        /// <summary>
        /// 当导航进入此页面时触发
        /// </summary>
        /// <param name="navigationContext"></param>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            // 1. 获取导航传递的参数类型名称 (例如: "CommonParam", "SystemConfigParam")
            if (navigationContext.Parameters.ContainsKey("TargetParamType"))
            {
                string targetType = navigationContext.Parameters.GetValue<string>("TargetParamType");

                if (targetType != null) 
                {
                    if (targetType.Contains("_"))
                    {
                        var paramtype = targetType.Split("_")[1];
                        // 2. 在已加载的类型列表中查找匹配项
                        var match = ParamTypes.FirstOrDefault(p => p.TypeInstence.Name == paramtype);

                        if (match != null)
                        {
                            // 3. 如果匹配到了，且当前未选中或选中的不是同一个，则切换
                            // 切换 SelectedParamType 会自动触发 Set 里的 LoadParametersAsync
                            if (SelectedParamType != match)
                            {
                                SelectedParamType = match;
                            }
                            else
                            {
                                // 如果已经是当前类型，强制刷新一下数据
                                _ = LoadParametersAsync();
                            }
                        }
                    }
                }
               
            }
            else
            {
                // 如果没有传参（比如首次初始化），且当前没有选中项，默认选中第一个
                if (SelectedParamType == null && ParamTypes.Any())
                {
                    SelectedParamType = ParamTypes.First();
                }
            }
        }

        #endregion

        #region Properties

        // 支持的参数类型列表（通用、系统、用户等）
        public ObservableCollection<ParamTypeViewModel> ParamTypes { get; private set; }

        private ParamTypeViewModel _selectedParamType;
        /// <summary>
        /// 当前选中的参数类型（由导航自动设置）
        /// </summary>
        public ParamTypeViewModel SelectedParamType
        {
            get => _selectedParamType;
            set
            {
                // 当类型改变时，重置分类索引并重新加载数据
                if (SetProperty(ref _selectedParamType, value))
                {
                    SelectCategory = 0; // 重置为"全部"
                    _ = LoadParametersAsync();
                }
            }
        }

        private int _SelectCategory = 0;
        /// <summary>
        /// 当前选中的具体分类索引（对应 SelectedParamType.Category 数组）
        /// </summary>
        public int SelectCategory
        {
            get { return _SelectCategory; }
            set
            {
                if (SetProperty(ref _SelectCategory, value))
                {
                    _ = LoadParametersAsync();
                }
            }
        }

        private ObservableCollection<ParamItemViewModel> _parameters = new();
        public ObservableCollection<ParamItemViewModel> Parameters
        {
            get => _parameters;
            set => SetProperty(ref _parameters, value);
        }

        private ParamItemViewModel _selectedParameter;
        public ParamItemViewModel SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        #endregion

        #region Commands

        public ICommand LoadCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand AddCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ResetDefaultsCommand { get; private set; }
        public ICommand ViewHistoryCommand { get; private set; }
        public ICommand ChangeValueCommand { get; set; }

        #endregion

        #region Initialization

        private void InitializeCommands()
        {
            LoadCommand = new DelegateCommand(async () => await LoadParametersAsync());
            SaveCommand = new DelegateCommand(async () => await SaveChangesAsync());
            AddCommand = new DelegateCommand(AddNewParameter);
            DeleteCommand = new DelegateCommand<object>(DeleteParameter);
            ResetDefaultsCommand = new DelegateCommand(async () => await ResetToDefaultsAsync(_defaultParam));
            ViewHistoryCommand = new DelegateCommand<object>(ViewParameterHistory);
            ChangeValueCommand = new DelegateCommand<ParamItemViewModel>(OnValueChanged);
        }

        private async void InitializeParamTypes()
        {
            // 扫描所有继承自 ParamEntity 的类型
            var paramEntityTypes = TypeScanner<ParamEntity>.GetAllTypes();
            ParamTypes = new ObservableCollection<ParamTypeViewModel>();

            for (int i = 0; i < paramEntityTypes.Count; i++)
            {
                string displayName = paramEntityTypes[i].Name;
                // 设置友好的显示名称
                switch (paramEntityTypes[i].Name)
                {
                    case "CommonParam": displayName = "通用参数"; break;
                    case "SystemConfigParam": displayName = "系统配置参数"; break;
                    case "UserLoginParam": displayName = "用户登录参数"; break;
                }

                var paramTypeVm = new ParamTypeViewModel()
                {
                    Name = displayName,
                    TypeInstence = paramEntityTypes[i]
                };

                // 异步加载该类型下的所有 Category 字符串列表
                paramTypeVm.Category = await GetAllCategoryWithType(paramTypeVm);

                ParamTypes.Add(paramTypeVm);
            }
        }

        #endregion

        #region Data Loading & Logic

        /// <summary>
        /// 加载参数列表核心逻辑
        /// </summary>
        private async Task LoadParametersAsync()
        {
            try
            {
                if (SelectedParamType == null) return;

                IsLoading = true;
                StatusMessage = "正在加载参数...";

                Parameters.Clear();

                // 检查分类数组是否越界，防止未加载完成时报错
                string selectedCategory = "全部";
                if (SelectedParamType.Category != null && SelectCategory >= 0 && SelectCategory < SelectedParamType.Category.Length)
                {
                    selectedCategory = SelectedParamType.Category[SelectCategory];
                }

                // 从服务获取数据
                var paramInfos = await _paramService.GetParamsByCategoryAsync(SelectedParamType.TypeInstence.FullName, selectedCategory);

                foreach (var paramInfo in paramInfos)
                {
                    Parameters.Add(new ParamItemViewModel
                    {
                        Id = paramInfo.Id,
                        Name = paramInfo.Name,
                        Description = paramInfo.Description,
                        TypeFullName = paramInfo.TypeName,
                        Category = paramInfo.Category,
                        UpdateTime = paramInfo.UpdateTime,
                        ParamType = SelectedParamType.TypeInstence.Name,
                        JsonValue = paramInfo.Value?.ToString() ?? ""
                    });
                }

                UpdateCounts();
                StatusMessage = $"已加载 {Parameters.Count} 个参数";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
                // 实际生产中建议记录日志，这里仅做简单提示
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<string[]> GetAllCategoryWithType(ParamTypeViewModel paramType)
        {
            try
            {
                var paramInfos = await _paramService.GetParamsByCategoryAsync(paramType.TypeInstence.FullName);

                return paramInfos
                       .Where(p => !string.IsNullOrEmpty(p.Category))
                       .Select(p => p.Category)
                       .Distinct()
                       .Prepend("全部") // 在列表头部插入"全部"选项
                       .ToArray();
            }
            catch
            {
                return new string[] { "全部" };
            }
        }

        private void OnParamChanged(object sender, ParamChangedEventArgs e)
        {
            // 如果后台或其他客户端修改了当前显示的参数类型的参数，进行刷新
            if (SelectedParamType != null)
            {
                // 判断类型是否匹配（简单判断，实际可能需要更严谨的 FullName 判断）
                // 这里的逻辑假设 ParamTypeViewModel.Name 是中文，TypeInstence.Name 是类名
                // e.ParamName 通常无法直接判断类型，但如果在查看全部或对应分类，应该刷新

                // 简单起见，收到任何更新事件都刷新当前列表（如果不想太频繁，可加判断）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = LoadParametersAsync();
                });

                _eventAggregator.GetEvent<ParamUpdatedEvent>().Publish(new ParamUpdateInfo
                {
                    ParamName = e.ParamName,
                    Category = e.Category,
                    ChangeTime = e.ChangeTime
                });
            }
        }

        #endregion

        #region CRUD Operations & Dialogs

        private void OnValueChanged(ParamItemViewModel model)
        {
            if (model == null) return;

            var param = new DialogParameters();
            param.Add("Data", model);
            DialogService.ShowDialog(NavigationConstants.Dialogs.CommonChangeParamDialog, param, ValueChangeCallBack);
        }

        private void ValueChangeCallBack(IDialogResult result)
        {
            if (result.Result == ButtonResult.Yes)
            {
                try
                {
                    var paramItem = result.Parameters.GetValue<ParamItemViewModel>("CallBackParamItem");
                    if (paramItem != null)
                    {
                        // 刷新列表或更新单项
                        _ = LoadParametersAsync();
                    }
                }
                catch (Exception) { }
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存参数...";

                var modifiedParams = Parameters; // 这里假设所有都保存，或者你可以筛选 IsDirty 的项

                if (!modifiedParams.Any())
                {
                    StatusMessage = "没有参数";
                    return;
                }

                foreach (var paramVm in modifiedParams)
                {
                    await SaveParameterAsync(paramVm);
                }

                UpdateCounts();
                StatusMessage = $"成功保存 {modifiedParams.Count} 个参数";
                await LoadParametersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败: {ex.Message}";
                MessageBox.Show($"保存参数时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveParameterAsync(ParamItemViewModel paramVm)
        {
            try
            {
                bool success = false;
                // 根据当前选中的参数类型，将 JsonValue 反序列化为对应类型的对象并保存
                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        var commonValue = DeserializeValue<object>(paramVm.JsonValue, paramVm.TypeFullName);
                        success = await _paramService.SetParamAsync(paramVm.Name, commonValue, _currentUser, paramVm.Description);
                        break;
                    case "UserLoginParam":
                        var userValue = DeserializeValue<UserInfo>(paramVm.JsonValue, paramVm.TypeFullName);
                        success = await _paramService.SetParamAsync(paramVm.Name, userValue, _currentUser, paramVm.Description);
                        break;
                    case "SystemConfigParam":
                        var sysValue = DeserializeValue<object>(paramVm.JsonValue, paramVm.TypeFullName);
                        success = await _paramService.SetParamAsync(paramVm.Name, sysValue, _currentUser, paramVm.Description);
                        break;
                    default:
                        // 处理其他可能的自定义类型
                        var objValue = DeserializeValue<object>(paramVm.JsonValue, paramVm.TypeFullName);
                        success = await _paramService.SetParamAsync(paramVm.Name, objValue, _currentUser, paramVm.Description);
                        break;
                }

                if (!success) throw new InvalidOperationException($"更新参数 '{paramVm.Name}' 返回失败");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存参数 '{paramVm.Name}' 时出错: {ex.Message}", ex);
            }
        }

        private T DeserializeValue<T>(string jsonValue, string typeFullName)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonValue)) return default;

                // 尝试根据全名获取类型
                Type type = null;
                if (!string.IsNullOrEmpty(typeFullName))
                {
                    type = Type.GetType(typeFullName);
                    // 如果 Type.GetType 找不到（可能是未加载的程序集），尝试简单查找
                    if (type == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            type = asm.GetType(typeFullName);
                            if (type != null) break;
                        }
                    }
                }

                if (type != null)
                {
                    return (T)JsonSerializer.Deserialize(jsonValue, type);
                }

                // 降级处理
                return JsonSerializer.Deserialize<T>(jsonValue);
            }
            catch
            {
                // 如果反序列化失败，可能是字符串格式问题，直接返回默认
                return default;
            }
        }

        private void AddNewParameter()
        {
            if (SelectedParamType == null) return;

            var newParam = new ParamItemViewModel
            {
                Name = $"NewParameter_{DateTime.Now:HHmmss}",
                Description = "新参数",
                TypeFullName = typeof(string).FullName,
                JsonValue = "\"New Value\"", // 默认为 JSON 字符串格式
                Category = "Default",
                ParamType = SelectedParamType.TypeInstence.Name,
            };
            Parameters.Add(newParam);
            SelectedParameter = newParam;

            // 滚动到新添加的项
            UpdateCounts();
            StatusMessage = "已添加新参数（未保存）";
        }

        private async void DeleteParameter(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm) return;

            if (MessageBox.Show($"确定要删除参数 '{paramVm.Name}' 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                IsLoading = true;
                bool success = await _paramService.DeleteParamAsync(paramVm.Name, _currentUser);
                if (success)
                {
                    Parameters.Remove(paramVm);
                    StatusMessage = $"参数 '{paramVm.Name}' 已删除";
                    UpdateCounts();
                }
                else
                {
                    StatusMessage = $"删除参数 '{paramVm.Name}' 失败";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private void ViewParameterHistory(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm) return;
            MessageBox.Show($"查看参数 '{paramVm.Name}' 的历史记录功能暂未实现。", "提示");
        }

        private async Task ResetToDefaultsAsync(IDefaultParam DefaultParameters)
        {
            if (MessageBox.Show("确定要重置当前类型的所有参数为默认值吗？\n这将覆盖现有数据！", "确认重置",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                IsLoading = true;
                StatusMessage = "正在重置...";
                Dictionary<string, object> defaultParams = null;

                if (SelectedParamType == null) return;

                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        defaultParams = DefaultParameters.GetCommonDefaults().ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;
                    case "UserLoginParam":
                        defaultParams = DefaultParameters.GetUsersDefaults().ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;
                    case "SystemConfigParam":
                        defaultParams = DefaultParameters.GetSystemDefaults().ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;
                }

                if (defaultParams != null && defaultParams.Any())
                {
                    bool success = await _paramService.BatchSetParamsAsync(defaultParams, _currentUser, "用户手动重置");
                    if (success)
                    {
                        StatusMessage = "重置成功";
                        await LoadParametersAsync();
                    }
                    else
                    {
                        StatusMessage = "重置操作返回失败";
                    }
                }
                else
                {
                    StatusMessage = "未找到该类型的默认参数定义";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"重置失败: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private void UpdateCounts()
        {
            TotalCount = Parameters.Count;
        }

        public void Cleanup()
        {
            if (_paramService != null)
            {
                _paramService.ParamChanged -= OnParamChanged;
            }
        }

        #endregion
    }

}

#region Supporting Classes

public class ParamTypeViewModel : BindableBase
{
    public Type TypeInstence { get; set; }
    public string Name { get; set; }

    private string[] _Category;
    public string[] Category
    {
        get { return _Category; }
        set { SetProperty(ref _Category, value); }
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
        set => SetProperty(ref _typeFullName, value);
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

// 事件定义
public class ParamUpdatedEvent : PubSubEvent<ParamUpdateInfo> { }

public class ParamUpdateInfo
{
    public string ParamName { get; set; }
    public string Category { get; set; }
    public DateTime ChangeTime { get; set; }
}

#endregion

