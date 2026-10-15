using PF.Core.Entities.Hardware;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 一次扫描的几何量——由起点/终点两个轴点位算出，不持有任何可独立编辑的运动参数。
    /// 帧长/行频/曝光这些成像参数仍配置在相机自己身上（UserSetDefault），本模组不下发覆盖；
    /// 但曝光必须跟扫描速度匹配（曝光 ≤ 行周期 = 像素精度 ÷ 速度），行程也必须凑得满一帧
    /// （行程 ÷ 像素精度 ≥ 帧高），这里提供换算与校验（<see cref="MaxExposureUs"/>/<see cref="CheckExposure"/>/
    /// <see cref="CheckFrameFill"/>），像素精度取自相机的 <c>LineSpacingUm</c>。
    ///
    /// <para><b>单位一律是微米</b>：扫描轴点位表按脉冲填、1 脉冲 = 1μm，所以位置是 μm、速度是 μm/s、
    /// 加减速是 μm/s²，与像素精度（μm/行）同一量纲，换算时不再有 mm 与 μm 之间的 1000 倍。</para>
    ///
    /// <para><b>为什么速度/加减速/S曲线时间不是本类自己的字段</b>：它们全部来自"终点"这个点位
    /// 自身的配置（<see cref="AxisPoint.Speed"/>/<see cref="AxisPoint.Acc"/>/
    /// <see cref="AxisPoint.Dec"/>/<see cref="AxisPoint.STime"/>）——这几个量本来就该在轴的
    /// 点位表里维护一份，机台上其它轴全都这么用，线扫轴没有理由另起一套。<see cref="AxisPoint.STime"/>
    /// 的单位是秒，这里原样透传。</para>
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

        /// <summary>起点位置（μm）。</summary>
        public double StartUm { get; }

        /// <summary>终点位置（μm）。</summary>
        public double EndUm { get; }

        /// <summary>扫描速度（μm/s）= 终点点位自身配置的速度。</summary>
        public double VelocityUmPerSec { get; }

        /// <summary>加速度（μm/s²）= 终点点位自身配置的加速度。</summary>
        public double AccelerationUmPerSec2 { get; }

        /// <summary>减速度（μm/s²）= 终点点位自身配置的减速度。</summary>
        public double DecelerationUmPerSec2 { get; }

        /// <summary>S 曲线时间（秒）= 终点点位自身配置的值，与 <see cref="AxisPoint.STime"/> 同单位。</summary>
        public double SCurveTimeSec { get; }

        /// <summary>由起点/终点两个轴点位构造（点位按 μm、μm/s 填）。</summary>
        public ScanGeometry(AxisPoint start, AxisPoint end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            StartPointName = start.Name;
            EndPointName = end.Name;
            StartUm = start.TargetPosition;
            EndUm = end.TargetPosition;
            VelocityUmPerSec = end.Speed;
            AccelerationUmPerSec2 = end.Acc;
            DecelerationUmPerSec2 = end.Dec;
            SCurveTimeSec = end.STime;
        }

        /// <summary>扫描方向：终点大于等于起点为 +1，否则为 -1。</summary>
        public int Direction => EndUm >= StartUm ? 1 : -1;

        /// <summary>扫描行程（μm，恒为正）。</summary>
        public double ScanLengthUm => Math.Abs(EndUm - StartUm);

        /// <summary>实际减速度（终点未单独配置减速度时取加速度）。</summary>
        public double EffectiveDeceleration => DecelerationUmPerSec2 > 0 ? DecelerationUmPerSec2 : AccelerationUmPerSec2;

        /// <summary>
        /// 理论加速距离（μm）= v²/(2a)，仅供参考——不是硬性校验（余量已经交给起点/终点两个点位
        /// 自己的位置去把握），但仍然是判断"这段行程够不够轴提上速"的有用信息，在调试页展示。
        /// </summary>
        public double TheoreticalAccelDistanceUm => AccelerationUmPerSec2 > 0
            ? VelocityUmPerSec * VelocityUmPerSec / (2 * AccelerationUmPerSec2)
            : 0;

        /// <summary>理论减速距离（μm），含义同 <see cref="TheoreticalAccelDistanceUm"/>。</summary>
        public double TheoreticalDecelDistanceUm => EffectiveDeceleration > 0
            ? VelocityUmPerSec * VelocityUmPerSec / (2 * EffectiveDeceleration)
            : 0;

        /// <summary>
        /// 理论帧时间（ms）= 扫描行程 ÷ 扫描速度 + 加减速多花的时间（各 v/2a）+ S曲线时间，只用来估一个等帧超时，不追求精确。
        /// <para>加减速必须算进去：行程短、加速度小的点位，轴大半段都在加减速，只按匀速估会把超时给短，
        /// 轴还没走完就报取帧超时。</para>
        /// </summary>
        public int EstimatedFrameTimeMs
        {
            get
            {
                if (VelocityUmPerSec <= 0) return 0;
                double sec = ScanLengthUm / VelocityUmPerSec;
                if (AccelerationUmPerSec2 > 0) sec += VelocityUmPerSec / (2 * AccelerationUmPerSec2);
                if (EffectiveDeceleration > 0) sec += VelocityUmPerSec / (2 * EffectiveDeceleration);
                sec += Math.Max(0, SCurveTimeSec);
                return (int)Math.Ceiling(sec * 1000.0);
            }
        }

        /// <summary>等一帧完整图像的超时（ms）：理论帧时间 × 余量倍数，至少 1000ms。</summary>
        public int FrameTimeoutMs => Math.Max(1000, (int)(EstimatedFrameTimeMs * FrameTimeoutRatio));

        /// <summary>
        /// 校验起点/终点自洽性，返回问题清单（空列表表示通过）。
        /// 只管"轴这段行程能不能走"；曝光与速度、行程与帧高的匹配见 <see cref="CheckExposure"/>/<see cref="CheckFrameFill"/>。
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (ScanLengthUm <= 0)
                problems.Add($"扫描行程为 0：起点 '{StartPointName}' 与终点 '{EndPointName}' 位置相同。");

            if (VelocityUmPerSec <= 0)
                problems.Add($"终点 '{EndPointName}' 在轴点位表里的速度必须大于 0。");

            return problems;
        }

        // ── 曝光与速度的匹配 ─────────────────────────────────────────────────
        //
        // 线阵相机每走过一个像素精度（行间距）触发一行，曝光必须在下一行触发前结束：
        //   行频 (Hz)      = 速度 (μm/s) ÷ 像素精度 (μm/行)
        //   最大曝光 (μs)  = 像素精度 (μm/行) ÷ 速度 (μm/s) × 10⁶   （即行周期）
        // 曝光超过行周期，相机跟不上行触发会丢行，一帧永远攒不满，表现为取帧超时——
        // 原因却看不出来。

        /// <summary>曝光超过最大曝光的这个比例时提醒（未超限，但余量不多）。</summary>
        public const double ExposureWarnRatio = 0.9;

        /// <summary>行频（Hz）；像素精度或速度 ≤0 时为 0。</summary>
        public double LineRateHz(double lineSpacingUm)
            => lineSpacingUm > 0 && VelocityUmPerSec > 0 ? VelocityUmPerSec / lineSpacingUm : 0;

        /// <summary>当前速度下允许的最大曝光（μs）= 行周期；像素精度或速度 ≤0 时为 0（无法计算）。</summary>
        public double MaxExposureUs(double lineSpacingUm)
            => lineSpacingUm > 0 && VelocityUmPerSec > 0 ? lineSpacingUm / VelocityUmPerSec * 1_000_000.0 : 0;

        /// <summary>
        /// 按指定曝光反算允许的最大扫描速度（μm/s）= 像素精度 ÷ 曝光 × 10⁶；参数 ≤0 时为 0。
        /// </summary>
        public static double MaxVelocityFor(double exposureUs, double lineSpacingUm)
            => exposureUs > 0 && lineSpacingUm > 0 ? lineSpacingUm / exposureUs * 1_000_000.0 : 0;

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
             + $"（终点 '{EndPointName}' 速度 {VelocityUmPerSec:F0}μm/s、像素精度 {lineSpacingUm:F3}μm/行、行频 {LineRateHz(lineSpacingUm):F0}Hz）"
             + $"——相机跟不上行触发会丢行、取帧超时。请把曝光降到 {MaxExposureUs(lineSpacingUm):F1}μs 以下，"
             + $"或把终点速度降到 {MaxVelocityFor(exposureUs, lineSpacingUm):F0}μm/s 以下。";

        // ── 行程能否凑满一帧 ─────────────────────────────────────────────────
        //
        // 行触发按位移产生（编码器每走一个像素精度出一行），与速度、加减速无关：
        //   可产生行数 = 扫描行程 (μm) ÷ 像素精度 (μm/行)
        //   凑满一帧所需行程 (μm) = 帧高 (行) × 像素精度 (μm/行)
        // 可产生行数少于帧高，相机永远等不到最后几行，一帧出不来，表现为取帧超时、
        // 轴却已经停在终点——症状和"触发没接上"一模一样，必须在动轴之前拦下。

        /// <summary>这段行程能产生的行数；像素精度 ≤0 时为 0（无法计算）。</summary>
        public double ProducibleLines(double lineSpacingUm)
            => lineSpacingUm > 0 ? ScanLengthUm / lineSpacingUm : 0;

        /// <summary>凑满 <paramref name="frameHeight"/> 行所需的扫描行程（μm）；参数 ≤0 时为 0。</summary>
        public static double RequiredScanLengthUm(int frameHeight, double lineSpacingUm)
            => frameHeight > 0 && lineSpacingUm > 0 ? frameHeight * lineSpacingUm : 0;

        /// <summary>
        /// 行程凑帧校验：可产生行数少于帧高返回 <see cref="FrameFillCheck.Insufficient"/>；
        /// 帧高或像素精度未知时返回 <see cref="FrameFillCheck.Unknown"/>（不校验）。
        /// </summary>
        public FrameFillCheck CheckFrameFill(int? frameHeight, double lineSpacingUm)
        {
            if (frameHeight is not > 0 || lineSpacingUm <= 0) return FrameFillCheck.Unknown;
            // 留 0.5 行的浮点余量：行程恰好等于帧高 × 像素精度时不该误判
            return ProducibleLines(lineSpacingUm) + 0.5 < frameHeight.Value ? FrameFillCheck.Insufficient : FrameFillCheck.Ok;
        }

        /// <summary>行程凑不满一帧时给操作员看的一句话（含缺多少行程、终点该挪到哪）。</summary>
        public string DescribeFrameFillInsufficient(int frameHeight, double lineSpacingUm)
        {
            double required = RequiredScanLengthUm(frameHeight, lineSpacingUm);
            double suggestedEnd = StartUm + Direction * required;
            return $"扫描行程 {ScanLengthUm:F0}μm（'{StartPointName}' {StartUm:F0} → '{EndPointName}' {EndUm:F0}）"
                 + $"按像素精度 {lineSpacingUm:F3}μm/行只能产生 {ProducibleLines(lineSpacingUm):F0} 行，"
                 + $"不够相机帧高 {frameHeight} 行——一帧凑不满，取帧必超时。"
                 + $"凑满一帧至少需要 {required:F0}μm 行程（终点至少到 {suggestedEnd:F0}），或把相机帧高降到 "
                 + $"{(int)Math.Floor(ProducibleLines(lineSpacingUm))} 行以下。";
        }

        /// <summary>一行摘要，便于日志中一眼看清这次扫描的关键量。</summary>
        public override string ToString()
            => $"{StartPointName}({StartUm:F0}μm)→{EndPointName}({EndUm:F0}μm) 行程 {ScanLengthUm:F0}μm @ {VelocityUmPerSec:F0}μm/s, "
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

    /// <summary>扫描行程与相机帧高的匹配校验结果（见 <see cref="ScanGeometry.CheckFrameFill"/>）。</summary>
    public enum FrameFillCheck
    {
        /// <summary>无法校验：帧高读不到或像素精度未配置。</summary>
        Unknown,
        /// <summary>行程足够凑满一帧。</summary>
        Ok,
        /// <summary>行程产生的行数少于帧高，一帧凑不满、取帧必超时——必须拦截。</summary>
        Insufficient,
    }
}
