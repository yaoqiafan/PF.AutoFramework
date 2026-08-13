using PF.Core.Entities.Hardware.Vision;
using PF.Infrastructure.Hardware.Vision.Hikvision;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Hardware.Camera.LineScan.Hikvision
{
    /// <summary>
    /// 相机直连的帧控制策略（GigE / USB，无采集卡）。
    ///
    /// <para>没有采集卡时，帧长回到相机自身的 Height 节点，帧触发回到相机的帧触发节点。
    /// 相机侧没有与采集卡 FrameTimeoutTime 对应的节点，"攒不满行"只能靠
    /// WaitFrameAsync 的等待超时兜底，本策略对该字段不下发。</para>
    ///
    /// <para><b>新旧固件双分支</b>：新节点树用 FrameTriggerMode / FrameTriggerSource；
    /// 老节点树没有 FrameTriggerControl，需退回 TriggerSelector=FrameBurstStart +
    /// TriggerMode + TriggerSource。用 GetNodeAccessMode 探测节点是否存在来选分支，
    /// 与官方示例 ParameterCamera_LineScanIOSettings 的做法一致。</para>
    /// </summary>
    internal sealed class DeviceFrameControl : IFrameControl
    {
        /// <summary>新节点树的探针节点：存在即说明相机支持独立的帧触发控制组。</summary>
        private const string FrameTriggerControlNode = "FrameTriggerControl";

        private readonly GenICamNodeAccessor _accessor;
        private readonly CategoryLogger _logger;
        private readonly string _owner;

        /// <summary>本次生效的软触发命令节点名（随分支不同而不同）。</summary>
        private string _softTriggerCommand = "FrameTriggerSoftware";

        /// <summary>构造相机直连帧控制策略。</summary>
        public DeviceFrameControl(GenICamNodeAccessor accessor, CategoryLogger logger, string owner)
        {
            _accessor = accessor;
            _logger = logger;
            _owner = owner;
        }

        /// <inheritdoc/>
        public string Description => "相机自身(直连)";

        /// <inheritdoc/>
        public Task ApplyAsync(FrameControlConfig config, CancellationToken token)
        {
            if (config == null) return Task.CompletedTask;

            return Task.Run(() =>
            {
                // 帧长：线扫相机的 Height 即"一帧累计多少行"
                if (config.ImageHeight > 0)
                    _accessor.SetIfPresent("Height", config.ImageHeight.ToString());

                if (config.FrameTimeoutMs > 0)
                {
                    _logger.Debug($"[{_owner}] 相机直连模式无帧超时节点，"
                        + $"帧超时({config.FrameTimeoutMs}ms)仅由取帧等待超时兜底。");
                }

                if (_accessor.IsNodeAvailable(FrameTriggerControlNode))
                    ApplyModernFrameTrigger(config);
                else
                    ApplyLegacyFrameTrigger(config);

                _accessor.ApplyExtraNodes(config.ExtraNodes);
            }, token);
        }

        /// <inheritdoc/>
        public Task<bool> SoftwareTriggerFrameAsync(CancellationToken token)
            => Task.Run(() => _accessor.ExecuteCommand(_softTriggerCommand), token);

        /// <summary>新节点树：FrameTriggerMode / FrameTriggerSource。</summary>
        private void ApplyModernFrameTrigger(FrameControlConfig config)
        {
            _softTriggerCommand = "FrameTriggerSoftware";

            if (!_accessor.SetNode("FrameTriggerMode", config.TriggerEnable ? "true" : "false"))
                _logger.Warn($"[{_owner}] 设置 FrameTriggerMode 失败。");

            if (config.TriggerEnable)
                _accessor.SetIfPresent("FrameTriggerSource", config.TriggerSource);

            _logger.Info($"[{_owner}] 帧触发已按新节点树配置："
                + $"{(config.TriggerEnable ? config.TriggerSource : "关闭(连续)")}。");
        }

        /// <summary>
        /// 老节点树：TriggerSelector=FrameBurstStart + TriggerMode + TriggerSource。
        /// </summary>
        private void ApplyLegacyFrameTrigger(FrameControlConfig config)
        {
            _softTriggerCommand = "TriggerSoftware";

            if (!_accessor.SetNode("TriggerSelector", "FrameBurstStart"))
            {
                _logger.Warn($"[{_owner}] 相机既无 {FrameTriggerControlNode} 节点，"
                    + "也不支持 TriggerSelector=FrameBurstStart，帧触发配置已跳过。");
                return;
            }

            _accessor.SetNode("TriggerMode", config.TriggerEnable ? "On" : "Off");

            if (config.TriggerEnable)
                _accessor.SetIfPresent("TriggerSource", config.TriggerSource);

            _logger.Info($"[{_owner}] 帧触发已按老节点树(FrameBurstStart)配置："
                + $"{(config.TriggerEnable ? config.TriggerSource : "关闭(连续)")}。");
        }
    }
}
