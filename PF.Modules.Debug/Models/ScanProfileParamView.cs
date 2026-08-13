using PF.Core.Entities.Hardware.Vision;
using Prism.Mvvm;
using System.ComponentModel;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 扫描配方的可编辑视图对象，供 pf:PropertyGrid 直接绑定。
    ///
    /// <para><b>为什么不直接把 <see cref="ScanProfile"/> 丢给 PropertyGrid</b>：
    /// 一是 ScanProfile 是 PF.Core 的纯实体，不该为了界面去背 ComponentModel 特性；
    /// 二是它身上有十来个只读换算属性（帧长、行频、实走范围…），
    /// PropertyGrid 会把它们一并列出来，把真正要填的项淹掉；
    /// 三是它不实现 INotifyPropertyChanged，改完值界面上的换算结果不会跟着刷新。</para>
    ///
    /// <para>与框架里 ParamView + Mapper 的做法一致：视图对象负责"怎么显示、怎么编辑"，
    /// 实体负责"是什么、怎么算"，两者用 <see cref="ToProfile"/> 衔接。</para>
    /// </summary>
    public class ScanProfileParamView : BindableBase
    {
        #region 运动

        private double _scanStartMm;
        /// <summary>扫描起点（mm）</summary>
        [Category("运动")]
        [DisplayName("扫描起点(mm)")]
        [Description("图像第一行对应的轴位置。终点小于起点即为反向扫描。")]
        public double ScanStartMm { get => _scanStartMm; set => SetProperty(ref _scanStartMm, value); }

        private double _scanEndMm = 100;
        /// <summary>扫描终点（mm）</summary>
        [Category("运动")]
        [DisplayName("扫描终点(mm)")]
        [Description("图像最后一行对应的轴位置。")]
        public double ScanEndMm { get => _scanEndMm; set => SetProperty(ref _scanEndMm, value); }

        private double _scanVelocityMmPerSec = 50;
        /// <summary>扫描速度（mm/s）</summary>
        [Category("运动")]
        [DisplayName("扫描速度(mm/s)")]
        [Description("匀速段速度。它与行间距共同决定实际行频，行频又反过来限制曝光时间上限。")]
        public double ScanVelocityMmPerSec { get => _scanVelocityMmPerSec; set => SetProperty(ref _scanVelocityMmPerSec, value); }

        private double _positioningVelocityMmPerSec;
        /// <summary>定位速度（mm/s）</summary>
        [Category("运动")]
        [DisplayName("定位速度(mm/s)")]
        [Description("回起点等非扫描移动用。填 0 表示取扫描速度。")]
        public double PositioningVelocityMmPerSec { get => _positioningVelocityMmPerSec; set => SetProperty(ref _positioningVelocityMmPerSec, value); }

        private double _accelerationMmPerSec2 = 500;
        /// <summary>加速度（mm/s²）</summary>
        [Category("运动")]
        [DisplayName("加速度(mm/s²)")]
        [Description("决定理论加速距离 v²/(2a)，也就是起点前至少要留多少余量。")]
        public double AccelerationMmPerSec2 { get => _accelerationMmPerSec2; set => SetProperty(ref _accelerationMmPerSec2, value); }

        private double _decelerationMmPerSec2;
        /// <summary>减速度（mm/s²）</summary>
        [Category("运动")]
        [DisplayName("减速度(mm/s²)")]
        [Description("填 0 表示取加速度。")]
        public double DecelerationMmPerSec2 { get => _decelerationMmPerSec2; set => SetProperty(ref _decelerationMmPerSec2, value); }

        private double _sCurveTimeMs;
        /// <summary>S 曲线时间（ms）</summary>
        [Category("运动")]
        [DisplayName("S曲线时间(ms)")]
        [Description("透传给轴的柔性加减速时间。")]
        public double SCurveTimeMs { get => _sCurveTimeMs; set => SetProperty(ref _sCurveTimeMs, value); }

        private double _approachMarginMm;
        /// <summary>加速余量（mm）</summary>
        [Category("运动")]
        [DisplayName("加速余量(mm)")]
        [Description("起点前预留的加速距离，填 0 按理论值自动取。留余量是光学原因："
                   + "编码器同步下几何是位移锁定的，加减速不会让图像拉伸，但会让每行曝光时间不一致，"
                   + "表现为图像头尾亮度不均。")]
        public double ApproachMarginMm { get => _approachMarginMm; set => SetProperty(ref _approachMarginMm, value); }

        private double _overtravelMarginMm;
        /// <summary>减速余量（mm）</summary>
        [Category("运动")]
        [DisplayName("减速余量(mm)")]
        [Description("终点后预留的减速距离，填 0 按理论值自动取。")]
        public double OvertravelMarginMm { get => _overtravelMarginMm; set => SetProperty(ref _overtravelMarginMm, value); }

        #endregion

        #region 成像

        private double _lineSpacingUm = 10;
        /// <summary>行间距（μm/行）</summary>
        [Category("成像")]
        [DisplayName("行间距(μm/行)")]
        [Description("编码器当量 × 分频系数，即图像 Y 向的物理分辨率。"
                   + "填错的话图像 Y 向比例整体是错的，且帧长与行频都会跟着错。")]
        public double LineSpacingUm { get => _lineSpacingUm; set => SetProperty(ref _lineSpacingUm, value); }

        private double _exposureTimeUs = 50;
        /// <summary>曝光时间（μs）</summary>
        [Category("成像")]
        [DisplayName("曝光时间(μs)")]
        [Description("必须小于行周期，否则来不及曝光会掉行或整体偏暗。"
                   + "光不够应加光源或提高模拟增益，而不是降低扫描速度。")]
        public double ExposureTimeUs { get => _exposureTimeUs; set => SetProperty(ref _exposureTimeUs, value); }

        private int _maxLineRate;
        /// <summary>相机最大行频（行/秒）</summary>
        [Category("成像")]
        [DisplayName("相机最大行频(行/秒)")]
        [Description("填 0 表示不校验。填了之后，扫描速度导致行频超限时会在校验里拦下。")]
        public int MaxLineRate { get => _maxLineRate; set => SetProperty(ref _maxLineRate, value); }

        private bool _useFrameTrigger = true;
        /// <summary>是否用帧触发起帧</summary>
        [Category("成像")]
        [DisplayName("使用帧触发")]
        [Description("不勾为连续模式：开流到轴动之间的杂散编码器脉冲会被算进这一帧，图像整体偏移；"
                   + "勾上则在轴进入扫描区后才起帧，更可控。")]
        public bool UseFrameTrigger { get => _useFrameTrigger; set => SetProperty(ref _useFrameTrigger, value); }

        private double _frameTimeoutRatio = 3.0;
        /// <summary>帧超时倍数</summary>
        [Category("成像")]
        [DisplayName("帧超时倍数")]
        [Description("帧超时 = 理论帧时间 × 本倍数，最低 1000ms。")]
        public double FrameTimeoutRatio { get => _frameTimeoutRatio; set => SetProperty(ref _frameTimeoutRatio, value); }

        private int _axisTimeoutMs = 60_000;
        /// <summary>轴到位超时（ms）</summary>
        [Category("成像")]
        [DisplayName("轴到位超时(ms)")]
        [Description("回起点、等走完的超时。行程长、速度慢时要相应放宽。")]
        public int AxisTimeoutMs { get => _axisTimeoutMs; set => SetProperty(ref _axisTimeoutMs, value); }

        #endregion

        /// <summary>转成设备层使用的扫描配方实体。</summary>
        public ScanProfile ToProfile() => new()
        {
            ScanStartMm = ScanStartMm,
            ScanEndMm = ScanEndMm,
            ScanVelocityMmPerSec = ScanVelocityMmPerSec,
            PositioningVelocityMmPerSec = PositioningVelocityMmPerSec,
            AccelerationMmPerSec2 = AccelerationMmPerSec2,
            DecelerationMmPerSec2 = DecelerationMmPerSec2,
            SCurveTimeMs = SCurveTimeMs,
            ApproachMarginMm = ApproachMarginMm,
            OvertravelMarginMm = OvertravelMarginMm,
            LineSpacingUm = LineSpacingUm,
            ExposureTimeUs = ExposureTimeUs,
            MaxLineRate = MaxLineRate,
            UseFrameTrigger = UseFrameTrigger,
            FrameTimeoutRatio = FrameTimeoutRatio,
            AxisTimeoutMs = AxisTimeoutMs,
        };
    }
}
