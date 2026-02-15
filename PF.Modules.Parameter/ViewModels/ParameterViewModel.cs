using PF.CommonTools.Reflection;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Data.Entity;
using PF.Data.Entity.Category.Basic;
using PF.UI.Infrastructure.Dialog.Basic;
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
        private readonly IUserService _userService;
        private readonly IDefaultParam _defaultParam;

        public ParameterViewModel(
            IParamService paramService,
            IEventAggregator eventAggregator,
            IUserService userService,
            IDefaultParam defaultParam,
            IMessageService messageService)
        {
            _paramService = paramService;
            _eventAggregator = eventAggregator;
            _userService = userService;
            _defaultParam = defaultParam;

            InitializeCommands();

            // 初始化参数类型列表
            InitializeParamTypes();

            // 初始化权限计算
            UpdatePermissions();
            RaisePropertyChanged(nameof(CanRefreshAndReset));

            if (_userService != null)
            {
                _userService.CurrentUserChanged += OnCurrentUserChanged;
            }

            if (_paramService != null)
            {
                _paramService.ParamChanged += OnParamChanged;
            }
        }

        #region Navigation

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("TargetParamType"))
            {
                string targetType = navigationContext.Parameters.GetValue<string>("TargetParamType");

                if (targetType != null && targetType.Contains("_"))
                {
                    var paramtype = targetType.Split("_")[1];
                    var match = ParamTypes.FirstOrDefault(p => p.TypeInstence.Name == paramtype);

                    if (match != null)
                    {
                        if (SelectedParamType != match)
                        {
                            SelectedParamType = match;
                        }
                        else
                        {
                            _ = LoadParametersAsync();
                        }
                    }
                }
            }
            else
            {
                if (SelectedParamType == null && ParamTypes.Any())
                {
                    SelectedParamType = ParamTypes.First();
                }
            }
        }

        #endregion

        #region Properties & Permissions

        public bool CanRefreshAndReset => _userService.CurrentUser != null && _userService.CurrentUser.Root >= UserLevel.SuperUser;

        private bool _canAddAndDelete;
        public bool CanAddAndDelete
        {
            get => _canAddAndDelete;
            set => SetProperty(ref _canAddAndDelete, value);
        }

        public ObservableCollection<ParamTypeViewModel> ParamTypes { get; private set; }

        public ObservableCollection<string> AvailableTypes { get; } = new ObservableCollection<string>();

        private ParamTypeViewModel _selectedParamType;
        public ParamTypeViewModel SelectedParamType
        {
            get => _selectedParamType;
            set
            {
                if (SetProperty(ref _selectedParamType, value))
                {
                    SelectCategory = 0; // 重置为"全部"
                    UpdatePermissions(); // 动态计算添加/删除权限
                    UpdateAvailableTypes(); // 切换大类时动态更新可选的数据类型
                    _ = LoadParametersAsync();
                }
            }
        }

        private int _SelectCategory = 0;
        public int SelectCategory
        {
            get => _SelectCategory;
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

        private int _modifiedCount;
        public int ModifiedCount
        {
            get => _modifiedCount;
            set => SetProperty(ref _modifiedCount, value);
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

        #region Initialization & Logic

        private void InitializeCommands()
        {
            LoadCommand = new DelegateCommand(async () => await LoadParametersAsync());
            SaveCommand = new DelegateCommand(async () => await SaveChangesAsync());
            AddCommand = new DelegateCommand(AddNewParameter);
            DeleteCommand = new DelegateCommand<object>(async obj => await DeleteParameterAsync(obj));
            ResetDefaultsCommand = new DelegateCommand(async () => await ResetToDefaultsAsync(_defaultParam));
            ViewHistoryCommand = new DelegateCommand<object>(ViewParameterHistory);
            ChangeValueCommand = new DelegateCommand<ParamItemViewModel>(OnValueChanged);
        }

        private void OnCurrentUserChanged(object sender, UserInfo? newUser)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RaisePropertyChanged(nameof(CanRefreshAndReset));
                UpdatePermissions();
            });
        }

        private void UpdatePermissions()
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null)
            {
                CanAddAndDelete = false;
                return;
            }

            if (SelectedParamType?.TypeInstence?.Name == "UserLoginParam")
            {
                CanAddAndDelete = currentUser.Root >= UserLevel.Administrator;
            }
            else
            {
                CanAddAndDelete = currentUser.Root >= UserLevel.SuperUser;
            }
        }

        private void UpdateAvailableTypes()
        {
            AvailableTypes.Clear();
            if (_selectedParamType == null) return;

            if (_selectedParamType.TypeInstence.Name == "UserLoginParam")
            {
                AvailableTypes.Add("PF.Core.Entities.Identity.UserInfo");
            }
            else
            {
                AvailableTypes.Add(typeof(string).FullName);
                AvailableTypes.Add(typeof(int).FullName);
                AvailableTypes.Add(typeof(double).FullName);
                AvailableTypes.Add(typeof(bool).FullName);
                AvailableTypes.Add(typeof(float).FullName);
                AvailableTypes.Add(typeof(long).FullName);
            }
        }

        private async void InitializeParamTypes()
        {
            var paramEntityTypes = TypeScanner<ParamEntity>.GetAllTypes();
            ParamTypes = new ObservableCollection<ParamTypeViewModel>();

            foreach (var type in paramEntityTypes)
            {
                string displayName = type.Name switch
                {
                    "CommonParam" => "通用参数",
                    "SystemConfigParam" => "系统配置参数",
                    "UserLoginParam" => "用户登录参数",
                    _ => type.Name
                };

                var paramTypeVm = new ParamTypeViewModel()
                {
                    Name = displayName,
                    TypeInstence = type
                };

                paramTypeVm.Category = await GetAllCategoryWithType(paramTypeVm);
                ParamTypes.Add(paramTypeVm);
            }
        }

        private async Task LoadParametersAsync()
        {
            try
            {
                if (SelectedParamType == null) return;

                IsLoading = true;
                StatusMessage = "正在加载参数...";
                Parameters.Clear();

                string selectedCategory = "全部";
                if (SelectedParamType.Category != null && SelectCategory >= 0 && SelectCategory < SelectedParamType.Category.Length)
                {
                    selectedCategory = SelectedParamType.Category[SelectCategory];
                }

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
                        // 【修复1：删除异常】这里必须使用 FullName，否则底层删除时反射找不到正确的表
                        ParamType = SelectedParamType.TypeInstence.FullName,
                        JsonValue = paramInfo.Value?.ToString() ?? ""
                    });
                }

                UpdateCounts();
                StatusMessage = $"已加载 {Parameters.Count} 个参数";
                ModifiedCount = 0;
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
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
                       .Prepend("全部")
                       .ToArray();
            }
            catch
            {
                return new string[] { "全部" };
            }
        }

        private void OnParamChanged(object sender, ParamChangedEventArgs e)
        {
            if (SelectedParamType != null)
            {
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

        #region CRUD Operations

        private void OnValueChanged(ParamItemViewModel model)
        {
            if (model == null) return;

            var param = new DialogParameters { { "Data", model } };
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
                        // 同步用户参数的 Name 作为主键
                        if (paramItem.TypeFullName == typeof(UserInfo).FullName)
                        {
                            var userInfo = JsonSerializer.Deserialize<UserInfo>(paramItem.JsonValue);
                            if (userInfo != null && !string.IsNullOrWhiteSpace(userInfo.UserName))
                            {
                                paramItem.Name = userInfo.UserName;
                                paramItem.Description = $"用户账号: {userInfo.UserName}";
                            }
                        }

                        // 注释掉此行以防止覆盖内存中未保存的新增项
                        // _ = LoadParametersAsync(); 
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"同步参数名称失败: {ex.Message}");
                }
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存参数...";

                // 【关键修复】：这里必须加上 .ToList()，创建一个列表副本！
                // 这样即使保存时触发了事件导致原 Parameters 被清空，也不会影响这里的循环遍历。
                var modifiedParams = Parameters.ToList();

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
               await MessageService.ShowMessageAsync($"保存参数时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                Type valueType = Type.GetType(paramVm.TypeFullName);
                if (valueType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        valueType = asm.GetType(paramVm.TypeFullName);
                        if (valueType != null) break;
                    }
                }

                if (valueType == null) throw new Exception($"找不到类型: {paramVm.TypeFullName}");

                object realValue = JsonSerializer.Deserialize(paramVm.JsonValue, valueType);
                if (realValue == null) throw new Exception("参数值不能为空");

                // 【关键修复点】：不再使用泛型推导 (dynamic)realValue，避免触发 where T : class 约束
                // 显式传入 paramVm.ParamType (如 PF.Data.Entity.Category.SystemConfigParam) 保证参数存在正确的表中
                bool success = await _paramService.SetParamAsync(
                    typeName: paramVm.ParamType,
                    name: paramVm.Name,
                    value: realValue, // 直接作为 object 传入
                    userInfo: _userService.CurrentUser,
                    description: paramVm.Description);

                if (!success) throw new InvalidOperationException($"更新参数 '{paramVm.Name}' 返回失败");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存参数 '{paramVm.Name}' 时出错: {ex.Message}", ex);
            }
        }

        private void AddNewParameter()
        {
            if (SelectedParamType == null) return;

            string currentCategory = "未分类";
            if (SelectedParamType.Category != null && SelectCategory > 0 && SelectCategory < SelectedParamType.Category.Length)
            {
                currentCategory = SelectedParamType.Category[SelectCategory];
            }

            bool isUserParam = SelectedParamType.TypeInstence.Name == "UserLoginParam";
            string defaultType = isUserParam ? "PF.Core.Entities.Identity.UserInfo" : typeof(string).FullName;

            // 给定初始名称
            string newName = isUserParam ? $"User_{DateTime.Now:HHmmss}" : $"NewParam_{DateTime.Now:HHmmss}";
            string defaultJson = "\"New Value\"";

            // 【修复2：默认权限】如果是添加用户，直接给定操作员的默认权限并生成完整 JSON
            if (isUserParam)
            {
                var defaultUser = new UserInfo
                {
                    UserName = newName,
                    UserId = "0000",
                    Root = UserLevel.Operator, // 默认操作员
                    Password = "123",          // 默认密码
                    AccessibleViews = new List<string>
            {
                // 默认赋予操作员最基础的两个页面权限
                NavigationConstants.Views.LoggingListView,
                NavigationConstants.Views.ParameterView_CommonParam
            }
                };
                defaultJson = JsonSerializer.Serialize(defaultUser);
            }

            var newParam = new ParamItemViewModel
            {
                Name = newName,
                Description = isUserParam ? "新用户" : "新参数",
                TypeFullName = defaultType,
                JsonValue = defaultJson,
                Category = currentCategory,
                // 【修复1：删除异常】新增的对象也必须使用 FullName
                ParamType = SelectedParamType.TypeInstence.FullName,
            };

            Parameters.Add(newParam);
            SelectedParameter = newParam;

            UpdateCounts();
            ModifiedCount++;
            StatusMessage = "已添加新参数（未保存）";
        }

        private async Task DeleteParameterAsync(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm) return;

            if (await MessageService.ShowMessageAsync($"确定要删除参数 '{paramVm.Name}' 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != ButtonResult.Yes) return;

            try
            {
                IsLoading = true;

                bool success = await _paramService.DeleteParamAsync(paramVm.ParamType, paramVm.Name, _userService.CurrentUser);

                if (success)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Parameters.Remove(paramVm);
                    });
                    StatusMessage = $"参数 '{paramVm.Name}' 已删除";
                    UpdateCounts();
                }
                else
                {
                    StatusMessage = $"删除参数 '{paramVm.Name}' 失败，可能是对应类型中不存在此参数。";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private async void ViewParameterHistory(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm) return;
            await MessageService.ShowMessageAsync($"查看参数 '{paramVm.Name}' 的历史记录功能暂未实现。", "提示");
        }

        private async Task ResetToDefaultsAsync(IDefaultParam DefaultParameters)
        {
            if (await MessageService.ShowMessageAsync("确定要重置当前类型的所有参数为默认值吗？\n这将覆盖现有数据！", "确认重置",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) !=  ButtonResult.Yes) return;

            try
            {
                IsLoading = true;
                StatusMessage = "正在重置...";
                bool success = false;

                if (SelectedParamType == null) return;

                switch (SelectedParamType.TypeInstence.Name)
                {
                    case "CommonParam":
                        var commonDict = DefaultParameters.GetCommonDefaults();
                        success = await _paramService.BatchSetParamsAsync(commonDict, _userService.CurrentUser, "用户手动重置");
                        break;
                    case "UserLoginParam":
                        var userDict = DefaultParameters.GetUsersDefaults();
                        success = await _paramService.BatchSetParamsAsync(userDict, _userService.CurrentUser, "用户手动重置");
                        break;
                    case "SystemConfigParam":
                        var sysDict = DefaultParameters.GetSystemDefaults();
                        success = await _paramService.BatchSetParamsAsync(sysDict, _userService.CurrentUser, "用户手动重置");
                        break;
                }

                if (success)
                {
                    StatusMessage = "重置成功";
                    await LoadParametersAsync();
                }
                else
                {
                    StatusMessage = "重置操作返回失败或未找到默认定义";
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
            if (_userService != null)
            {
                _userService.CurrentUserChanged -= OnCurrentUserChanged;
            }
        }

        #endregion
    }

    #region Supporting Classes

    public class ParamUpdatedEvent : PubSubEvent<ParamUpdateInfo> { }

    public class ParamUpdateInfo
    {
        public string ParamName { get; set; }
        public string Category { get; set; }
        public DateTime ChangeTime { get; set; }
    }

    #endregion
}