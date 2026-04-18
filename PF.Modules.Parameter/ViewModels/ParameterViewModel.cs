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
    /// <summary>
    /// 系统参数配置管理主界面的 ViewModel。
    /// 负责管理参数的分类展示、增删改查（CRUD）、历史查看、恢复默认值，
    /// 并结合当前登录用户的角色级别动态控制界面的操作权限。
    /// </summary>
    public class ParameterViewModel : RegionViewModelBase
    {
        private readonly IParamService _paramService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUserService _userService;
        private readonly IDefaultParam _defaultParam;

        /// <summary>
        /// 实例化 <see cref="ParameterViewModel"/>。
        /// </summary>
        /// <param name="paramService">参数数据服务</param>
        /// <param name="eventAggregator">Prism 事件聚合器</param>
        /// <param name="userService">用户服务，用于权限校验</param>
        /// <param name="defaultParam">默认参数供应服务</param>
        /// <param name="messageService">全局消息/弹窗服务</param>
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

            // 初始化左侧/顶部的参数数据表大类列表
            InitializeParamTypes();

            // 初始化界面按钮与功能权限计算
            UpdatePermissions();
            RaisePropertyChanged(nameof(CanRefreshAndReset));

            // 订阅全局用户变更事件，以动态刷新权限
            if (_userService != null)
            {
                _userService.CurrentUserChanged += OnCurrentUserChanged;
            }

            // 订阅底层参数服务发生变更的事件，以保持多端数据同步
            if (_paramService != null)
            {
                _paramService.ParamChanged += OnParamChanged;
            }
        }

        #region Navigation

        /// <summary>
        /// 拦截 Prism 的页面导航进入事件。
        /// 支持通过路由参数 "TargetParamType" 自动定位并展开对应的参数大类。
        /// </summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("TargetParamType"))
            {
                string targetType = navigationContext.Parameters.GetValue<string>("TargetParamType");

                if (targetType != null && targetType.Contains("_"))
                {
                    // 解析出目标类名，例如从 "Param_SystemConfigParam" 提取 "SystemConfigParam"
                    var paramtype = targetType.Split("_")[1];
                    var match = ParamTypes.FirstOrDefault(p => p.TypeInstence.Name == paramtype);

                    if (match != null)
                    {
                        if (SelectedParamType != match)
                        {
                            SelectedParamType = match; // 触发 setter 自动加载数据
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
                // 如果没有路由参数且当前未选中任何类型，则默认选中第一个类型
                if (SelectedParamType == null && ParamTypes.Any())
                {
                    SelectedParamType = ParamTypes.First();
                }
            }
        }

        #endregion

        #region Properties & Permissions

        /// <summary>
        /// 获取一个值，指示当前登录用户是否具有刷新和重置默认值的权限（超级管理员及以上）。
        /// </summary>
        public bool CanRefreshAndReset => _userService.CurrentUser != null && _userService.CurrentUser.Root >= UserLevel.SuperUser;

        private bool _canAddAndDelete;
        /// <summary>
        /// 获取或设置一个值，指示当前用户是否允许添加或删除参数。
        /// 结合具体选中的参数类型（如用户表或普通配置表）动态计算。
        /// </summary>
        public bool CanAddAndDelete
        {
            get => _canAddAndDelete;
            set => SetProperty(ref _canAddAndDelete, value);
        }

        /// <summary>
        /// 获取系统支持的所有参数实体大类集合（如系统参数、硬件参数、用户表等）。
        /// </summary>
        public ObservableCollection<ParamTypeViewModel> ParamTypes { get; private set; }

        /// <summary>
        /// 获取当前参数类别下，允许新建的具体 C# 数据类型名称集合。
        /// </summary>
        public ObservableCollection<string> AvailableTypes { get; } = new ObservableCollection<string>();

        private ParamTypeViewModel _selectedParamType;
        /// <summary>
        /// 获取或设置当前选中的参数大类，并在切换时触发子分类重置、权限重新计算和数据加载。
        /// </summary>
        public ParamTypeViewModel SelectedParamType
        {
            get => _selectedParamType;
            set
            {
                if (SetProperty(ref _selectedParamType, value))
                {
                    SelectCategory = 0; // 切换大类时，子分类标签默认切回"全部"
                    UpdatePermissions(); // 动态计算该表下的添加/删除权限
                    UpdateAvailableTypes(); // 切换大类时动态更新允许新增的数据类型
                    _ = LoadParametersAsync();
                }
            }
        }

        private int _SelectCategory = 0;
        /// <summary>
        /// 获取或设置当前选中的参数子分类（如"全部"、"视觉"、"轴"等）。
        /// 绑定到 UI 的 TabControl 索引。
        /// </summary>
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
        /// <summary>
        /// 获取或设置当前网格中展示的具体参数明细列表。
        /// </summary>
        public ObservableCollection<ParamItemViewModel> Parameters
        {
            get => _parameters;
            set => SetProperty(ref _parameters, value);
        }

        private ParamItemViewModel _selectedParameter;
        /// <summary>
        /// 获取或设置数据网格中当前选中的单条参数项。
        /// </summary>
        public ParamItemViewModel SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        private bool _isLoading;
        /// <summary>
        /// 获取或设置一个值，指示当前是否正在执行耗时的加载或保存操作（用于控制 UI 遮罩/等待动画）。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage;
        /// <summary>
        /// 获取或设置底部状态栏的提示文本。
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _totalCount;
        /// <summary>
        /// 获取或设置当前分类下的参数总数。
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _modifiedCount;
        /// <summary>
        /// 获取或设置当前尚未保存的修改或新增的参数项数量。
        /// </summary>
        public int ModifiedCount
        {
            get => _modifiedCount;
            set => SetProperty(ref _modifiedCount, value);
        }

        #endregion

        #region Commands

        /// <summary>加载当前选中分类的参数</summary>
        public ICommand LoadCommand { get; private set; }
        /// <summary>将当前网格中的所有修改保存到数据库</summary>
        public ICommand SaveCommand { get; private set; }
        /// <summary>在当前分类下新增一条参数</summary>
        public ICommand AddCommand { get; private set; }
        /// <summary>删除选中的参数</summary>
        public ICommand DeleteCommand { get; private set; }
        /// <summary>重置当前表的所有参数为出厂默认值</summary>
        public ICommand ResetDefaultsCommand { get; private set; }
        /// <summary>查看选中参数的历史变更记录</summary>
        public ICommand ViewHistoryCommand { get; private set; }
        /// <summary>触发弹窗修改当前选中的参数值（JSON 格式编辑）</summary>
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

        /// <summary>
        /// 处理用户登录状态变化事件，实时刷新界面的操作按钮权限。
        /// </summary>
        private void OnCurrentUserChanged(object sender, UserInfo? newUser)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RaisePropertyChanged(nameof(CanRefreshAndReset));
                UpdatePermissions();
            });
        }

        /// <summary>
        /// 动态计算当前登录用户对所选数据表的操作权限。
        /// </summary>
        private void UpdatePermissions()
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null)
            {
                CanAddAndDelete = false;
                return;
            }

            // 对于用户账号管理，管理员级别以上即可操作；普通配置参数则需超级管理员
            if (SelectedParamType?.TypeInstence?.Name == "UserLoginParam")
            {
                CanAddAndDelete = currentUser.Root >= UserLevel.Administrator;
            }
            else
            {
                CanAddAndDelete = currentUser.Root >= UserLevel.SuperUser;
            }
        }

        /// <summary>
        /// 当切换参数大类时，更新对应表允许新增的数据类型。
        /// </summary>
        private void UpdateAvailableTypes()
        {
            AvailableTypes.Clear();
            if (_selectedParamType == null) return;

            // 如果当前处于用户管理表，只允许新增 UserInfo 类型；否则允许基础 C# 类型
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

        /// <summary>
        /// 利用反射扫描系统中所有的参数实体类，并转化为前端可绑定的 ViewModel 集合。
        /// </summary>
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
                    "HardwareParam" => "硬件参数",
                    "UserLoginParam" => "用户登录参数",
                    _ => type.Name
                };

                var paramTypeVm = new ParamTypeViewModel()
                {
                    Name = displayName,
                    TypeInstence = type
                };

                // 异步获取该实体类下存在的所有子分类（Category）
                paramTypeVm.Category = await GetAllCategoryWithType(paramTypeVm);
                ParamTypes.Add(paramTypeVm);
            }
        }

        /// <summary>
        /// 从底层服务异步加载并构建参数列表项。
        /// </summary>
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
                        // 【修复1：删除异常】这里必须使用 FullName，否则底层反射确定仓储实体时会找不到正确的表
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

        /// <summary>
        /// 获取指定参数表内所有不重复的二级分类集合（Category）。
        /// </summary>
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

        /// <summary>
        /// 监听底层服务抛出的参数变更事件，以跨模块同步 UI 状态。
        /// </summary>
        private void OnParamChanged(object sender, ParamChangedEventArgs e)
        {
            if (SelectedParamType != null)
            {
                // 回到 UI 线程刷新数据列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = LoadParametersAsync();
                });

                // 通过 Prism 事件总线通知全系统该参数已被更新
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

        /// <summary>
        /// 触发参数值修改弹窗。
        /// </summary>
        private void OnValueChanged(ParamItemViewModel model)
        {
            if (model == null) return;

            var param = new DialogParameters { { "Data", model } };
            DialogService.ShowDialog(NavigationConstants.Dialogs.CommonChangeParamDialog, param, ValueChangeCallBack);
        }

        /// <summary>
        /// 值修改对话框的回调处理：同步更新当前网格中的内存对象，但不立即写入数据库。
        /// </summary>
        private void ValueChangeCallBack(IDialogResult result)
        {
            if (result.Result == ButtonResult.Yes)
            {
                try
                {
                    var paramItem = result.Parameters.GetValue<ParamItemViewModel>("CallBackParamItem");
                    if (paramItem != null)
                    {
                        // 针对用户信息类型的特殊处理：将解析后对象的 UserName 同步到参数表的 Name 主键上
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

        /// <summary>
        /// 批量保存当前网格中的所有参数更改至数据库。
        /// </summary>
        private async Task SaveChangesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存参数...";

                // 【关键修复】：这里必须加上 .ToList()，创建一个列表副本！
                // 因为 SaveParameterAsync 内部调用服务后会触发 ParamChanged 事件，
                // 导致 OnParamChanged 方法调用 LoadParametersAsync 清空原 Parameters 集合，
                // 若不使用副本，会引发集合在 foreach 遍历过程中被修改的 InvalidOperationException 异常。
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

                // 保存完成后重新拉取最新数据
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

        /// <summary>
        /// 调用底层服务持久化单条参数实例。
        /// </summary>
        private async Task SaveParameterAsync(ParamItemViewModel paramVm)
        {
            try
            {
                // 反射定位参数对应的真实数据类型
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

                // 将 JSON 字符串反序列化为真实的强类型对象
                object realValue = JsonSerializer.Deserialize(paramVm.JsonValue, valueType);
                if (realValue == null) throw new Exception("参数值不能为空");

                // 【关键修复点】：不再使用泛型推导 (dynamic)realValue，避免触发 where T : class 约束
                // 显式传入 paramVm.ParamType (如 PF.Data.Entity.Category.SystemConfigParam) 保证参数被写入正确的表中
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

        /// <summary>
        /// 在界面列表新增一行默认的待保存参数。
        /// </summary>
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

            // 给定初始占位名称
            string newName = isUserParam ? $"User_{DateTime.Now:HHmmss}" : $"NewParam_{DateTime.Now:HHmmss}";
            string defaultJson = "\"New Value\"";

            // 【修复2：默认权限】如果是添加用户，直接给定操作员的默认权限集并生成完整的 JSON 结构，避免解析为空
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
                        // 默认赋予操作员最基础的核心页面权限
                        NavigationConstants.Views.LoggingListView,
                        NavigationConstants.Views.HomeView,
                        NavigationConstants.Views.MainView,
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
                // 【修复1：删除异常】新增的对象也必须使用 FullName 映射正确的物理表
                ParamType = SelectedParamType.TypeInstence.FullName,
            };

            Parameters.Add(newParam);
            SelectedParameter = newParam;

            UpdateCounts();
            ModifiedCount++;
            StatusMessage = "已添加新参数（未保存）";
        }

        /// <summary>
        /// 删除指定的参数实例（连带数据库持久化删除）。
        /// </summary>
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

        /// <summary>
        /// 查看参数历史记录（桩方法）。
        /// </summary>
        private async void ViewParameterHistory(object parameterObj)
        {
            if (parameterObj is not ParamItemViewModel paramVm) return;
            await MessageService.ShowMessageAsync($"查看参数 '{paramVm.Name}' 的历史记录功能暂未实现。", "提示");
        }

        /// <summary>
        /// 批量将当前所选大类的数据表重置为系统出厂默认值。
        /// </summary>
        private async Task ResetToDefaultsAsync(IDefaultParam DefaultParameters)
        {
            if (await MessageService.ShowMessageAsync("确定要重置当前类型的所有参数为默认值吗？\n这将覆盖现有数据！", "确认重置",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != ButtonResult.Yes) return;

            try
            {
                IsLoading = true;
                StatusMessage = "正在重置...";
                bool success = false;

                if (SelectedParamType == null) return;

                switch (SelectedParamType.TypeInstence.Name)
                {
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

        /// <summary>
        /// 更新当前的参数计数统计信息。
        /// </summary>
        private void UpdateCounts()
        {
            TotalCount = Parameters.Count;
        }

        /// <summary>
        /// 清理 ViewModel 占用的全局事件订阅资源。
        /// </summary>
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

    /// <summary>
    /// 全局参数更新通知事件，基于 Prism 的 PubSubEvent 实现。
    /// </summary>
    public class ParamUpdatedEvent : PubSubEvent<ParamUpdateInfo> { }

    /// <summary>
    /// 参数更新通知所携带的载荷信息模型。
    /// </summary>
    public class ParamUpdateInfo
    {
        /// <summary>参数名称（键）</summary>
        public string ParamName { get; set; }

        /// <summary>所属子分类</summary>
        public string Category { get; set; }

        /// <summary>最后修改发生的时间</summary>
        public DateTime ChangeTime { get; set; }
    }

    #endregion
}