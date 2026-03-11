using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Entities.Command;
using PF.Infrastructure.SecsGem.Entities.System;
using PF.Infrastructure.SecsGem.Entities.Variable;
using System.Collections.Concurrent;

namespace PF.Infrastructure.SecsGem.Param
{
    public class ParamsManger : IParams
    {
        private readonly ISecsGemDataBase _secsGemDataBase;
        private readonly ICommandManager _commandManager;
        private readonly Dictionary<ParamType, object> _params;
        private readonly object _lock = new object();

        // 参数更改事件
        public event EventHandler<ParamChangedEventArgs> ParamChanged;

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

        public async Task<bool> InitializationParams()
        {
            try
            {
                bool res1, res2, res3;

                res1 = await LoadSystemConfiguration();
                res2 = await LoadValidateConfiguration();
                res3 = await LoadFormulaConfiguration();

                if (res1 & res2 & res3)
                {
                    return await _commandManager.InitializeAsync((FormulaConfiguration)_params[ParamType.Formula]);
                }
                return false;
            }
            catch (Exception)
            {
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

                Console.WriteLine(errorMessage);
                return false;
            }

            return true;
        }

        #region 私有方法

        private async Task<bool> LoadSystemConfiguration()
        {
            try
            {
                var manger0 = _secsGemDataBase.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                var systemParams = (await manger0.GetAllAsync()).Select(t => t.GetSecsGemSystemFormSecsGemSystemEntity()).ToList().FirstOrDefault();
                (_params[ParamType.System]) = systemParams;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> LoadValidateConfiguration()
        {
            try
            {
                var manger1 = _secsGemDataBase.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs);
                var commandids = new ConcurrentDictionary<string, CommandID>((await manger1.GetAllAsync()).Select(t => t.GetCommandIDFormCommandIDEntity()).ToList().Select(item => new KeyValuePair<string, CommandID>(item.Description, item)));

                var manger2 = _secsGemDataBase.GetRepository<CEIDEntity>(SecsDbSet.CEIDs);
                var ceids = new ConcurrentDictionary<string, CEID>((await manger2.GetAllAsync()).Select(t => t.GetCEIDFormCEIDEntity()).ToList().Select(item => new KeyValuePair<string, CEID>(item.Description, item)));

                var manger3 = _secsGemDataBase.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs);
                var reoprtids = new ConcurrentDictionary<string, ReportID>((await manger3.GetAllAsync()).Select(t => t.GetReportIDFormReportIDEntity()).ToList().Select(item => new KeyValuePair<string, ReportID>(item.Description, item)));

                var manger4 = _secsGemDataBase.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                var vids = new ConcurrentDictionary<string, VID>((await manger4.GetAllAsync()).Select(t => t.GetVIDFormVIDEntity()).ToList().Select(item => new KeyValuePair<string, VID>(item.Description, item)));

                ((ValidateConfiguration)_params[ParamType.Validate]).CommandIDS = commandids;
                ((ValidateConfiguration)_params[ParamType.Validate]).CEIDS = ceids;
                ((ValidateConfiguration)_params[ParamType.Validate]).ReportIDS = reoprtids;
                ((ValidateConfiguration)_params[ParamType.Validate]).VIDS = vids;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> LoadFormulaConfiguration()
        {
            try
            {
                var manger1 = _secsGemDataBase.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                var incentiveCommands = new ConcurrentDictionary<string, SFCommand>((await manger1.GetAllAsync()).Select(t => t.GetSFCommandFormIncentiveEntity()).ToList().Select(item => new KeyValuePair<string, SFCommand>(item.Name, item)));

                var manger2 = _secsGemDataBase.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                var responseCommands = new ConcurrentDictionary<string, SFCommand>((await manger2.GetAllAsync()).Select(t => t.GetSFCommandFormResponseEntity()).ToList().Select(item => new KeyValuePair<string, SFCommand>(item.Name, item)));

                ((FormulaConfiguration)_params[ParamType.Formula]).IncentiveCommandDictionary = incentiveCommands;
                ((FormulaConfiguration)_params[ParamType.Formula]).ResponseCommandDictionary = responseCommands;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion
    }
}
