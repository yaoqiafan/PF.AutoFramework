using PF.Core.Interfaces.Recipe;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.CostParam
{
    public class OCRRecipeParam : ReceipeParamBase
    {
        /// <summary>
        /// 条码个数
        /// </summary>
        public int CodeCount { get; set; } = 2;


        /// <summary>
        /// 关联OCR相机配方名称
        /// </summary>
        public string OCRRecipeName { get; set; } = string.Empty;

        /// <summary>
        /// 晶圆尺寸
        /// </summary>
        public E_WafeSize WafeSize { get; set; } = E_WafeSize._12寸;

        /// <summary>
        /// OCR相机X轴位置，单位mm
        /// </summary>
        public double PosX { get; set; } = 0;

        /// <summary>
        /// OCR相机X轴位置，单位mm
        /// </summary>
        public double PosY { get; set; } = 0;

        /// <summary>
        /// OCR相机Z轴位置，单位mm
        /// </summary>
        public double PosZ { get; set; } = 0;

        /// <summary>
        /// 客批比对开始索引
        /// </summary>
        public int GuestStartIndex { get; set; } = 0;



        /// <summary>
        /// 客批比对长度
        /// </summary>
        public int GuestLength { get; set; } = 6;



        /// <summary>
        /// OCR标签是否张贴
        /// </summary>
        public bool IsOCRCodePate { get; set; } = true;

        /// <summary>
        /// 关联工位配方名称列表（如有多个工位关联同一OCR配方，则在此列表中添加对应工位配方名称）
        /// </summary>
        public List<string> AssociateProduct { get; set; } = new List<string>();

    }
}
