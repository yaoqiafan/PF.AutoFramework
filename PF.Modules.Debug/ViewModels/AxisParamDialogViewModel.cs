using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Modules.Debug.ViewModels
{
    public class AxisParamDialogViewModel:PFDialogViewModelBase
    {

        public AxisParamDialogViewModel() 
        {
            Title = "轴参数更改";


            ConfirmCommand = new DelegateCommand(ONParamConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult()
                {
                    Result = ButtonResult.Cancel,
                });
            });
        }



        private AxisParamViewModel _ParamInstence;
        public AxisParamViewModel ParamInstence
        {
            get { return _ParamInstence; }
            set { SetProperty(ref _ParamInstence, value); }
        }


        #region 接口实现
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

         

            // 创建对话框参数，用于传递回调数据
            DialogParameters paras = new DialogParameters();
            paras.Add("CallBackParamItem", ParamInstence.ToModel());

            // 触发对话框关闭请求，返回确认结果和参数
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
                HomeAcc = model.HomeAcc,
                HomeDec = model.HomeDec,
                HomeOffest = model.HomeOffest,
                HomeModelFixed = model.HomeModelFixed,
                PelHomeModel = model.PelHomeModel,
                MelHomeModel = model.MelHomeModel,
                LimitVisEnable = model.LimitVisEnable,
                LimitPel = model.LimitPel,
                LimitMel = model.LimitMel,
                PositioningAccuracy = model.PositioningAccuracy,
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
                HomeAcc = viewModel.HomeAcc,
                HomeDec = viewModel.HomeDec,
                HomeOffest = viewModel.HomeOffest,
                HomeModelFixed = viewModel.HomeModelFixed,
                PelHomeModel = viewModel.PelHomeModel,
                MelHomeModel = viewModel.MelHomeModel,
                LimitVisEnable = viewModel.LimitVisEnable,
                LimitPel = viewModel.LimitPel,
                LimitMel = viewModel.LimitMel,
                PositioningAccuracy = viewModel.PositioningAccuracy,
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
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("1.运行速度")]
        [BrowsableAttribute(true)]
        public double Vel
        {
            get { return _vel; }
            set { SetProperty(ref _vel, value); }
        }

        private double _acc;
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("2.运行加速度")]
        [BrowsableAttribute(true)]
        public double Acc
        {
            get { return _acc; }
            set { SetProperty(ref _acc, value); }
        }

        private double _dec;
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("3.运行减速度")]
        [BrowsableAttribute(true)]
        public double Dec
        {
            get { return _dec; }
            set { SetProperty(ref _dec, value); }
        }


        private double _positioningAccuracy;
        [CategoryAttribute("A. 基本运行参数")]
        [DisplayNameAttribute("4.定位精度")]
        [BrowsableAttribute(true)]
        public double PositioningAccuracy
        {
            get { return _positioningAccuracy; }
            set { SetProperty(ref _positioningAccuracy, value); }
        }


        #endregion

        #region 限位与极限参数

        private bool _pelVisEnabled;
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("1.正极限硬限位启用")]
        [BrowsableAttribute(true)]
        public bool PelVisEnabled
        {
            get { return _pelVisEnabled; }
            set { SetProperty(ref _pelVisEnabled, value); }
        }

        private bool _melVisEnabled;
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("2.负极限硬限位启用")]
        [BrowsableAttribute(true)]
        public bool MelVisEnabled
        {
            get { return _melVisEnabled; }
            set { SetProperty(ref _melVisEnabled, value); }
        }

        private bool _orgVisEnabled;
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("3.原点限位启用")]
        [BrowsableAttribute(true)]
        public bool ORGVisEnabled
        {
            get { return _orgVisEnabled; }
            set { SetProperty(ref _orgVisEnabled, value); }
        }

        private bool _limitVisEnable;
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("4.软限位启用")]
        [BrowsableAttribute(true)]
        public bool LimitVisEnable
        {
            get { return _limitVisEnable; }
            set { SetProperty(ref _limitVisEnable, value); }
        }

        private double _limitPel;
        [CategoryAttribute("B. 限位与极限参数")]
        [DisplayNameAttribute("5.正极限软限位")]
        [BrowsableAttribute(true)]
        public double LimitPel
        {
            get { return _limitPel; }
            set { SetProperty(ref _limitPel, value); }
        }

        private double _limitMel;
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
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("1.回零模式")]
        [BrowsableAttribute(true)]
        public int HomeModel
        {
            get { return _homeModel; }
            set { SetProperty(ref _homeModel, value); }
        }

        private bool _homeModelFixed;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("2.回零模式固定标志")]
        [BrowsableAttribute(true)]
        public bool HomeModelFixed
        {
            get { return _homeModelFixed; }
            set { SetProperty(ref _homeModelFixed, value); }
        }

        private int _pelHomeModel;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("3.正极限回零模式")]
        [BrowsableAttribute(true)]
        public int PelHomeModel
        {
            get { return _pelHomeModel; }
            set { SetProperty(ref _pelHomeModel, value); }
        }

        private int _melHomeModel;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("4.负极限回零模式")]
        [BrowsableAttribute(true)]
        public int MelHomeModel
        {
            get { return _melHomeModel; }
            set { SetProperty(ref _melHomeModel, value); }
        }

        private double _homeVel;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("5.回零速度")]
        [BrowsableAttribute(true)]
        public double HomeVel
        {
            get { return _homeVel; }
            set { SetProperty(ref _homeVel, value); }
        }

        private double _homeAcc;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("6.回零加速度")]
        [BrowsableAttribute(true)]
        public double HomeAcc
        {
            get { return _homeAcc; }
            set { SetProperty(ref _homeAcc, value); }
        }

        private double _homeDec;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("7.回零减速度")]
        [BrowsableAttribute(true)]
        public double HomeDec
        {
            get { return _homeDec; }
            set { SetProperty(ref _homeDec, value); }
        }

        private double _homeOffest;
        [CategoryAttribute("C. 回零参数")]
        [DisplayNameAttribute("8.回零偏移")]
        [BrowsableAttribute(true)]
        public double HomeOffest
        {
            get { return _homeOffest; }
            set { SetProperty(ref _homeOffest, value); }
        }

        #endregion
    }
}
