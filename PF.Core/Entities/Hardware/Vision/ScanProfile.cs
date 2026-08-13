namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 线扫扫描配方 —— 把「轴怎么走」和「相机怎么拍」这两件事绑在一起的那组参数。
    ///
    /// <para><b>为什么要有这么一个东西</b>：线扫相机单独配置是配不出一张正确图像的。
    /// 帧长取决于扫描行程与行间距，行频取决于扫描速度与行间距，曝光上限又反过来受行频限制。
    /// 这几个量任何一个填错，症状都是"图不对"，但原因分别在轴、在相机、在光学，
    /// 现场很难分辨。本类把换算和约束集中起来，让配错在扫描**开始前**就被 <see cref="Validate"/> 挡住。</para>
    ///
    /// <para><b>坐标约定</b>：<see cref="ScanStartMm"/> 是图像第一行对应的轴位置，
    /// <see cref="ScanEndMm"/> 是最后一行。终点小于起点即为反向扫描，各处按符号自动处理。</para>
    /// </summary>
    public class ScanProfile
    {
        #region 运动

        /// <summary>扫描起点（mm）：图像第一行对应的轴位置。</summary>
        public double ScanStartMm { get; set; }

        /// <summary>扫描终点（mm）：图像最后一行对应的轴位置。小于起点即反向扫描。</summary>
        public double ScanEndMm { get; set; }

        /// <summary>扫描速度（mm/s）。整个扫描行程必须处于匀速段。</summary>
        public double ScanVelocityMmPerSec { get; set; }

        /// <summary>定位速度（mm/s）：回到起点等非扫描移动用。小于等于 0 时取扫描速度。</summary>
        public double PositioningVelocityMmPerSec { get; set; }

        /// <summary>加速度（mm/s²）。</summary>
        public double AccelerationMmPerSec2 { get; set; }

        /// <summary>减速度（mm/s²）。小于等于 0 时取加速度。</summary>
        public double DecelerationMmPerSec2 { get; set; }

        /// <summary>S 曲线时间（ms），透传给轴。</summary>
        public double SCurveTimeMs { get; set; }

        /// <summary>
        /// 起点前的加速余量（mm）。小于等于 0 时按 <see cref="TheoreticalAccelDistanceMm"/> 自动取值。
        /// <para>留余量是**光学原因**：编码器同步下几何是位移锁定的，加减速不会让图像拉伸，
        /// 但会让每行的曝光时间不一致，表现为图像头尾亮度不均。</para>
        /// </summary>
        public double ApproachMarginMm { get; set; }

        /// <summary>终点后的减速余量（mm）。小于等于 0 时按理论减速距离自动取值。</summary>
        public double OvertravelMarginMm { get; set; }

        #endregion

        #region 成像

        /// <summary>
        /// 行间距（μm/行）= 编码器当量 × 分频系数。图像 Y 向的物理分辨率，
        /// 直接决定帧长与行频，填错则图像 Y 向比例整体错误。
        /// </summary>
        public double LineSpacingUm { get; set; }

        /// <summary>曝光时间（μs）。必须小于行周期，否则来不及曝光会掉行或整体偏暗。</summary>
        public double ExposureTimeUs { get; set; }

        /// <summary>相机最大行频（行/秒）。大于 0 时参与校验，为 0 表示不校验。</summary>
        public int MaxLineRate { get; set; }

        /// <summary>是否用帧触发起帧。false = 连续模式，开流后攒满帧长即出图。</summary>
        public bool UseFrameTrigger { get; set; } = true;

        /// <summary>帧超时相对理论帧时间的倍数，默认 3 倍。</summary>
        public double FrameTimeoutRatio { get; set; } = 3.0;

        /// <summary>轴运动到位的等待超时（ms）。</summary>
        public int AxisTimeoutMs { get; set; } = 60_000;

        #endregion

        #region 换算

        /// <summary>扫描方向：终点大于等于起点为 +1，否则为 -1。</summary>
        public int Direction => ScanEndMm >= ScanStartMm ? 1 : -1;

        /// <summary>扫描行程（mm，恒为正）。</summary>
        public double ScanLengthMm => Math.Abs(ScanEndMm - ScanStartMm);

        /// <summary>实际减速度（未单独设置时取加速度）。</summary>
        public double EffectiveDeceleration => DecelerationMmPerSec2 > 0 ? DecelerationMmPerSec2 : AccelerationMmPerSec2;

        /// <summary>实际定位速度（未单独设置时取扫描速度）。</summary>
        public double EffectivePositioningVelocity
            => PositioningVelocityMmPerSec > 0 ? PositioningVelocityMmPerSec : ScanVelocityMmPerSec;

        /// <summary>理论加速距离（mm）= v² / (2a)。</summary>
        public double TheoreticalAccelDistanceMm => AccelerationMmPerSec2 > 0
            ? ScanVelocityMmPerSec * ScanVelocityMmPerSec / (2 * AccelerationMmPerSec2)
            : 0;

        /// <summary>理论减速距离（mm）。</summary>
        public double TheoreticalDecelDistanceMm => EffectiveDeceleration > 0
            ? ScanVelocityMmPerSec * ScanVelocityMmPerSec / (2 * EffectiveDeceleration)
            : 0;

        /// <summary>实际生效的加速余量。</summary>
        public double EffectiveApproachMarginMm
            => ApproachMarginMm > 0 ? ApproachMarginMm : TheoreticalAccelDistanceMm;

        /// <summary>实际生效的减速余量。</summary>
        public double EffectiveOvertravelMarginMm
            => OvertravelMarginMm > 0 ? OvertravelMarginMm : TheoreticalDecelDistanceMm;

        /// <summary>轴的实际起始位置（扫描起点往回退一个加速余量）。</summary>
        public double MoveStartMm => ScanStartMm - Direction * EffectiveApproachMarginMm;

        /// <summary>轴的实际终止位置（扫描终点往前多走一个减速余量）。</summary>
        public double MoveEndMm => ScanEndMm + Direction * EffectiveOvertravelMarginMm;

        /// <summary>帧长（行）= 扫描行程 ÷ 行间距。</summary>
        public int FrameHeightLines => LineSpacingUm > 0
            ? (int)Math.Round(ScanLengthMm * 1000.0 / LineSpacingUm)
            : 0;

        /// <summary>实际行频（行/秒）= 扫描速度 ÷ 行间距。</summary>
        public double ActualLineRate => LineSpacingUm > 0
            ? ScanVelocityMmPerSec * 1000.0 / LineSpacingUm
            : 0;

        /// <summary>行周期（μs），即单行可用的最长曝光时间。</summary>
        public double MaxExposureTimeUs => ActualLineRate > 0 ? 1_000_000.0 / ActualLineRate : 0;

        /// <summary>理论帧时间（ms）= 扫描行程 ÷ 扫描速度。</summary>
        public int EstimatedFrameTimeMs => ScanVelocityMmPerSec > 0
            ? (int)Math.Ceiling(ScanLengthMm / ScanVelocityMmPerSec * 1000.0)
            : 0;

        /// <summary>帧超时（ms）：理论帧时间 × 余量倍数，至少 1000ms。</summary>
        public int FrameTimeoutMs => Math.Max(1000, (int)(EstimatedFrameTimeMs * FrameTimeoutRatio));

        #endregion

        /// <summary>
        /// 校验配方自洽性，返回问题清单（空列表表示通过）。
        /// <para>这些问题的共同点是：不拦住的话，症状都只是"图不对"，
        /// 而原因分别落在轴、相机、光学三处，现场排查代价远高于这里挡一道。</para>
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (LineSpacingUm <= 0)
                problems.Add("行间距未设置（应为 编码器当量 × 分频系数）。");

            if (ScanLengthMm <= 0)
                problems.Add("扫描行程为 0：起点与终点相同。");

            if (ScanVelocityMmPerSec <= 0)
                problems.Add("扫描速度必须大于 0。");

            if (AccelerationMmPerSec2 <= 0)
                problems.Add("加速度必须大于 0。");

            if (FrameHeightLines <= 0)
                problems.Add("按行程与行间距算出的帧长为 0，请检查两者。");

            if (MaxLineRate > 0 && ActualLineRate > MaxLineRate)
            {
                problems.Add($"行频超上限：当前 {ActualLineRate:F0} 行/秒 > 相机上限 {MaxLineRate} 行/秒。"
                    + $"需降低扫描速度到 {MaxLineRate * LineSpacingUm / 1000.0:F1} mm/s 以下，或增大分频系数。");
            }

            if (ExposureTimeUs > 0 && MaxExposureTimeUs > 0 && ExposureTimeUs > MaxExposureTimeUs)
            {
                problems.Add($"曝光时间超过行周期：{ExposureTimeUs:F1}μs > {MaxExposureTimeUs:F1}μs。"
                    + "会掉行或整体偏暗。请缩短曝光并靠加光源/提高模拟增益补亮度，而不是降低扫描速度。");
            }

            // 余量不足只是警告级别的提示，但同样会毁掉一整帧，故一并拦下
            if (EffectiveApproachMarginMm < TheoreticalAccelDistanceMm)
            {
                problems.Add($"加速余量不足：{EffectiveApproachMarginMm:F2}mm < 理论加速距离 "
                    + $"{TheoreticalAccelDistanceMm:F2}mm，扫描起始段仍在加速，图像头部会偏亮/偏暗。");
            }

            if (EffectiveOvertravelMarginMm < TheoreticalDecelDistanceMm)
            {
                problems.Add($"减速余量不足：{EffectiveOvertravelMarginMm:F2}mm < 理论减速距离 "
                    + $"{TheoreticalDecelDistanceMm:F2}mm，扫描尾段已在减速，图像尾部亮度异常。");
            }

            return problems;
        }

        /// <summary>一行摘要，便于日志中一眼看清这次扫描的关键量。</summary>
        public override string ToString()
            => $"行程 {ScanStartMm:F2}→{ScanEndMm:F2}mm ({ScanLengthMm:F2}mm) @ {ScanVelocityMmPerSec:F1}mm/s, "
             + $"行间距 {LineSpacingUm:F3}μm, 帧长 {FrameHeightLines} 行, "
             + $"行频 {ActualLineRate:F0} 行/秒 (曝光上限 {MaxExposureTimeUs:F1}μs), "
             + $"实走 {MoveStartMm:F2}→{MoveEndMm:F2}mm, 帧时间约 {EstimatedFrameTimeMs}ms";
    }
}
