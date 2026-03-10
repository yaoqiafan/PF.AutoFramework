using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    public class OcrRecipeManageViewModel:RegionViewModelBase
    {

        public OcrRecipeManageViewModel()
        {
            test = new OcrRecipeParamEntity();
           var parameters = new ObservableCollection<OcrRecipeParamEntity>();

            for (int i = 0; i < 100; i++)
            {
                OcrRecipeParamEntity ocrRecipeParamEntity = new OcrRecipeParamEntity() { RecipeName=$"Test{i}" };
                parameters.Add(ocrRecipeParamEntity);
            }

            Parameters = parameters;
        }

        public OcrRecipeParamEntity test { get; set; }

        public ObservableCollection<OcrRecipeParamEntity> Parameters { get; set; }


    }



    public class OcrRecipeParamEntity : BindableBase
    {
        private string _recipeName;
        [DefaultValue("Test")]
        [CategoryAttribute("基本参数")]
        [DisplayNameAttribute("配方名称")]
        [BrowsableAttribute(true)]
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }


        private int _CodeCount = 2;
        [DefaultValue(2)]
        [CategoryAttribute("基本参数")]
        [DisplayNameAttribute("条码个数")]
        [BrowsableAttribute(true)]
        public int CodeCount
        {
            get { return _CodeCount; }
            set { SetProperty(ref _CodeCount, value); }
        }

        private string _OCRRecipeName = string.Empty;
        [DefaultValue("")]
        [CategoryAttribute("基本参数")]
        [DisplayNameAttribute("关联相机程式")]
        [BrowsableAttribute(true)]
        public string OCRRecipeName
        {
            get { return _OCRRecipeName; }
            set { SetProperty(ref _OCRRecipeName, value); }
        }

        private E_WafeSize _WafeSize = E_WafeSize._12寸;
        [DefaultValue(E_WafeSize._12寸)]
        [CategoryAttribute("基本参数")]
        [DisplayNameAttribute("晶圆尺寸")]
        [BrowsableAttribute(true)]
        public E_WafeSize WafeSize
        {
            get { return _WafeSize; }
            set { SetProperty(ref _WafeSize, value); }
        }

        private double _PosX = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("坐标参数")]
        [DisplayNameAttribute("OCR相机X轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosX
        {
            get { return _PosX; }
            set { SetProperty(ref _PosX, value); }
        }

        private double _PosY = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("坐标参数")]
        [DisplayNameAttribute("OCR相机Y轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosY
        {
            get { return _PosY; }
            set { SetProperty(ref _PosY, value); }
        }

        private double _PosZ = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("坐标参数")]
        [DisplayNameAttribute("OCR相机Z轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosZ
        {
            get { return _PosZ; }
            set { SetProperty(ref _PosZ, value); }
        }

        private int _GuestStartIndex = 0;
        [DefaultValue(0)]
        [CategoryAttribute("比对参数")]
        [DisplayNameAttribute("客批比对开始索引")]
        [BrowsableAttribute(true)]
        public int GuestStartIndex
        {
            get { return _GuestStartIndex; }
            set { SetProperty(ref _GuestStartIndex, value); }
        }

        private int _GuestLength = 6;
        [DefaultValue(6)]
        [CategoryAttribute("比对参数")]
        [DisplayNameAttribute("客批比对长度")]
        [BrowsableAttribute(true)]
        public int GuestLength
        {
            get { return _GuestLength; }
            set { SetProperty(ref _GuestLength, value); }
        }

        private bool _IsOCRCodePate = true;
        [DefaultValue(true)]
        [CategoryAttribute("状态参数")]
        [DisplayNameAttribute("OCR标签是否张贴")]
        [BrowsableAttribute(true)]
        public bool IsOCRCodePate
        {
            get { return _IsOCRCodePate; }
            set { SetProperty(ref _IsOCRCodePate, value); }
        }

        private List<string> _AssociateProduct = new List<string>();
        [CategoryAttribute("关联参数")]
        [DisplayNameAttribute("关联工位配方名称列表")]
        [BrowsableAttribute(false)] // 集合类型在普通的 PropertyGrid 中通常无法直接优雅编辑，因此默认设为 false 隐藏
        public List<string> AssociateProduct
        {
            get { return _AssociateProduct; }
            set { SetProperty(ref _AssociateProduct, value); }
        }

    }

}
