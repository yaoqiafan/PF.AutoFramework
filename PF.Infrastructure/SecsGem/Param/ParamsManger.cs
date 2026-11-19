using PF.Infrastructure.SecsGem.Tools;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;
using System.Collections.Concurrent;

namespace PF.Infrastructure.SecsGem.Param
{
    /// <summary>
    /// SecsGem参数管理器
    /// </summary>
    public class ParamsManger : IParams
    {
        private readonly ISecsGemDataBase _secsGemDataBase;
        private readonly ICommandManager _commandManager;
        private readonly Dictionary<ParamType, object> _params;
        private readonly object _lock = new object();

        // 参数更改事件
        /// <summary>
        /// 参数变更事件
        /// </summary>
        public event EventHandler<ParamChangedEventArgs> ParamChanged;

        /// <summary>
        /// 配方验证错误事件
        /// </summary>
        public event EventHandler<FormulaValidateErrorEventArgs> FormulaValidateError;

        /// <summary>
        /// 初始化参数配置
        /// </summary>
        public ParamsManger(ISecsGemDataBase secsGemDataBase, ICommandManager commandManager)
        {
            _commandManager = commandManager;
            _secsGemDataBase = secsGemDataBase;
            _params = new Dictionary<ParamType, object>()
            {
                { ParamType.System, new SecsGemSystemParam() },
                { ParamType.Validate, new ValidateConfiguration() },
                { ParamType.Formula, new FormulaConfiguration() },
            };
        }

