using PF.UI.Controls;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.UI.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Models
{
    public class OcrRecipeParamEntity : BindableBase
    {
        private string _recipeName;
        [DefaultValue("Test")]
        [CategoryAttribute("A.基本参数")]
        [DisplayNameAttribute("1.配方名称")]
        [BrowsableAttribute(true)]
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }


        private int _CodeCount = 2;
        [DefaultValue(2)]
        [CategoryAttribute("A.基本参数")]
        [DisplayNameAttribute("2.条码个数")]
        [BrowsableAttribute(true)]
        public int CodeCount
        {
            get { return _CodeCount; }
            set { SetProperty(ref _CodeCount, value); }
        }

        private string _OCRRecipeName = string.Empty;
        [DefaultValue("")]
        [CategoryAttribute("A.基本参数")]
        [DisplayNameAttribute("3.关联相机程式")]
        [BrowsableAttribute(true)]
        public string OCRRecipeName
        {
            get { return _OCRRecipeName; }
            set { SetProperty(ref _OCRRecipeName, value); }
        }

        private E_WafeSize _WafeSize = E_WafeSize._12寸;
        [DefaultValue(E_WafeSize._12寸)]
        [CategoryAttribute("A.基本参数")]
        [DisplayNameAttribute("4.晶圆尺寸")]
        [BrowsableAttribute(true)]
        public E_WafeSize WafeSize
        {
            get { return _WafeSize; }
            set { SetProperty(ref _WafeSize, value); }
        }

        private double _PosX = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.坐标参数")]
        [DisplayNameAttribute("1.OCR相机X轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosX
        {
            get { return _PosX; }
            set
            {
                SetProperty(ref _PosX, value);
                PosXYZ = $"({value}),({PosY}),({PosZ})";
            }
        }

        private double _PosY = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.坐标参数")]
        [DisplayNameAttribute("2.OCR相机Y轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosY
        {
            get { return _PosY; }
            set
            {
                SetProperty(ref _PosY, value);
                PosXYZ = $"({PosX}),({value}),({PosZ})";
            }
        }

        private double _PosZ = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.坐标参数")]
        [DisplayNameAttribute("3.OCR相机Z轴位置(mm)")]
        [BrowsableAttribute(true)]
        public double PosZ
        {
            get { return _PosZ; }
            set
            {
                SetProperty(ref _PosZ, value);
                PosXYZ = $"({PosX}),({PosY}),({value})";
            }
        }



        private string _PosXYZ = "(0),(0),(0)";
        [DefaultValue(0.0)]
        [BrowsableAttribute(false)]
        public string PosXYZ
        {
            get { return _PosXYZ; }
            set { SetProperty(ref _PosXYZ, value); }
        }

        private int _GuestStartIndex = 0;
        [DefaultValue(0)]
        [CategoryAttribute("C.比对参数")]
        [DisplayNameAttribute("1.客批比对开始索引")]
        [BrowsableAttribute(true)]
        public int GuestStartIndex
        {
            get { return _GuestStartIndex; }
            set { SetProperty(ref _GuestStartIndex, value); }
        }

        private int _GuestLength = 6;
        [DefaultValue(6)]
        [CategoryAttribute("C.比对参数")]
        [DisplayNameAttribute("2.客批比对长度")]
        [BrowsableAttribute(true)]
        public int GuestLength
        {
            get { return _GuestLength; }
            set { SetProperty(ref _GuestLength, value); }
        }

        private bool _IsOCRCodePate = true;
        [DefaultValue(true)]
        [CategoryAttribute("D.状态参数")]
        [DisplayNameAttribute("1.OCR标签是否张贴")]
        [BrowsableAttribute(true)]
        public bool IsOCRCodePate
        {
            get { return _IsOCRCodePate; }
            set { SetProperty(ref _IsOCRCodePate, value); }
        }

        private List<string> _AssociateProduct = new List<string>();
        [CategoryAttribute("E.关联参数")]
        [DisplayNameAttribute("1.关联工位配方名称列表")]
        [BrowsableAttribute(true)]
        [Editor(typeof(AssociateProductPropertyEditor), typeof(AssociateProductPropertyEditor))]
        public List<string> AssociateProduct
        {
            get { return _AssociateProduct; }
            set { SetProperty(ref _AssociateProduct, value); }
        }
    }



    public class AssociateProductPropertyEditor : PropertyEditorBase
    {
        // 重写对应的控件构建类，用于返回UI需要显示的控件实例
        public override FrameworkElement CreateElement(PropertyItem propertyItem)
        {
            AssociateProductListView associateProductListView = new AssociateProductListView();
            return associateProductListView;
        }

        // 设置对应实体属性与控件关联的依赖属性
        public override DependencyProperty GetDependencyProperty()
        {
            return AssociateProductListView.AssociatesProperty;
        }
    }
}
