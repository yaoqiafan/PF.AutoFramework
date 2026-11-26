using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>轴参数对话框 ViewModel</summary>
    public class AxisParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化轴参数对话框 ViewModel</summary>
        public AxisParamDialogViewModel()
        {
            Title = "轴参数更改";

            ConfirmCommand = new DelegateCommand(ONParamConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[轴参数] 用户[{CurrentUserName}] 取消修改轴参数", "操作日志");
                RequestClose.Invoke(new DialogResult()
                {
                    Result = ButtonResult.Cancel,
                });
            });
        }

        private AxisParamViewModel _ParamInstence;

        /// <summary>获取或设置轴参数实例</summary>
        public AxisParamViewModel ParamInstence // 提示：Instence 可能是拼写错误，建议改为 Instance
        {
            get { return _ParamInstence; }
            set { SetProperty(ref _ParamInstence, value); }
        }

        #region 接口实现

        /// <summary>对话框打开时加载轴参数</summary>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
            {
                var paramItem = parameters.GetValue<AxisParam>("Data");
                ParamInstence = paramItem.ToViewModel();
            }
        }

        private void ONParamConfirmed()
        {
            if (ParamInstence == null)
                return;

            // 非法减速比运行时会按 1 处理：界面显示的值与轴实际用的值不一致，现场看不出来，确认前直接拦下
            var gearRatio = ParamInstence.GearRatio;
            if (!(gearRatio > 0) || double.IsInfinity(gearRatio))
            {
                MessageService.ShowMessage($"减速比 {gearRatio} 无效，需为大于 0 的数（直连填 1）。参数未保存。",
                    "轴参数", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogService.Info(
                $"[轴参数] 用户[{CurrentUserName}] 确认修改轴参数 | " +
                $"速度:{ParamInstence.Vel} 加速度:{ParamInstence.Acc} 减速度:{ParamInstence.Dec} " +
                $"定位精度:{ParamInstence.PositioningAccuracy} 回零速度:{ParamInstence.HomeVel} 回零低速倍率:{ParamInstence.HomeLowVelRatio} 减速比:{ParamInstence.GearRatio}",
                "操作日志");

            DialogParameters paras = new DialogParameters();
            paras.Add("CallBackParamItem", ParamInstence.ToModel());

            RequestClose.Invoke(new DialogResult()
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });
        }

        #endregion
    }

    /// <summary>
    /// 轴参数实体与 ViewModel 转换扩展方法
    /// </summary>
    public static class AxisParamExtensions
    {
        /// <summary>
        /// 将 Model (AxisParam) 转换为 ViewModel (AxisParamViewModel)
        /// </summary>
        public static AxisParamViewModel ToViewModel(this AxisParam model)
        {
            if (model == null) return null;

            return new AxisParamViewModel
            {
                Vel = model.Vel,
                Acc = model.Acc,
                Dec = model.Dec,
                PelVisEnabled = model.PelVisEnabled,
                MelVisEnabled = model.MelVisEnabled,
                ORGVisEnabled = model.ORGVisEnabled,
                HomeModel = model.HomeModel,
                HomeVel = model.HomeVel,
                HomeLowVelRatio = model.HomeLowVelRatio,
                HomeAcc = model.HomeAcc,
                HomeDec = model.HomeDec,
                HomeOffset = model.HomeOffset,
                HomeModelFixed = model.HomeModelFixed,
                PelHomeModel = model.PelHomeModel,
                MelHomeModel = model.MelHomeModel,
                LimitVisEnable = model.LimitVisEnable,
                LimitPel = model.LimitPel,
                LimitMel = model.LimitMel,
                PositioningAccuracy = model.PositioningAccuracy,
                GearRatio = model.GearRatio,
            };
        }

        /// <summary>
        /// 将 ViewModel (AxisParamViewModel) 转换为 Model (AxisParam)
        /// </summary>
        public static AxisParam ToModel(this AxisParamViewModel viewModel)
        {
            if (viewModel == null) return null;

            return new AxisParam
            {
                Vel = viewModel.Vel,
                Acc = viewModel.Acc,
                Dec = viewModel.Dec,
                PelVisEnabled = viewModel.PelVisEnabled,
                MelVisEnabled = viewModel.MelVisEnabled,
                ORGVisEnabled = viewModel.ORGVisEnabled,
                HomeModel = viewModel.HomeModel,
                HomeVel = viewModel.HomeVel,
                HomeLowVelRatio = viewModel.HomeLowVelRatio,
                HomeAcc = viewModel.HomeAcc,
                HomeDec = viewModel.HomeDec,
                HomeOffset = viewModel.HomeOffset,
                HomeModelFixed = viewModel.HomeModelFixed,
                PelHomeModel = viewModel.PelHomeModel,
                MelHomeModel = viewModel.MelHomeModel,
                LimitVisEnable = viewModel.LimitVisEnable,
                LimitPel = viewModel.LimitPel,
                LimitMel = viewModel.LimitMel,
                PositioningAccuracy = viewModel.PositioningAccuracy,
                GearRatio = viewModel.GearRatio,
            };
        }
    }

    /// <summary>
    /// 轴参数信息 ViewModel
    /// </summary>
    public class AxisParamViewModel : BindableBase
    {
        #region 基本运行参数

        private double _vel;
        /// <summary>获取或设置运行速度</summary>
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("1.运行速度")]
        [BrowsableAttribute(true)]
        public double Vel
        {
            get { return _vel; }
            set { SetProperty(ref _vel, value); }
        }

        private double _acc;
        /// <summary>获取或设置运行加速度</summary>
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("2.运行加速度")]
        [BrowsableAttribute(true)]
        public double Acc
        {
            get { return _acc; }
            set { SetProperty(ref _acc, value); }
        }

        private double _dec;
        /// <summary>获取或设置运行减速度</summary>
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("3.运行减速度")]
        [BrowsableAttribute(true)]
        public double Dec
        {
            get { return _dec; }
            set { SetProperty(ref _dec, value); }
        }

        private double _positioningAccuracy;
        /// <summary>获取或设置定位精度</summary>
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("4.定位精度")]
        [BrowsableAttribute(true)]
        public double PositioningAccuracy
        {
            get { return _positioningAccuracy; }
            set { SetProperty(ref _positioningAccuracy, value); }
        }

        private double _gearRatio = 1;
        /// <summary>获取或设置减速比</summary>
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("5.减速比")]
        [DescriptionAttribute("电机侧 → 负载侧的传动比（电机转 N 圈、负载侧转 1 圈时为 N），默认 1（直连，不换算）。不为 1 时，轴的位置 / 速度 / 加减速按负载侧单位：下发板卡前乘以本值，读回位置除以本值。板卡的脉冲当量须按电机侧配置；已按负载侧配好的轴保持 1。")]
        [BrowsableAttribute(true)]
        public double GearRatio
        {
            get { return _gearRatio; }
            set { SetProperty(ref _gearRatio, value); }
        }

        #endregion

        #region 限位与极限参数

        private bool _pelVisEnabled;
        /// <summary>获取或设置正极限硬限位启用</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("1.正极限硬限位启用")]
        [BrowsableAttribute(true)]
        public bool PelVisEnabled
        {
            get { return _pelVisEnabled; }
            set { SetProperty(ref _pelVisEnabled, value); }
        }

        private bool _melVisEnabled;
        /// <summary>获取或设置负极限硬限位启用</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("2.负极限硬限位启用")]
        [BrowsableAttribute(true)]
        public bool MelVisEnabled
        {
            get { return _melVisEnabled; }
            set { SetProperty(ref _melVisEnabled, value); }
        }

        private bool _orgVisEnabled;
        /// <summary>获取或设置原点限位启用</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("3.原点限位启用")]
        [BrowsableAttribute(true)]
        public bool ORGVisEnabled
        {
            get { return _orgVisEnabled; }
            set { SetProperty(ref _orgVisEnabled, value); }
        }

        private bool _limitVisEnable;
        /// <summary>获取或设置软限位启用</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("4.软限位启用")]
        [BrowsableAttribute(true)]
        public bool LimitVisEnable
        {
            get { return _limitVisEnable; }
            set { SetProperty(ref _limitVisEnable, value); }
        }

        private double _limitPel;
        /// <summary>获取或设置正极限软限位</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("5.正极限软限位")]
        [BrowsableAttribute(true)]
        public double LimitPel
        {
            get { return _limitPel; }
            set { SetProperty(ref _limitPel, value); }
        }

        private double _limitMel;
        /// <summary>获取或设置负极限软限位</summary>
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("6.负极限软限位")]
        [BrowsableAttribute(true)]
        public double LimitMel
        {
            get { return _limitMel; }
            set { SetProperty(ref _limitMel, value); }
        }

        #endregion

        #region 回零参数

        private int _homeModel;
        /// <summary>获取或设置回零模式</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("1.回零模式")]
        [BrowsableAttribute(true)]
        public int HomeModel
        {
            get { return _homeModel; }
            set { SetProperty(ref _homeModel, value); }
        }

        private bool _homeModelFixed;
        /// <summary>获取或设置回零模式固定标志</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("2.回零模式固定标志")]
        [BrowsableAttribute(true)]
        public bool HomeModelFixed
        {
            get { return _homeModelFixed; }
            set { SetProperty(ref _homeModelFixed, value); }
        }

        private int _pelHomeModel;
        /// <summary>获取或设置正极限回零模式</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("3.正极限回零模式")]
        [BrowsableAttribute(true)]
        public int PelHomeModel
        {
            get { return _pelHomeModel; }
            set { SetProperty(ref _pelHomeModel, value); }
        }

        private int _melHomeModel;
        /// <summary>获取或设置负极限回零模式</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("4.负极限回零模式")]
        [BrowsableAttribute(true)]
        public int MelHomeModel
        {
            get { return _melHomeModel; }
            set { SetProperty(ref _melHomeModel, value); }
        }

        private double _homeVel;
        /// <summary>获取或设置回零速度</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("5.回零速度")]
        [BrowsableAttribute(true)]
        public double HomeVel
        {
            get { return _homeVel; }
            set { SetProperty(ref _homeVel, value); }
        }

        private double _homeLowVelRatio = 10;
        /// <summary>获取或设置回零低速倍率（除数）</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("6.回零低速倍率")]
        [DescriptionAttribute("回零低速 = 回零速度 ÷ 本值，默认 10。低速用于回零第二段（反向慢速寻原点 / 寻 Z 相）；需 ≥ 1，小于 1 按 10 处理。")]
        [BrowsableAttribute(true)]
        public double HomeLowVelRatio
        {
            get { return _homeLowVelRatio; }
            set { SetProperty(ref _homeLowVelRatio, value); }
        }

        private double _homeAcc;
        /// <summary>获取或设置回零加速度</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("7.回零加速度")]
        [BrowsableAttribute(true)]
        public double HomeAcc
        {
            get { return _homeAcc; }
            set { SetProperty(ref _homeAcc, value); }
        }

        private double _homeDec;
        /// <summary>获取或设置回零减速度</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("8.回零减速度")]
        [BrowsableAttribute(true)]
        public double HomeDec
        {
            get { return _homeDec; }
            set { SetProperty(ref _homeDec, value); }
        }

        private double _homeOffest;
        /// <summary>获取或设置回零偏移</summary>
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("9.回零偏移")]
        [BrowsableAttribute(true)]
        public double HomeOffset
        {
            get { return _homeOffest; }
            set { SetProperty(ref _homeOffest, value); }
        }

        #endregion
    }
}