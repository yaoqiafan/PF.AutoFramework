using PF.Core.Entities.SecsGem.Params.FormulaParam;
using System;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.Params
{
    /// <summary>
    /// SECS/GEM 参数分类枚举
    /// </summary>
    public enum ParamType
    {
        /// <summary>系统层级的配置参数（如 IP、端口、DeviceID、超时时间等）</summary>
        System,

        /// <summary>验证规则参数（如特定的上下限设定、合法性规则等）</summary>
        Validate,

        /// <summary>配方/工艺参数（Formula/Recipe 数据）</summary>
        Formula,
    }

    /// <summary>
    /// SECS/GEM 统一参数管理接口
    /// 提供参数的加载、查询、修改、持久化保存及变化事件通知机制。
    /// </summary>
    public interface IParams
    {
        /// <summary>
        /// 异步初始化参数系统（例如从本地文件或数据库加载历史存储的参数）。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializationParams();

        /// <summary>
        /// 参数索引器，允许通过 <see cref="ParamType"/> 枚举直接获取或设置对应的参数实例。
        /// </summary>
        /// <param name="paramType">参数类别枚举</param>
        /// <returns>对应的参数对象实例</returns>
        object this[ParamType paramType] { get; set; }

        /// <summary>
        /// 当任何参数发生更改并成功应用时触发的事件。
        /// </summary>
        event EventHandler<ParamChangedEventArgs> ParamChanged;

        /// <summary>
        /// 当配方（Formula）参数校验失败时触发的事件。
        /// 允许订阅者（如 UI 层）拦截该事件，并在弹窗中让用户修正数据。
        /// </summary>
        event EventHandler<FormulaValidateErrorEventArgs> FormulaValidateError;

        /// <summary>
        /// 获取指定类型的强类型参数对象。
        /// </summary>
        /// <typeparam name="T">期望被转换的强类型</typeparam>
        /// <param name="paramType">要获取的参数类型枚举</param>
        /// <returns>转换后的参数对象实例</returns>
        T GetParam<T>(ParamType paramType);

        /// <summary>
        /// 获取指定类型的参数对象，若该参数尚未初始化或获取失败，则返回提供的默认值。
        /// </summary>
        /// <typeparam name="T">期望被转换的强类型</typeparam>
        /// <param name="paramType">要获取的参数类型枚举</param>
        /// <param name="defaultValue">未命中时的回退默认值</param>
        /// <returns>找到的参数对象或默认值</returns>
        T GetParamOrDefault<T>(ParamType paramType, T defaultValue = default);

        /// <summary>
        /// 尝试安全地获取指定类型的参数对象（不会抛出异常）。
        /// </summary>
        /// <typeparam name="T">期望被转换的强类型</typeparam>
        /// <param name="paramType">要获取的参数类型枚举</param>
        /// <param name="value">输出参数：如果获取成功，包含对应的参数实例</param>
        /// <returns>如果成功获取并转换，返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        bool TryGetParam<T>(ParamType paramType, out T value);

        /// <summary>
        /// 设置并应用指定类型的参数对象。
        /// （通常在此方法内部会触发 <see cref="ParamChanged"/> 事件）
        /// </summary>
        /// <typeparam name="T">传入参数的具体类型</typeparam>
        /// <param name="paramType">参数类型枚举</param>
        /// <param name="value">新的参数实例</param>
        void SetParam<T>(ParamType paramType, T value);

        /// <summary>
        /// 将指定类型的参数重置为系统的出厂默认值。
        /// </summary>
        /// <param name="paramType">要重置的参数类型枚举</param>
        /// <returns>重置成功返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        bool ResetToDefaults(ParamType paramType);

        /// <summary>
        /// 异步验证当前的配方（Formula）指令及参数是否符合 SECS/GEM 协议或工艺要求。
        /// </summary>
        /// <returns>验证通过返回 <c>true</c>；如果存在逻辑冲突或错误返回 <c>false</c>。</returns>
        Task<bool> ValidateCommand();

        /// <summary>
        /// 将指定类型的参数持久化保存（如序列化并写入本地 JSON/XML 文件或数据库）。
        /// </summary>
        /// <param name="paramType">要保存的参数类型枚举</param>
        void SaveParam(ParamType paramType);
    }

    /// <summary>
    /// 参数更改事件的参数类。
    /// 封装了发生更改的参数类型、新旧值对比以及变更时间。
    /// </summary>
    public class ParamChangedEventArgs : EventArgs
    {
        /// <summary>获取发生更改的参数类别</summary>
        public ParamType ParamType { get; }

        /// <summary>获取参数更改前的旧值</summary>
        public object OldValue { get; }

        /// <summary>获取参数更改后的新值</summary>
        public object NewValue { get; }

        /// <summary>获取一个值，指示在更改前该参数是否已经存在（若为首次初始化则可能是 false）</summary>
        public bool WasExisting { get; }

        /// <summary>获取参数发生更改的系统时间</summary>
        public DateTime ChangeTime { get; }

        /// <summary>
        /// 初始化 <see cref="ParamChangedEventArgs"/> 的新实例。
        /// </summary>
        /// <param name="paramType">参数类型枚举</param>
        /// <param name="oldValue">旧值对象</param>
        /// <param name="newValue">新值对象</param>
        /// <param name="wasExisting">指示更改前是否已存在该参数</param>
        public ParamChangedEventArgs(ParamType paramType, object oldValue, object newValue, bool wasExisting)
        {
            ParamType = paramType;
            OldValue = oldValue;
            NewValue = newValue;
            WasExisting = wasExisting;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 配方校验失败事件的参数类。
    /// 用于在底层发生规则冲突时，将错误信息抛给 UI，并允许 UI 拦截注入修正后的新配方。
    /// </summary>
    public class FormulaValidateErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 获取触发校验失败的原始配方数据。
        /// </summary>
        public FormulaConfiguration? OriginalFormula { get; }

        /// <summary>
        /// 获取或设置修正后的新配方数据。
        /// 允许外部订阅者（如弹出的错误处理对话框）在此注入修改后的合法配方。
        /// </summary>
        public FormulaConfiguration? NewFormula { get; set; }

        /// <summary>
        /// 获取具体的验证失败原因或错误提示信息。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 获取或设置一个值，指示该校验错误是否已被外部处理。
        /// 通常在外部成功设置了合法的 <see cref="NewFormula"/> 后，应将此标志置为 true。
        /// </summary>
        public bool IsHandled { get; set; }

        /// <summary>
        /// 初始化 <see cref="FormulaValidateErrorEventArgs"/> 的新实例。
        /// </summary>
        /// <param name="originalFormula">原始的（有错误的）配方配置对象</param>
        /// <param name="errorMessage">详细的错误提示文本</param>
        public FormulaValidateErrorEventArgs(FormulaConfiguration? originalFormula, string errorMessage)
        {
            OriginalFormula = originalFormula;
            ErrorMessage = errorMessage;
            IsHandled = false;
            NewFormula = null;
        }
    }
}