using PF.Common.Core.PrismBase;
using PF.CommonTools.Reflection;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Data.Entity;
using PF.Data.Params;
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
            UserInfo currentUser,IDefaultParam defaultParam)
        {
            _paramService = paramService;
            _eventAggregator = eventAggregator;
            _currentUser = currentUser;
            _defaultParam= defaultParam;
            InitializeCommands();
            InitializeParamTypes();

            // 订阅参数更改事件
            _paramService.ParamChanged += OnParamChanged;

            // 初始加载
            _ = LoadParametersAsync();
        }

        #region Properties

        public ObservableCollection<ParamTypeViewModel> ParamTypes { get; private set; }

        private ParamTypeViewModel _selectedParamType;
        public ParamTypeViewModel SelectedParamType
        {
            get => _selectedParamType;
            set
            {
                if (SetProperty(ref _selectedParamType, value))
                {

                    SelectCategory = 0;
                    _ = LoadParametersAsync();
                }
            }
        }

        private int _SelectCategory = 0;
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
            var paramEntityTypes = TypeScanner<ParamEntity>.GetAllTypes();
            ParamTypes = new ObservableCollection<ParamTypeViewModel>();
            for (int i = 0; i < paramEntityTypes.Count; i++)
            {
                switch (paramEntityTypes[i].Name)
                {
                    case "CommonParam":
                        ParamTypes.Add(new ParamTypeViewModel() { Name = "通用参数", TypeInstence = paramEntityTypes[i] });
                        break;
                    case "SystemConfigParam":
                        ParamTypes.Add(new ParamTypeViewModel() { Name = "系统配置参数", TypeInstence = paramEntityTypes[i] });
                        break;
                    case "UserLoginParam":
                        ParamTypes.Add(new ParamTypeViewModel() { Name = "用户登录参数", TypeInstence = paramEntityTypes[i] });
                        break;

                }
                ParamTypes[i].Category = await GetAllCategoryWithType(ParamTypes[i]);
            }


            SelectedParamType = ParamTypes.First();
        }

        private void OnValueChanged(ParamItemViewModel model)
        {
            var param = new DialogParameters();
            param.Add("Data", model);
            DialogService.ShowDialog(NavigationConstants.Dialogs.CommonChangeParamDialog, param,ValueChangeCallBack);

        }

        private void ValueChangeCallBack(IDialogResult result)
        {
            // 如果用户点击了确定按钮
            if (result.Result == ButtonResult.Yes)
            {
                IDialogParameters paras = result.Parameters;
                try
                {
                    // 从对话框参数中获取回调的树节点
                    var paramItem = paras["CallBackParamItem"] as ParamItemViewModel;

                    SelectedParameter = paramItem;


                }
                catch (Exception)
                {
                    // 异常处理：忽略转换异常
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnParamChanged(object sender, ParamChangedEventArgs e)
        {
            // 如果有参数被更改，重新加载当前类型的参数
            if (e.Category == SelectedParamType.Category[SelectCategory])
            {
                _ = LoadParametersAsync();

                // 通过事件聚合器通知其他组件参数已更改
                _eventAggregator.GetEvent<ParamUpdatedEvent>().Publish(new ParamUpdateInfo
                {
                    ParamName = e.ParamName,
                    Category = e.Category,
                    ChangeTime = e.ChangeTime
                });
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadParametersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载参数...";

                Parameters.Clear();


                if (SelectedParamType != null)
                {
                    var paramInfos = await _paramService.GetParamsByCategoryAsync(SelectedParamType?.TypeInstence.FullName, SelectedParamType.Category[SelectCategory]);

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
                            JsonValue = paramInfo.Value.ToString()

                        });
                    }
                }


                UpdateCounts();
                StatusMessage = $"已加载 {Parameters.Count} 个参数";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
                MessageBox.Show($"加载参数时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadParameterValueAsync(ParamItemViewModel paramVm)
        {
            try
            {
                // 根据参数类型加载实际值
                object paramValue = null;

                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        paramValue = await _paramService.GetParamAsync<object>(paramVm.Name);
                        break;

                    case "UserLoginParam":
                        paramValue = await _paramService.GetParamAsync<UserInfo>(paramVm.Name);
                        break;

                    case "SystemConfigParam":
                        paramValue = await _paramService.GetParamAsync<object>(paramVm.Name);
                        break;
                }

                if (paramValue != null)
                {
                    paramVm.JsonValue = JsonSerializer.Serialize(paramValue);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续执行
                Console.WriteLine($"加载参数值失败: {ex.Message}");
            }
        }


        private async Task<string[]> GetAllCategoryWithType(ParamTypeViewModel paramType)
        {

            var paramInfos = await _paramService.GetParamsByCategoryAsync(paramType.TypeInstence.FullName);

            return paramInfos
                   .Where(p => !string.IsNullOrEmpty(p.Category))
                   .Select(p => p.Category)
                   .Distinct()
                   .Prepend("全部")  // 在序列开头添加元素
                   .ToArray();
        }


        #endregion

        #region CRUD Operations

        private async Task SaveChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存参数...";

                var modifiedParams = Parameters;

                if (!modifiedParams.Any())
                {
                    StatusMessage = "没有需要保存的修改";
                    return;
                }

                foreach (var paramVm in modifiedParams)
                {
                    await SaveParameterAsync(paramVm);
                }

                UpdateCounts();
                StatusMessage = $"成功保存 {modifiedParams.Count} 个参数";

                // 重新加载以确保获取最新数据
                await LoadParametersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败: {ex.Message}";
                MessageBox.Show($"保存参数时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        var commonValue = DeserializeValue<object>(paramVm.JsonValue, paramVm.TypeFullName);
                        if (commonValue != null)
                        {
                            success = await _paramService.SetParamAsync(paramVm.Name, commonValue, _currentUser, paramVm.Description);
                        }
                        break;

                    case "UserLoginParam":
                        var userValue = DeserializeValue<UserInfo>(paramVm.JsonValue, paramVm.TypeFullName);
                        if (userValue != null)
                        {
                            success = await _paramService.SetParamAsync(paramVm.Name, userValue, _currentUser, paramVm.Description);
                        }
                        break;

                    case "SystemConfigParam":
                        var sysValue = DeserializeValue<object>(paramVm.JsonValue, paramVm.TypeFullName);
                        if (sysValue != null)
                        {
                            success = await _paramService.SetParamAsync(paramVm.Name, sysValue, _currentUser, paramVm.Description);
                        }
                        break;
                }

                if (!success)
                {
                    throw new InvalidOperationException($"更新参数 '{paramVm.Name}' 失败");
                }



            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存参数 '{paramVm.Name}' 时出错: {ex.Message}", ex);
            }
        }

        private T DeserializeValue<T>(string jsonValue, string typeFullName) where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(jsonValue))
                    return default;

                // 尝试使用指定的类型进行反序列化
                if (!string.IsNullOrEmpty(typeFullName))
                {
                    var type = Type.GetType(typeFullName);
                    if (type != null && type != typeof(T))
                    {
                        // 如果类型不匹配，使用实际类型反序列化
                        var result = JsonSerializer.Deserialize(jsonValue, type);
                        return result as T;
                    }
                }

                // 使用默认类型反序列化
                return JsonSerializer.Deserialize<T>(jsonValue);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"反序列化值时出错: {ex.Message}", ex);
            }
        }

        private void AddNewParameter()
        {
            var newParam = new ParamItemViewModel
            {
                Name = $"NewParameter_{DateTime.Now:HHmmss}",
                Description = "新参数",
                TypeFullName = typeof(string).FullName,
                JsonValue = JsonSerializer.Serialize(""),
                Category = string.Empty,
                ParamType = SelectedParamType.TypeInstence.Name,
            };

            Parameters.Add(newParam);
            SelectedParameter = newParam;
            UpdateCounts();
            StatusMessage = "已添加新参数";
        }

        private async void DeleteParameter(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm)
                return;

            if (MessageBox.Show($"确定要删除参数 '{paramVm.Name}' 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;

                bool success = await _paramService.DeleteParamAsync(paramVm.Name, _currentUser);

                if (success)
                {
                    // 从UI删除
                    Parameters.Remove(paramVm);
                    StatusMessage = $"参数 '{paramVm.Name}' 已删除";
                    UpdateCounts();
                }
                else
                {
                    StatusMessage = $"删除参数 '{paramVm.Name}' 失败";
                    MessageBox.Show("删除参数失败，请稍后重试", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
                MessageBox.Show($"删除参数时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void ViewParameterHistory(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm)
                return;

            // 这里可以打开参数历史记录窗口
            // 需要实现历史记录服务
            MessageBox.Show($"查看参数 '{paramVm.Name}' 的历史记录", "历史记录",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Default Parameters

        private async Task ResetToDefaultsAsync(IDefaultParam DefaultParameters)
        {
            if (MessageBox.Show("确定要重置为默认参数吗？这会覆盖现有参数！", "确认重置",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "正在重置为默认参数...";

                // 获取默认参数
                Dictionary<string, object> defaultParams = null;

                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        defaultParams = DefaultParameters.GetCommonDefaults()
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;

                    case "UserLoginParam":
                        defaultParams = DefaultParameters.GetUsersDefaults()
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;

                    case "SystemConfigParam":
                        defaultParams = DefaultParameters.GetSystemDefaults()
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        break;
                }

                if (defaultParams != null && defaultParams.Any())
                {
                    // 批量设置默认参数
                    bool success = await _paramService.BatchSetParamsAsync(defaultParams, _currentUser, "系统重置为默认值");

                    if (success)
                    {
                        StatusMessage = $"已重置 {defaultParams.Count} 个默认参数";

                        // 重新加载
                        await LoadParametersAsync();
                    }
                    else
                    {
                        StatusMessage = "重置参数失败";
                    }
                }
                else
                {
                    StatusMessage = "没有找到默认参数";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"重置失败: {ex.Message}";
                MessageBox.Show($"重置参数时出错: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Helper Methods


        private void UpdateCounts()
        {
            TotalCount = Parameters.Count;
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            // 取消事件订阅
            if (_paramService != null)
            {
                _paramService.ParamChanged -= OnParamChanged;
            }
        }

        #endregion
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
}
