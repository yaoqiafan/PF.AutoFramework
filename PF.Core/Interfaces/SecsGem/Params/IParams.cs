using PF.Core.Entities.SecsGem.Params.FormulaParam;

namespace PF.Core.Interfaces.SecsGem.Params
{
    public enum ParamType
    {
        System,
        Validate,
        Formula,
    }
    /// <summary>
    /// 参数接口
    /// </summary>
    public interface IParams
    {

        Task<bool> InitializationParams();

        /// <summary>
        /// 参数索引器
        /// </summary>
        object this[ParamType paramType] { get; set; }

        /// <summary>
        /// 参数更改事件
        /// </summary>
        event EventHandler<ParamChangedEventArgs> ParamChanged;

        /// <summary>
        /// 配方验证失败事件
        /// </summary>
        event EventHandler<FormulaValidateErrorEventArgs> FormulaValidateError;
        /// <summary>
        /// 获取参数值
        /// </summary>
        T GetParam<T>(ParamType paramType);

        /// <summary>
        /// 获取参数或默认值
        /// </summary>
        T GetParamOrDefault<T>(ParamType paramType, T defaultValue = default);

        /// <summary>
        /// 尝试获取参数值
        /// </summary>
        bool TryGetParam<T>(ParamType paramType, out T value);

        /// <summary>
        /// 设置参数值
        /// </summary>
        void SetParam<T>(ParamType paramType, T value);

        /// <summary>
        /// 重置为默认值
        /// </summary>
        bool ResetToDefaults(ParamType paramType);

        /// <summary>
        /// 验证配方参数
        /// </summary>
       Task<bool>  ValidateCommand();

        /// <summary>
        /// 重置为默认值
        /// </summary>
        void SaveParam(ParamType paramType);
    }

    /// <summary>
    /// 参数更改事件参数
    /// </summary>
    public class ParamChangedEventArgs : EventArgs
    {
        public ParamType ParamType { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public bool WasExisting { get; }
        public DateTime ChangeTime { get; }

        public ParamChangedEventArgs(ParamType paramType, object oldValue, object newValue, bool wasExisting)
        {
            ParamType = paramType;
            OldValue = oldValue;
            NewValue = newValue;
            WasExisting = wasExisting;
            ChangeTime = DateTime.Now;
        }
    }




    public class FormulaValidateErrorEventArgs : EventArgs
    {
        // 原始配方数据
        public FormulaConfiguration? OriginalFormula { get; }

        // 新配方数据（外部可以设置）
        public FormulaConfiguration? NewFormula { get; set; }

        // 验证失败的具体信息
        public string ErrorMessage { get; }

        // 是否已处理（外部设置新配方后设为true）
        public bool IsHandled { get; set; }

        public FormulaValidateErrorEventArgs(FormulaConfiguration? originalFormula, string errorMessage)
        {
            OriginalFormula = originalFormula;
            ErrorMessage = errorMessage;
            IsHandled = false;
            NewFormula = null;
        }
    }
}
