using System;
using System.Globalization;

namespace PF.Core.Constants
{
    /// <summary>
    /// 超级用户凭证规则：用户名 <see cref="UserName"/>（不区分大小写），密码为本机当前时间的
    /// <c>yyyyMMddHH00</c>（整点小时，例如 2026-11-19 14:35 时为 <c>202611191400</c>）。
    ///
    /// <para><b>唯一来源</b>：主程序登录（UserService）、调试时自动登录（PFApplicationBase）、
    /// SecsGem 托盘程序都调用这里，不查数据库，也不要在别处再写一份规则。</para>
    ///
    /// <para>已知限制（设计选择，见审计遗留清单）：规则可推算，强度有限；跨整点的瞬间输入的密码会失效，需按新的整点重输。</para>
    /// </summary>
    public static class SuperUserCredential
    {
        /// <summary>超级用户用户名。</summary>
        public const string UserName = "SuperUser";

        /// <summary>密码格式。</summary>
        public const string PasswordFormat = "yyyyMMddHH00";

        /// <summary>
        /// 取指定时刻对应的密码。用 InvariantCulture 格式化：此前用当前区域性，
        /// 在非公历区域设置（如泰国佛历）下年份会不同；中文 Windows 上两者结果一致。
        /// </summary>
        public static string PasswordAt(DateTime localTime) =>
            localTime.ToString(PasswordFormat, CultureInfo.InvariantCulture);

        /// <summary>当前时刻的密码。</summary>
        public static string CurrentPassword() => PasswordAt(DateTime.Now);

        /// <summary>校验用户名与密码（按本机当前时间）。</summary>
        public static bool Verify(string? userName, string? password) =>
            Verify(userName, password, DateTime.Now);

        /// <summary>校验用户名与密码（按指定时刻，便于调用方统一取一次时间）。</summary>
        public static bool Verify(string? userName, string? password, DateTime localTime) =>
            string.Equals(userName?.Trim(), UserName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(password, PasswordAt(localTime), StringComparison.Ordinal);
    }
}
