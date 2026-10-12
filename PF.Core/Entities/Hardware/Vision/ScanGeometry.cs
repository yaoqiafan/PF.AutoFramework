using PF.Core.Entities.Hardware;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 一次扫描的几何量——由起点/终点两个轴点位算出，不持有任何可独立编辑的运动参数。
    /// 帧长/行频/曝光这些成像参数仍配置在相机自己身上（UserSetDefault），本模组不下发覆盖；
    /// 但曝光必须跟扫描速度匹配（曝光 ≤ 行周期 = 像素精度 ÷ 速度），这里提供换算与校验
    /// （<see cref="MaxExposureUs"/>/<see cref="CheckExposure"/>），像素精度取自相机的 <c>LineSpacingUm</c>。
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

        // ── 曝光与速度的匹配 ─────────────────────────────────────────────────
        //
        // 线阵相机每走过一个像素精度（行间距）触发一行，曝光必须在下一行触发前结束：
        //   行频 (Hz)      = 速度 (mm/s) × 1000 ÷ 像素精度 (μm/行)
        //   最大曝光 (μs)  = 像素精度 (μm/行) ÷ 速度 (mm/s) × 1000   （即行周期）
        // 曝光超过行周期，相机跟不上行触发会丢行，一帧永远攒不满，表现为取帧超时——
        // 原因却看不出来。速度单位按轴点位表的工程单位 mm/s 计。

        /// <summary>曝光超过最大曝光的这个比例时提醒（未超限，但余量不多）。</summary>
        public const double ExposureWarnRatio = 0.9;

        /// <summary>行频（Hz）；像素精度或速度 ≤0 时为 0。</summary>
        public double LineRateHz(double lineSpacingUm)
            => lineSpacingUm > 0 && VelocityMmPerSec > 0 ? VelocityMmPerSec * 1000.0 / lineSpacingUm : 0;

        /// <summary>当前速度下允许的最大曝光（μs）= 行周期；像素精度或速度 ≤0 时为 0（无法计算）。</summary>
        public double MaxExposureUs(double lineSpacingUm)
            => lineSpacingUm > 0 && VelocityMmPerSec > 0 ? lineSpacingUm / VelocityMmPerSec * 1000.0 : 0;

        /// <summary>
        /// 按指定曝光反算允许的最大扫描速度（mm/s）= 像素精度 ÷ 曝光 × 1000；参数 ≤0 时为 0。
        /// </summary>
        public static double MaxVelocityFor(double exposureUs, double lineSpacingUm)
            => exposureUs > 0 && lineSpacingUm > 0 ? lineSpacingUm / exposureUs * 1000.0 : 0;

        /// <summary>
        /// 曝光校验：<paramref name="exposureUs"/> 超过最大曝光（行周期）返回 <see cref="ExposureCheck.Exceeded"/>，
        /// 超过 <see cref="ExposureWarnRatio"/> 返回 <see cref="ExposureCheck.NearLimit"/>；像素精度未配置、
        /// 曝光未指定或速度无效时返回 <see cref="ExposureCheck.Unknown"/>（不校验）。
        /// </summary>
        public ExposureCheck CheckExposure(double? exposureUs, double lineSpacingUm)
        {
            double max = MaxExposureUs(lineSpacingUm);
            if (exposureUs is not > 0 || max <= 0) return ExposureCheck.Unknown;
            if (exposureUs.Value > max) return ExposureCheck.Exceeded;
            return exposureUs.Value > max * ExposureWarnRatio ? ExposureCheck.NearLimit : ExposureCheck.Ok;
        }

        /// <summary>曝光超限时给操作员看的一句话（含行频、最大曝光、两种改法）。</summary>
        public string DescribeExposureExceeded(double exposureUs, double lineSpacingUm)
            => $"曝光 {exposureUs:F1}μs 超过当前速度下的最大曝光 {MaxExposureUs(lineSpacingUm):F1}μs"
             + $"（终点 '{EndPointName}' 速度 {VelocityMmPerSec:F2}mm/s、像素精度 {lineSpacingUm:F3}μm/行、行频 {LineRateHz(lineSpacingUm):F0}Hz）"
             + $"——相机跟不上行触发会丢行、取帧超时。请把曝光降到 {MaxExposureUs(lineSpacingUm):F1}μs 以下，"
             + $"或把终点速度降到 {MaxVelocityFor(exposureUs, lineSpacingUm):F2}mm/s 以下。";

        /// <summary>一行摘要，便于日志中一眼看清这次扫描的关键量。</summary>
        public override string ToString()
            => $"{StartPointName}({StartMm:F2})→{EndPointName}({EndMm:F2}) ({ScanLengthMm:F2}) @ {VelocityMmPerSec:F1}/s, "
             + $"帧时间约 {EstimatedFrameTimeMs}ms（等帧超时 {FrameTimeoutMs}ms）";
    }

    /// <summary>曝光与扫描速度的匹配校验结果（见 <see cref="ScanGeometry.CheckExposure"/>）。</summary>
    public enum ExposureCheck
    {
        /// <summary>无法校验：像素精度未配置、曝光未指定或速度无效。</summary>
        Unknown,
        /// <summary>曝光在最大曝光的 90% 以内。</summary>
        Ok,
        /// <summary>曝光超过最大曝光的 90%，未超限但余量不多。</summary>
        NearLimit,
        /// <summary>曝光超过最大曝光（行周期），会丢行、取帧超时——必须拦截。</summary>
        Exceeded,
    }
}
