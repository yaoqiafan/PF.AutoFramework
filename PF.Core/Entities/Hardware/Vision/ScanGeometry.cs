using PF.Core.Entities.Hardware;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 一次扫描的几何量——由起点/终点两个轴点位算出，不持有任何可独立编辑的运动参数，
    /// 也不涉及成像（帧长/行频/曝光）——那些已经配置在相机自己身上（UserSetDefault），
    /// 本模组不再重新计算、不再下发覆盖。
    ///
    /// <para><b>为什么速度/加减速/S曲线时间不是本类自己的字段</b>：它们全部来自"终点"这个点位
    /// 自身的配置（<see cref="AxisPoint.Speed"/>/<see cref="AxisPoint.Acc"/>/
    /// <see cref="AxisPoint.Dec"/>/<see cref="AxisPoint.STime"/>）——这几个量本来就该在轴的
    /// 点位表里维护一份，机台上其它轴全都这么用，线扫轴没有理由另起一套。这样带来的另一个好处是
    /// 单位天然和轴保持一致：<see cref="AxisPoint.STime"/> 的单位是秒，这里原样透传，不会像过去
    /// 那样在"标了 ms 实际按秒下发给轴"上出错。</para>
    ///
    /// <para><b>坐标约定</b>：终点小于起点即为反向扫描，各处按符号自动处理。</para>
    /// </summary>
    public sealed class ScanGeometry
    {
        /// <summary>帧超时相对理论帧时间的倍数，供拍摄一帧的等待兜底，不是精确值，够用即可。</summary>
        private const double FrameTimeoutRatio = 3.0;

        /// <summary>起点点位名称。</summary>
        public string StartPointName { get; }

        /// <summary>终点点位名称。</summary>
        public string EndPointName { get; }

        /// <summary>起点位置（工程单位，即轴点位表里的单位）。</summary>
        public double StartMm { get; }

        /// <summary>终点位置（工程单位，即轴点位表里的单位）。</summary>
        public double EndMm { get; }

        /// <summary>扫描速度 = 终点点位自身配置的速度。</summary>
        public double VelocityMmPerSec { get; }

        /// <summary>加速度 = 终点点位自身配置的加速度。</summary>
        public double AccelerationMmPerSec2 { get; }

        /// <summary>减速度 = 终点点位自身配置的减速度。</summary>
        public double DecelerationMmPerSec2 { get; }

        /// <summary>S 曲线时间（秒）= 终点点位自身配置的值，与 <see cref="AxisPoint.STime"/> 同单位。</summary>
        public double SCurveTimeSec { get; }

        /// <summary>由起点/终点两个轴点位构造。</summary>
        public ScanGeometry(AxisPoint start, AxisPoint end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            StartPointName = start.Name;
            EndPointName = end.Name;
            StartMm = start.TargetPosition;
            EndMm = end.TargetPosition;
            VelocityMmPerSec = end.Speed;
            AccelerationMmPerSec2 = end.Acc;
            DecelerationMmPerSec2 = end.Dec;
            SCurveTimeSec = end.STime;
        }

        /// <summary>扫描方向：终点大于等于起点为 +1，否则为 -1。</summary>
        public int Direction => EndMm >= StartMm ? 1 : -1;

        /// <summary>扫描行程（工程单位，恒为正）。</summary>
        public double ScanLengthMm => Math.Abs(EndMm - StartMm);

        /// <summary>实际减速度（终点未单独配置减速度时取加速度）。</summary>
        public double EffectiveDeceleration => DecelerationMmPerSec2 > 0 ? DecelerationMmPerSec2 : AccelerationMmPerSec2;

        /// <summary>
        /// 理论加速距离 = v²/(2a)，仅供参考——不是硬性校验（余量已经交给起点/终点两个点位
        /// 自己的位置去把握），但仍然是判断"这段行程够不够轴提上速"的有用信息，在调试页展示。
        /// </summary>
        public double TheoreticalAccelDistanceMm => AccelerationMmPerSec2 > 0
            ? VelocityMmPerSec * VelocityMmPerSec / (2 * AccelerationMmPerSec2)
            : 0;

        /// <summary>理论减速距离，含义同 <see cref="TheoreticalAccelDistanceMm"/>。</summary>
        public double TheoreticalDecelDistanceMm => EffectiveDeceleration > 0
            ? VelocityMmPerSec * VelocityMmPerSec / (2 * EffectiveDeceleration)
            : 0;

        /// <summary>理论帧时间（ms）= 扫描行程 ÷ 扫描速度，只用来估一个等帧超时，不追求精确。</summary>
        public int EstimatedFrameTimeMs => VelocityMmPerSec > 0
            ? (int)Math.Ceiling(ScanLengthMm / VelocityMmPerSec * 1000.0)
            : 0;

        /// <summary>等一帧完整图像的超时（ms）：理论帧时间 × 余量倍数，至少 1000ms。</summary>
        public int FrameTimeoutMs => Math.Max(1000, (int)(EstimatedFrameTimeMs * FrameTimeoutRatio));

        /// <summary>
        /// 校验起点/终点自洽性，返回问题清单（空列表表示通过）。
        /// 只管"轴这段行程能不能走"，不涉及帧长/行频/曝光——那些已经在相机侧配置好，
        /// 本模组不再重复校验。
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (ScanLengthMm <= 0)
                problems.Add($"扫描行程为 0：起点 '{StartPointName}' 与终点 '{EndPointName}' 位置相同。");

            if (VelocityMmPerSec <= 0)
                problems.Add($"终点 '{EndPointName}' 在轴点位表里的速度必须大于 0。");

            return problems;
        }

        /// <summary>一行摘要，便于日志中一眼看清这次扫描的关键量。</summary>
        public override string ToString()
            => $"{StartPointName}({StartMm:F2})→{EndPointName}({EndMm:F2}) ({ScanLengthMm:F2}) @ {VelocityMmPerSec:F1}/s, "
             + $"帧时间约 {EstimatedFrameTimeMs}ms（等帧超时 {FrameTimeoutMs}ms）";
    }
}
