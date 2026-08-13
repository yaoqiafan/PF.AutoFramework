using MvCameraControl;
using PF.Infrastructure.Logging;
using System.Globalization;

namespace PF.Infrastructure.Hardware.Vision.Hikvision
{
    /// <summary>
    /// GenICam 节点读写通用实现 —— 相机（IDevice.Parameters）与采集卡（IInterface.Parameters）共用。
    ///
    /// <para>不依赖 GetNodeInterfaceType 判定节点类型，而是**按类型依次尝试**
    /// （枚举 → 整数 → 浮点 → 布尔 → 字符串），哪个成功用哪个。
    /// 这样做的原因：节点类型枚举在不同 SDK 版本间的命名并不稳定，
    /// 而"试一遍"对所有版本都成立，且配置下发不是高频路径，多几次失败调用无实际代价。</para>
    ///
    /// <para>所有方法都不抛异常：节点不存在、不可写、值非法一律返回 false / null 并记 Debug 日志，
    /// 由调用方决定是跳过还是升级为报警——换相机型号时，个别节点缺失是常态，不该让初始化整体失败。</para>
    /// </summary>
    internal sealed class GenICamNodeAccessor
    {
        private readonly Func<IParameters?> _parameters;
        private readonly CategoryLogger _logger;
        private readonly string _owner;

        /// <summary>
        /// 构造节点访问器。
        /// </summary>
        /// <param name="parametersProvider">
        /// 参数树提供者。传委托而不是实例，是因为设备重连后 IDevice/IInterface 会被重建，
        /// 持有实例会拿到已失效的旧句柄。
        /// </param>
        /// <param name="logger">硬件分类日志。</param>
        /// <param name="owner">日志前缀（设备名）。</param>
        public GenICamNodeAccessor(Func<IParameters?> parametersProvider, CategoryLogger logger, string owner)
        {
            _parameters = parametersProvider;
            _logger = logger;
            _owner = owner;
        }

        /// <summary>探测节点是否存在且当前可访问。</summary>
        public bool IsNodeAvailable(string nodeName)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return false;

            try
            {
                if (p.GetNodeAccessMode(nodeName, out XmlAccessMode mode) != MvError.MV_OK)
                    return false;

                return mode is XmlAccessMode.RO or XmlAccessMode.RW or XmlAccessMode.WO;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 探测节点 '{nodeName}' 异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>探测节点当前是否可写。节点不存在或只读均返回 false。</summary>
        public bool IsNodeWritable(string nodeName)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return false;

            try
            {
                if (p.GetNodeAccessMode(nodeName, out XmlAccessMode mode) != MvError.MV_OK)
                    return false;

                return mode is XmlAccessMode.RW or XmlAccessMode.WO;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 探测节点 '{nodeName}' 可写性异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>读取节点当前值（枚举返回 symbolic 名）。不可读返回 null。</summary>
        public string? GetNode(string nodeName)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return null;

            try
            {
                if (p.GetEnumValue(nodeName, out IEnumValue enumValue) == MvError.MV_OK)
                    return enumValue.CurEnumEntry.Symbolic;

                if (p.GetIntValue(nodeName, out IIntValue intValue) == MvError.MV_OK)
                    return intValue.CurValue.ToString(CultureInfo.InvariantCulture);

                if (p.GetFloatValue(nodeName, out IFloatValue floatValue) == MvError.MV_OK)
                    return floatValue.CurValue.ToString(CultureInfo.InvariantCulture);

                if (p.GetBoolValue(nodeName, out bool boolValue) == MvError.MV_OK)
                    return boolValue ? "true" : "false";

                if (p.GetStringValue(nodeName, out IStringValue stringValue) == MvError.MV_OK)
                    return stringValue.CurValue;

                return null;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 读取节点 '{nodeName}' 异常：{ex.Message}", ex);
                return null;
            }
        }

        /// <summary>写入节点值。成功返回 true。</summary>
        public bool SetNode(string nodeName, string value)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return false;

            try
            {
                // 枚举优先：symbolic 名是跨型号/固件最稳定的契约
                if (p.SetEnumValueByString(nodeName, value) == MvError.MV_OK)
                    return true;

                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)
                    && p.SetIntValue(nodeName, l) == MvError.MV_OK)
                    return true;

                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                    && p.SetFloatValue(nodeName, f) == MvError.MV_OK)
                    return true;

