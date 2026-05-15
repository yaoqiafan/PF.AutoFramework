using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Windows.Media;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// 晶圆盒单槽检测详情弹窗 ViewModel
    /// </summary>
    public class WaferSlotDetailViewModel : PFDialogViewModelBase
    {
        private int _displayIndex;
        private string _statusText = string.Empty;
        private Brush _statusBrush = Brushes.Gray;
        private string _detectionTime = string.Empty;
        private string _customerBatch = string.Empty;
        private string _ocrText = string.Empty;
        private string _barcode1 = string.Empty;
        private string _operatorId = string.Empty;
        private string _recipeName = string.Empty;
        private string _imagePath = string.Empty;
        private bool _isMatch;
        private string _errorMessage = string.Empty;
        private MachineDetectionData _detectionData;

        /// <summary>1-based 显示层号</summary>
        public int DisplayIndex
        {
            get => _displayIndex;
            private set => SetProperty(ref _displayIndex, value);
        }

        /// <summary>状态文字（OK / NG）</summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>状态颜色（绿 = OK，红 = NG）</summary>
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        /// <summary>检测时间</summary>
        public string DetectionTime
        {
            get => _detectionTime;
            private set => SetProperty(ref _detectionTime, value);
        }

        /// <summary>客户批次</summary>
        public string CustomerBatch
        {
            get => _customerBatch;
            private set => SetProperty(ref _customerBatch, value);
        }

        /// <summary>OCR 读取值</summary>
        public string OcrText
        {
            get => _ocrText;
            private set => SetProperty(ref _ocrText, value);
        }

        /// <summary>条码1</summary>
        public string Barcode1
        {
            get => _barcode1;
            private set => SetProperty(ref _barcode1, value);
        }

        /// <summary>操作员工号</summary>
        public string OperatorId
        {
            get => _operatorId;
            private set => SetProperty(ref _operatorId, value);
        }

        /// <summary>配方名称</summary>
        public string RecipeName
        {
            get => _recipeName;
            private set => SetProperty(ref _recipeName, value);
        }

        /// <summary>原图路径</summary>
        public string ImagePath
        {
            get => _imagePath;
            private set => SetProperty(ref _imagePath, value);
        }

        /// <summary>比对结果</summary>
        public bool IsMatch
        {
            get => _isMatch;
            private set => SetProperty(ref _isMatch, value);
        }

        /// <summary>错误信息（NG 时显示）</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>是否比对失败（用于绑定 NG 区域 Visibility）</summary>
        public bool HasError => !_isMatch;

        /// <summary>原始检测数据（供 ZoomableImageViewer AnnotationContent 使用）</summary>
        public MachineDetectionData DetectionData
        {
            get => _detectionData;
            private set => SetProperty(ref _detectionData, value);
        }

        private static readonly Brush _okBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly Brush _ngBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0x33, 0x40));

        public WaferSlotDetailViewModel()
        {
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (!parameters.TryGetValue("SlotInfo", out WaferSlotInfo slot)) return;

            DisplayIndex = slot.DisplayIndex;
            StatusText   = slot.Status == WaferSlotStatus.OK ? "OK" : "NG";
            StatusBrush  = slot.Status == WaferSlotStatus.OK ? _okBrush : _ngBrush;
            Title        = $"第 {slot.DisplayIndex} 层  检测详情";

            var data = slot.DetectionData;
            if (data == null) return;

            DetectionTime = DateTime.FromOADate(data.Time).ToString("yyyy-MM-dd HH:mm:ss");
            CustomerBatch = data.CustomerBatch;
            OcrText       = data.OcrText;
            Barcode1      = data.Barcode1;
            IsMatch       = data.IsMatch;
            ErrorMessage  = data.ErrorMessage;
            OperatorId    = data.OperatorId;
            RecipeName    = data.RecipeName;
            ImagePath     = data.ImagePath;
            DetectionData = data;

            RaisePropertyChanged(nameof(HasError));
        }
    }
}