        /// <summary>
        /// 初始化参数
        /// </summary>
        public async Task<bool> InitializationParams()
        {
            try
            {
                bool res1, res2, res3, res4;

                res1 = await LoadSystemConfiguration();
                res2 = await LoadValidateConfiguration();
                res3 = await LoadFormulaConfiguration();
                res4 = await _commandManager.InitializeAsync((FormulaConfiguration)_params[ParamType.Formula]);


                if (!res4) SecsGemTrace.Warn("SECS/GEM 命令管理器初始化失败");
                return res1 & res2 & res3 & res4;

            }
            catch (Exception ex)
            {
                SecsGemTrace.Warn($"SECS/GEM 参数初始化异常：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取参数值
        /// </summary>
        public T GetParam<T>(ParamType paramType)
        {
            lock (_lock)
            {
                if (!_params.TryGetValue(paramType, out object value))
                {
                    throw new KeyNotFoundException($"Parameter of type {paramType} not found.");
                }

                return ConvertToType<T>(value, paramType);
            }
        }

        /// <summary>
        /// 获取参数或默认值
        /// </summary>
        public T GetParamOrDefault<T>(ParamType paramType, T defaultValue = default)
        {
            try
            {
                return GetParam<T>(paramType);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 尝试获取参数值
        /// </summary>
        public bool TryGetParam<T>(ParamType paramType, out T value)
        {
            value = default;

            lock (_lock)
            {
                if (!_params.TryGetValue(paramType, out object objValue))
                    return false;

                try
                {
                    value = ConvertToType<T>(objValue, paramType);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 设置参数值
        /// </summary>
        public void SetParam<T>(ParamType paramType, T value)
        {
            object oldValue = null;
            bool exists = false;

            lock (_lock)
            {
                exists = _params.TryGetValue(paramType, out oldValue);
                _params[paramType] = value;
            }

            // 触发更改事件
            OnParamChanged(new ParamChangedEventArgs(paramType, oldValue, value, exists));
        }

        /// <summary>
        /// 类型转换辅助方法
        /// </summary>
        private T ConvertToType<T>(object value, ParamType paramType)
        {
            if (value == null)
                return default;

            if (value is T typedValue)
                return typedValue;

            try
            {
                // 尝试类型转换
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (InvalidCastException ex)
            {
                throw new InvalidCastException(
                    $"Cannot convert parameter {paramType} from type {value.GetType().Name} to {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// 索引器访问
        /// </summary>
        public object this[ParamType paramType]
        {
            get => GetParam<object>(paramType);
            set => SetParam(paramType, value);
        }

        /// <summary>
        /// 触发参数更改事件
        /// </summary>
        protected virtual void OnParamChanged(ParamChangedEventArgs e)
        {
            ParamChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public bool ResetToDefaults(ParamType paramType)
        {
            lock (_lock)
            {
                switch (paramType)
                {
                    case ParamType.System:
                        ((SecsGemSystemParam)_params[ParamType.System]).Reset();
                        break;

                    case ParamType.Formula:
                        string path = "";
                        return InitializationDefuaultSecsGemFormula(path);
                }

                return true;
            }
        }

        /// <summary>
        /// 保存参数
        /// </summary>
        public void SaveParam(ParamType paramType)
        {
            lock (_lock)
            {
                switch (paramType)
                {
                    case ParamType.System:
                        ((SecsGemSystemParam)_params[ParamType.System]).Save();
                        break;
                    case ParamType.Validate:
                        ((ValidateConfiguration)_params[ParamType.Validate]).Save();
                        break;
                    case ParamType.Formula:
                        string path = "";
                        SaveSecsGemFormula(path);
                        break;
                    default:
                        break;
                }
            }
        }

        private bool InitializationDefuaultSecsGemFormula(string path) { return true; }

        private bool SaveSecsGemFormula(string path) { return true; }

        /// <summary>
        /// 验证命令配置
        /// </summary>
        public async Task<bool> ValidateCommand()
        {
            var validate = ((ValidateConfiguration)_params[ParamType.Validate]);
            var formula = ((FormulaConfiguration)_params[ParamType.Formula]);

            if ((validate.CEIDS.Count + validate.CommandIDS.Count) > formula.IncentiveCommandDictionary.Count + formula.ResponseCommandDictionary.Count)
            {
                string errorMessage = $"验证失败: CEIDS数量({validate.CEIDS.Count})大于配方中的命令数量({formula.IncentiveCommandDictionary.Count + formula.ResponseCommandDictionary.Count})";

                var eventArgs = new FormulaValidateErrorEventArgs(formula, errorMessage);

                FormulaValidateError?.Invoke(this, eventArgs);

                if (eventArgs.IsHandled && eventArgs.NewFormula != null)
                {
                    _params[ParamType.Formula] = eventArgs.NewFormula;
                    return true;
                }

                SecsGemTrace.Warn(errorMessage);
                return false;
            }

            return true;
        }

        #region 私有方法

        // 三段加载此前全部 catch { return false; } 不留任何痕迹：SECS 机构初始化失败时现场只看到"初始化失败"，查不到原因。
        // 现统一记 Warn，并对按 Description/Name 建字典时的重名做容错（此前重名直接抛"已添加相同键"，整段配置加载失败）。

        private async Task<bool> LoadSystemConfiguration()
        {
            try
            {
                using var scope = _secsGemDataBase.BeginScope();
                var manger0 = scope.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                var systemParams = (await manger0.GetAllAsync()).Select(t => t.GetSecsGemSystemFormSecsGemSystemEntity()).ToList().FirstOrDefault();
                if (systemParams == null)
                    SecsGemTrace.Warn("SECS/GEM 系统参数表（SystemConfigs）为空，请先在 SECS 调试页导入系统参数");
                (_params[ParamType.System]) = systemParams;
                return true;
            }
            catch (Exception ex)
            {
                SecsGemTrace.Warn($"加载 SECS/GEM 系统参数失败：{ex.Message}");
                return false;
            }
        }

        private async Task<bool> LoadValidateConfiguration()
        {
            try
            {
                using var scope = _secsGemDataBase.BeginScope();
                var manger1 = scope.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs);
                var commandids = ToConcurrentDictionary((await manger1.GetAllAsync()).Select(t => t.GetCommandIDFormCommandIDEntity()), i => i.Description, "CommandID");

                var manger2 = scope.GetRepository<CEIDEntity>(SecsDbSet.CEIDs);
                var ceids = ToConcurrentDictionary((await manger2.GetAllAsync()).Select(t => t.GetCEIDFormCEIDEntity()), i => i.Description, "CEID");

                var manger3 = scope.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs);
                var reoprtids = ToConcurrentDictionary((await manger3.GetAllAsync()).Select(t => t.GetReportIDFormReportIDEntity()), i => i.Description, "ReportID");

                var manger4 = scope.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                var vids = ToConcurrentDictionary((await manger4.GetAllAsync()).Select(t => t.GetVIDFormVIDEntity()), i => i.Description, "VID");

                ((ValidateConfiguration)_params[ParamType.Validate]).CommandIDS = commandids;
                ((ValidateConfiguration)_params[ParamType.Validate]).CEIDS = ceids;
                ((ValidateConfiguration)_params[ParamType.Validate]).ReportIDS = reoprtids;
                ((ValidateConfiguration)_params[ParamType.Validate]).VIDS = vids;

                return true;
            }
            catch (Exception ex)
            {
                SecsGemTrace.Warn($"加载 SECS/GEM 变量配置（VID/CEID/ReportID/CommandID）失败：{ex.Message}");
                return false;
            }
        }

        private async Task<bool> LoadFormulaConfiguration()
        {
            try
            {
                // 此前内层 catch {} 吞掉读库异常后照常以空命令集返回 true：SECS "初始化成功"却一条命令都没有，
                // Host 的请求全部无人应答。读库失败必须如实返回 false
                using var scope = _secsGemDataBase.BeginScope();
                var manger1 = scope.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                var incentiveCommands = ToConcurrentDictionary((await manger1.GetAllAsync()).Select(t => t.GetSFCommandFormIncentiveEntity()), i => i.Name, "主动命令");

                var manger2 = scope.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                var responseCommands = ToConcurrentDictionary((await manger2.GetAllAsync()).Select(t => t.GetSFCommandFormResponseEntity()), i => i.Name, "应答命令");

                ((FormulaConfiguration)_params[ParamType.Formula]).IncentiveCommandDictionary = incentiveCommands;
                ((FormulaConfiguration)_params[ParamType.Formula]).ResponseCommandDictionary = responseCommands;
                return true;
            }
            catch (Exception ex)
            {
                SecsGemTrace.Warn($"加载 SECS/GEM 配方命令失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>按键建字典；重名时保留第一条并记 Warn，不让整段配置因一条重名加载失败</summary>
        private static ConcurrentDictionary<string, T> ToConcurrentDictionary<T>(IEnumerable<T> items, Func<T, string> keySelector, string kind)
        {
            var dict = new ConcurrentDictionary<string, T>();
            foreach (var item in items)
            {
                var key = keySelector(item) ?? string.Empty;
                if (!dict.TryAdd(key, item))
                    SecsGemTrace.Warn($"SECS/GEM {kind} 名称重复「{key}」，已忽略后一条，请在 SECS 调试页检查配置");
            }
            return dict;
        }
        #endregion
    }
}
