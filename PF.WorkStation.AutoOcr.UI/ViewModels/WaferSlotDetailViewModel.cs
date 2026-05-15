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
        private int    _displayIndex;
        private string _statusText    = string.Empty;
        private Brush  _statusBrush   = Brushes.Gray;
        private string _detectionTime = string.Empty;
        private string _internalBatchId = string.Empty;
        private string _customerBatch   = string.Empty;
        private string _waferId         = string.Empty;
        private string _productModel    = string.Empty;
        private string _operatorId      = string.Empty;
        private string _recipeName      = string.Empty;
        private string _ocrText         = string.Empty;
        private string _barcode1        = string.Empty;
        private string _barcode2        = string.Empty;
        private string _barcode3        = string.Empty;
        private bool   _isMatch;
        private string _errorMessage    = string.Empty;
        private bool   _hasBarcode2;
        private bool   _hasBarcode3;
        private bool   _hasError;
        private string _imagePath       = string.Empty;
        private MachineDetectionData _detectionData;

        private static readonly Brush _okBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly Brush _ngBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0x33, 0x40));

        // ── 槽位基础 ─────────────────────────────────────────────────

        public int DisplayIndex
        {
            get => _displayIndex;
            private set => SetProperty(ref _displayIndex, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // ── 时间 / 批次 / 溯源 ──────────────────────────────────────

        public string DetectionTime
        {
            get => _detectionTime;
            private set => SetProperty(ref _detectionTime, value);
        }

        public string InternalBatchId
        {
            get => _internalBatchId;
            private set => SetProperty(ref _internalBatchId, value);
        }

        public string CustomerBatch
        {
            get => _customerBatch;
            private set => SetProperty(ref _customerBatch, value);
        }

        public string WaferId
        {
            get => _waferId;
            private set => SetProperty(ref _waferId, value);
        }

        public string ProductModel
        {
            get => _productModel;
            private set => SetProperty(ref _productModel, value);
        }

        public string OperatorId
        {
            get => _operatorId;
            private set => SetProperty(ref _operatorId, value);
        }

        public string RecipeName
        {
            get => _recipeName;
            private set => SetProperty(ref _recipeName, value);
        }

        // ── 检测结果 ─────────────────────────────────────────────────

        public string OcrText
        {
            get => _ocrText;
            private set => SetProperty(ref _ocrText, value);
        }

        public string Barcode1
        {
            get => _barcode1;
            private set => SetProperty(ref _barcode1, value);
        }

        public string Barcode2
        {
            get => _barcode2;
            private set => SetProperty(ref _barcode2, value);
        }

        public string Barcode3
        {
            get => _barcode3;
            private set => SetProperty(ref _barcode3, value);
        }

        public bool IsMatch
        {
            get => _isMatch;
            private set => SetProperty(ref _isMatch, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        // ── 可见性控制 ───────────────────────────────────────────────

        /// <summary>条码2 有值时显示对应行</summary>
        public bool HasBarcode2
        {
            get => _hasBarcode2;
            private set => SetProperty(ref _hasBarcode2, value);
        }

        /// <summary>条码3 有值时显示对应行</summary>
        public bool HasBarcode3
        {
            get => _hasBarcode3;
            private set => SetProperty(ref _hasBarcode3, value);
        }

        /// <summary>NG 且有有效错误信息时显示错误行</summary>
        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        // ── 图片 ─────────────────────────────────────────────────────

        public string ImagePath
        {
            get => _imagePath;
            private set => SetProperty(ref _imagePath, value);
        }

        public MachineDetectionData DetectionData
        {
            get => _detectionData;
            private set => SetProperty(ref _detectionData, value);
        }

        // ── 构造 / 对话框生命周期 ─────────────────────────────────────

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

            var d = slot.DetectionData;
            if (d == null) return;

            DetectionTime   = DateTime.FromOADate(d.Time).ToString("yyyy-MM-dd HH:mm:ss");
            InternalBatchId = d.InternalBatchId;
            CustomerBatch   = d.CustomerBatch;
            WaferId         = d.WaferId;
            ProductModel    = d.ProductModel;
            OperatorId      = d.OperatorId;
            RecipeName      = d.RecipeName;
            OcrText         = d.OcrText;
            Barcode1        = d.Barcode1;
            Barcode2        = d.Barcode2;
            Barcode3        = d.Barcode3;
            IsMatch         = d.IsMatch;
            ImagePath       = d.ImagePath;
            DetectionData   = d;

            HasBarcode2 = !string.IsNullOrEmpty(d.Barcode2);
            HasBarcode3 = !string.IsNullOrEmpty(d.Barcode3);

            var rawError = d.ErrorMessage;
            var hasValidError = !d.IsMatch
                && !string.IsNullOrEmpty(rawError)
                && rawError != "NONE";
            ErrorMessage = hasValidError ? rawError : string.Empty;
            HasError     = hasValidError;
        }
    }
}
