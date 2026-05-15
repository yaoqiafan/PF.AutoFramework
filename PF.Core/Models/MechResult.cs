using PF.Core.Attributes;
using PF.Core.Enums;

namespace PF.Core.Models
{
    /// <summary>
    /// 模组操作结果基类，携带成功状态、错误代码和错误消息。
    /// 所有模组的公共业务方法统一返回此类型（或泛型派生类），替代原有的 bool/Exception 混合模式。
    /// </summary>
    public class MechResult
    {
        /// <summary>操作是否成功</summary>
        public bool IsSuccess { get; set; }

        /// <summary>失败时的错误代码（对应 AlarmCodesExtensions 中的常量）</summary>
        public string ErrorCode { get; set; }

        /// <summary>失败时的错误描述</summary>
        public string ErrorMessage { get; set; }

        /// <summary>创建无数据的成功结果</summary>
        public static MechResult Success() => new() { IsSuccess = true };

        /// <summary>创建携带数据的成功结果</summary>
        public static MechResult<T> Success<T>(T data) => new() { IsSuccess = true, Data = data };

        /// <summary>创建失败结果</summary>
        public static MechResult Fail(string errorCode, string message) => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message
        };

        /// <summary>隐式布尔转换（方便 if 判断）</summary>
        public static implicit operator bool(MechResult result) => result.IsSuccess;
    }

    /// <summary>
    /// 携带返回数据的泛型模组操作结果
    /// </summary>
    public class MechResult<T> : MechResult
    {
        /// <summary>操作成功时的返回数据</summary>
        public T Data { get; set; }

        /// <summary>创建携带数据的成功结果</summary>
        public static MechResult<T> Success(T data) => new() { IsSuccess = true, Data = data };

        /// <summary>创建失败结果</summary>
        public new static MechResult<T> Fail(string errorCode, string message) => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            Data = default
        };
    }
}
