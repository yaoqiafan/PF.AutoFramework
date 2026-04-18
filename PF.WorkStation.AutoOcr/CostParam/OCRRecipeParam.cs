using PF.Core.Interfaces.Recipe;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.CostParam
{
    /// <summary>
    /// OCR配方参数
    /// </summary>
    public class OCRRecipeParam : RecipeParamBase
    {
        /// <summary>
        /// 条码个数
        /// </summary>
        [Range(0, 3, ErrorMessage = "条码个数设置错误")]
        public int CodeCount { get; set; } = 2;


        /// <summary>
        /// 关联OCR相机配方名称
        /// </summary>
        [Required(ErrorMessage = "相机程序号不能为空")]
        public string OCRRecipeName { get; set; } = string.Empty;

        /// <summary>
        /// 晶圆尺寸
        /// </summary>
        public E_WafeSize WafeSize { get; set; } = E_WafeSize._12寸;

        /// <summary>
        /// 工位1OCR相机X轴位置，单位mm
        /// </summary>
        public double _1PosX { get; set; } = 0;

        /// <summary>
        /// 工位1OCR相机X轴位置，单位mm
        /// </summary>
        public double _1PosY { get; set; } = 0;

        /// <summary>
        /// 工位1OCR相机Z轴位置，单位mm
        /// </summary>
        public double _1PosZ { get; set; } = 0;


        /// <summary>
        /// 工位2OCR相机X轴位置，单位mm
        /// </summary>
        public double _2PosX { get; set; } = 0;

        /// <summary>
        /// 工位2OCR相机Y轴位置，单位mm
        /// </summary>
        public double _2PosY { get; set; } = 0;

        /// <summary>
        /// 工位2OCR相机Z轴位置，单位mm
        /// </summary>
        public double _2PosZ { get; set; } = 0;



        /// <summary>
        /// 客批比对开始索引
        /// </summary>
        [Range(0, 100, ErrorMessage = "客批比对开始索引设置错误")]
        public int GuestStartIndex { get; set; } = 0;



        /// <summary>
        /// 客批比对长度
        /// </summary>
        [Range(0, 100, ErrorMessage = "客批比对长度设置错误")]
        public int GuestLength { get; set; } = 6;



        /// <summary>
        /// OCR标签是否张贴
        /// </summary>
        public bool IsOCRCodePate { get; set; } = true;

        /// <summary>
        /// 关联工位配方名称列表（如有多个工位关联同一OCR配方，则在此列表中添加对应工位配方名称）
        /// </summary>
        public List<string> AssociateProduct { get; set; } = new List<string>();



        /// <summary>
        /// 光源通道1光源亮度(红外光)
        /// </summary>
        public int LightChanel1Value { get; set; } = 0;

        /// <summary>
        /// 光源通道2光源亮度(环光)
        /// </summary>
        public int LightChanel2Value { get; set; } = 0;

        /// <summary>深克隆当前配方参数</summary>
        public override OCRRecipeParam DeepClone()
        {
            // 先拷贝父类属性，再拷贝子类属性
            var clone = new OCRRecipeParam();
            clone.RecipeName = this.RecipeName;
            clone.CreateTime = this.CreateTime;
            clone.UpdateTime = this.UpdateTime;
            clone.CodeCount = this.CodeCount;
            clone.OCRRecipeName = this.OCRRecipeName;
            clone.WafeSize = this.WafeSize;
            clone._1PosX = this._1PosX;
            clone._1PosY = this._1PosY;
            clone._1PosZ = this._1PosZ;
            clone._2PosX = this._2PosX;
            clone._2PosY = this._2PosY;
            clone._2PosZ = this._2PosZ;
            clone.GuestStartIndex = this.GuestStartIndex;
            clone.GuestLength = this.GuestLength;
            clone.IsOCRCodePate = this.IsOCRCodePate;
            clone.AssociateProduct = new List<string>(this.AssociateProduct);
            clone.LightChanel1Value = this.LightChanel1Value;
            clone.LightChanel2Value = this.LightChanel2Value;
            return clone;
        }



    }
}
