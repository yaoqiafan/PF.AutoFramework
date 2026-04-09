using PF.UI.Controls;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.UI.UserControls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace PF.WorkStation.AutoOcr.UI.Models
{
    public class OcrRecipeParamEntity : BindableBase
    {
        private string _recipeName;
        [DefaultValue("Test")]
        [CategoryAttribute("A.基本参数")]
        [DisplayNameAttribute("1.配方名称")]
        [ReadOnly(true)]
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
        [Editor(typeof(OCRRecipePropertyEditor), typeof(OCRRecipePropertyEditor))]
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

        private double _1PosX = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位1OCR相机坐标参数")]
        [DisplayNameAttribute("1.X轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosX_1
        {
            get { return _1PosX; }
            set
            {
                SetProperty(ref _1PosX, value);
                PosXYZ_1 = $"({value}),({PosY_1}),({PosZ_1})";
            }
        }

        private double _1PosY = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位1OCR相机坐标参数")]
        [DisplayNameAttribute("2.Y轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosY_1
        {
            get { return _1PosY; }
            set
            {
                SetProperty(ref _1PosY, value);
                PosXYZ_1 = $"({PosX_1}),({value}),({PosZ_1})";
            }
        }

        private double _1PosZ = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位1OCR相机坐标参数")]
        [DisplayNameAttribute("3.Z轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosZ_1
        {
            get { return _1PosZ; }
            set
            {
                SetProperty(ref _1PosZ, value);
                PosXYZ_1 = $"({PosX_1}),({PosY_1}),({value})";
            }
        }



        private double _2PosX = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位2OCR相机坐标参数")]
        [DisplayNameAttribute("1.X轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosX_2
        {
            get { return _2PosX; }
            set
            {
                SetProperty(ref _2PosX, value);
                PosXYZ_2 = $"({value}),({PosY_2}),({PosZ_2})";
            }
        }

        private double _2PosY = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位2OCR相机坐标参数")]
        [DisplayNameAttribute("2.Y轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosY_2
        {
            get { return _2PosY; }
            set
            {
                SetProperty(ref _2PosY, value);
                PosXYZ_2 = $"({PosX_2}),({value}),({PosZ_2})";
            }
        }

        private double _2PosZ = 0;
        [DefaultValue(0.0)]
        [CategoryAttribute("B.工位2OCR相机坐标参数")]
        [DisplayNameAttribute("3.Z轴位置(um)")]
        [BrowsableAttribute(true)]
        public double PosZ_2
        {
            get { return _2PosZ; }
            set
            {
                SetProperty(ref _2PosZ, value);
                PosXYZ_2 = $"({PosX_2}),({PosY_2}),({value})";
            }
        }



        private string _1PosXYZ = "(0),(0),(0)";
        [DefaultValue(0.0)]
        [BrowsableAttribute(false)]
        public string PosXYZ_1
        {
            get { return _1PosXYZ; }
            set { SetProperty(ref _1PosXYZ, value); }
        }


        private string _2PosXYZ = "(0),(0),(0)";
        [DefaultValue(0.0)]
        [BrowsableAttribute(false)]
        public string PosXYZ_2
        {
            get { return _2PosXYZ; }
            set { SetProperty(ref _2PosXYZ, value); }
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

        [BrowsableAttribute(false)]
        public List<string> CameraPrograms { get; set; } = new List<string>();

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



        private int _light1Value = 0;
        [DefaultValue(0)]
        [CategoryAttribute("F.光源亮度")]
        [DisplayNameAttribute("1.红外光源亮度")]
        [BrowsableAttribute(true)]
        public int Light1Value
        {
            get { return _light1Value; }
            set { SetProperty(ref _light1Value, value); }
        }
        private int _light2Value = 0;
        [DefaultValue(0)]
        [CategoryAttribute("F.光源亮度")]
        [DisplayNameAttribute("2.白光光源亮度")]
        [BrowsableAttribute(true)]
        public int Light2Value
        {
            get { return _light2Value; }
            set { SetProperty(ref _light2Value, value); }
        }

    }



    public class OCRRecipePropertyEditor : PropertyEditorBase
    {
        // 重写对应的控件构建类，用于返回UI需要显示的控件实例
        public override FrameworkElement CreateElement(PropertyItem propertyItem)
        {
            var entity = propertyItem.Value as OcrRecipeParamEntity;
            var programs = entity?.CameraPrograms ?? new List<string>();

            return new SearchComboBox
            {
                IsEnabled = !propertyItem.IsReadOnly,
                ItemsSource = programs
            };
        }

        // 设置对应实体属性与控件关联的依赖属性
        public override DependencyProperty GetDependencyProperty()
        {
            return ListBox.SelectedItemProperty;
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