                if (bool.TryParse(value, out bool b)
                    && p.SetBoolValue(nodeName, b) == MvError.MV_OK)
                    return true;

                if (p.SetStringValue(nodeName, value) == MvError.MV_OK)
                    return true;

                // 失败原因回读一次访问模式再报：SDK 对"节点不存在"和"此刻不可写"给的是两个
                // 不同错误码（MV_E_GC_NODE_NOT_FOUND / MV_E_GC_ACCESS），但都表现为写入返回非 OK。
                // 只说"失败"会让人一路去查节点名有没有写错，而真正的原因往往是时序——
                // 例如采集卡的流参数在相机 Open 之后就变成只读了。
                _logger.Debug($"[{_owner}] 写入节点 '{nodeName}' = '{value}' 失败（{DescribeAccess(p, nodeName)}）。");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 写入节点 '{nodeName}' 异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 描述节点当前的访问状态，用于把"写失败"的原因讲清楚：
        /// 是节点根本不存在，还是存在但此刻只读/不可访问。
        /// </summary>
        private static string DescribeAccess(IParameters p, string nodeName)
        {
            try
            {
                if (p.GetNodeAccessMode(nodeName, out XmlAccessMode mode) != MvError.MV_OK)
                    return "本设备不存在该节点";

                return mode switch
                {
                    XmlAccessMode.RO => "节点当前为只读（多为时序问题：某些参数只在特定状态下可写）",
                    XmlAccessMode.NA => "节点当前不可访问",
                    XmlAccessMode.RW or XmlAccessMode.WO => "节点可写，但值非法或超出范围",
                    _ => $"节点访问模式={mode}",
                };
            }
            catch
            {
                return "无法读取节点访问模式";
            }
        }

        /// <summary>读取枚举节点的全部可选项（symbolic 名）。非枚举或失败返回空列表。</summary>
        public IReadOnlyList<string> GetEnumEntries(string nodeName)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return Array.Empty<string>();

            try
            {
                if (p.GetEnumValue(nodeName, out IEnumValue enumValue) != MvError.MV_OK)
                    return Array.Empty<string>();

                var result = new List<string>((int)enumValue.SupportedNum);
                for (int i = 0; i < enumValue.SupportedNum && i < enumValue.SupportEnumEntries.Length; i++)
                    result.Add(enumValue.SupportEnumEntries[i].Symbolic);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 读取枚举节点 '{nodeName}' 选项异常：{ex.Message}", ex);
                return Array.Empty<string>();
            }
        }

        /// <summary>执行命令节点。</summary>
        public bool ExecuteCommand(string nodeName)
        {
            var p = _parameters();
            if (p == null || string.IsNullOrWhiteSpace(nodeName)) return false;

            try
            {
                int ret = p.SetCommandValue(nodeName);
                if (ret == MvError.MV_OK) return true;

                _logger.Debug($"[{_owner}] 执行命令节点 '{nodeName}' 失败，错误码=0x{ret:X8}。");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[{_owner}] 执行命令节点 '{nodeName}' 异常：{ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 批量下发附加节点表。逐条尝试，失败只记警告不中断，返回成功条数。
        /// </summary>
        public int ApplyExtraNodes(IReadOnlyDictionary<string, string>? nodes)
        {
            if (nodes == null || nodes.Count == 0) return 0;

            int ok = 0;
            foreach (var kv in nodes)
            {
                if (SetNode(kv.Key, kv.Value)) ok++;
                else _logger.Warn($"[{_owner}] 附加节点 '{kv.Key}' = '{kv.Value}' 下发失败，已跳过。");
            }

            return ok;
        }

        /// <summary>
        /// 条件下发：值为 null/空时跳过（语义为"不下发、沿用设备当前值"）。
        /// </summary>
        public void SetIfPresent(string nodeName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (!SetNode(nodeName, value!))
                _logger.Warn($"[{_owner}] 节点 '{nodeName}' = '{value}' 下发失败，已跳过。");
        }
    }
}
