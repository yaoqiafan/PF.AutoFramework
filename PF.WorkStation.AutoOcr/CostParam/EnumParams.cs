using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Workstation.AutoOcr.CostParam
{
    public enum E_AxisName
    {
        视觉X轴,
        视觉Y轴,
        视觉Z轴,
        工位1上料Z轴,
        工位1挡料X轴,
        工位1拉料Y轴,
        工位2上料Z轴,
        工位2挡料X轴,
        工位2拉料Y轴,
    }

    /// <summary>
    /// 输入信号名称
    /// </summary>
    public enum E_InPutName
    {
        上晶圆左错层公共检测=0,
        上晶圆左错层12寸检测=1,
        上晶圆左错层8寸检测 = 2,
        [Browsable(false)]
        预留2 = 3,
        上晶圆右错层检测1 = 4,
        上晶圆右错层检测2 = 5,
        [Browsable(false)]
        预留3 = 6,
        [Browsable(false)]
        预留4 = 7,
        上晶圆左铁环突片检测 =8,
        上晶圆左8寸铁环防反检测 = 9,
        上晶圆左12寸铁环防反检测 = 10,
        [Browsable(false)]
        预留5 = 11,


        上晶圆左8寸料盒挡杆检测 = 12,
        上晶圆左12寸料盒挡杆检测1 = 13,
        上晶圆左12寸料盒挡杆检测2 = 14,
        [Browsable(false)]
        预留6 = 15,


        上晶圆左料盒公用到位检测 = 16,
        上晶圆左8寸料盒到位检测 = 17,
        上晶圆左12寸料盒到位检测 = 18,
        [Browsable(false)]
        预留7= 19,


        

        上晶圆右铁环铁环突片检测 = 20,
        上晶圆右8寸铁环防反检测 = 21,
        上晶圆右12寸铁环防反检测 = 22,
        [Browsable(false)]
        预留8 = 23,
        上晶圆右8寸料盒挡杆检测 = 24,
        上晶圆右12寸料盒挡杆检测1 = 25,
        上晶圆右12寸料盒挡杆检测2 = 26,
        [Browsable(false)]
        预留9 = 27,
        上晶圆右料盒公用到位 = 28,
        上晶圆右8寸料盒到位检测 = 29,
        上晶圆右12寸到料盒位检测 = 30,
        [Browsable(false)]
        预留10 = 31,
       
        晶圆夹爪左气缸张开 = 32,
        晶圆夹爪左气缸闭合 = 33,
        晶圆夹爪左铁环有无检测 = 34,
        夹爪左叠料检测 = 35,
        晶圆夹爪左卡料检测 = 36,
        晶圆夹爪左12寸气缸打开 = 37,
        晶圆夹爪左8寸气缸缩回 = 38,
        [Browsable(false)]
        预留11 = 39,
        晶圆夹爪右气缸张开 = 40,
        晶圆夹爪右气缸闭合 = 41,
        晶圆夹爪右铁环有无检测 = 42,
        夹爪右叠料检测 = 43,
        晶圆夹爪右卡料检测 = 44,
        晶圆夹爪右12寸气缸打开 = 45,
        晶圆夹爪右8寸气缸缩回 = 46,
        [Browsable(false)]
        预留12 = 47,
        晶圆轨道左调宽气缸打开 = 48,
        晶圆轨道左调宽气缸缩回 = 49,
        晶圆轨道左晶圆在位检测1 = 50,
        晶圆轨道左晶圆在位检测2 = 51,
        晶圆轨道右调宽气缸打开 = 52,
        晶圆轨道右调宽气缸缩回 = 53,
        晶圆轨道右晶圆在位检测1 = 54,
        晶圆轨道右晶圆在位检测2 = 55,
        电磁门锁1_2信号 = 56,
        电磁门锁3_4信号 = 57,
        电磁门锁5_6信号 = 58,
        电磁门锁7_8信号 = 59,
        上晶圆左启动按钮 = 60,
        上晶圆右启动按钮 = 61,
        [Browsable(false)]
        预留13 = 62,
        [Browsable(false)]
        预留14 = 63,
        [Browsable(false)]
        预留15 = 64,
        [Browsable(false)]
        预留16 = 65,
        [Browsable(false)]
        预留17 = 66,
        [Browsable(false)]
        预留18 = 67,
        [Browsable(false)]
        预留19 = 68,
        [Browsable(false)]
        预留20 = 69,
        [Browsable(false)]
        预留21 = 70,
        [Browsable(false)]
        预留22 = 71,

    }


    public enum E_OutPutName
    {
        [Browsable(false)]
        预留23 = 0,
        [Browsable(false)]
        预留24= 1,
        [Browsable(false)]
        预留25 = 2,
        [Browsable(false)]
        预留26 = 3,
        [Browsable(false)]
        预留27 = 4,
        [Browsable(false)]
        预留28 = 5,
        [Browsable(false)]
        预留29 = 6,
        [Browsable(false)]
        预留30 = 7,
        上晶圆左铁环突片检测开关 = 8,
        [Browsable(false)]
        预留2 = 9,
        [Browsable(false)]
        预留3 = 10,
        [Browsable(false)]
        预留4 = 11,
        上晶圆右铁环突片检测开关 = 12,
        [Browsable(false)]
        预留6 = 13,
        [Browsable(false)]
        预留7 = 14,
        [Browsable(false)]
        预留8 = 15,
        夹爪气缸左闭合 = 16,
        夹爪气缸左张开 = 17,
        夹爪左X轴气缸伸出 = 18,
        夹爪左X轴气缸缩回 = 19,
        晶圆轨道左调宽气缸伸出 = 20,
        晶圆轨道左调宽气缸收回 = 21,
        [Browsable(false)]
        预留9 = 22,
        [Browsable(false)]
        预留10 = 23,
        夹爪气缸右闭合 =   24,
        夹爪气缸右张开 = 25,
        夹爪右X轴气缸伸出 = 26,
        夹爪右X轴气缸缩回 = 27,
        晶圆轨道右调宽气缸伸出 = 28,
        晶圆轨道右调宽气缸收回 = 29,
        [Browsable(false)]
        预留11 = 30,
        [Browsable(false)]
        预留12 = 31,
        三色灯红 = 32,
        三色灯黄 = 33,
        三色灯绿 = 34,
        蜂鸣器 = 35,
        电磁门锁1 = 36,
        电磁门锁2 = 37,
        电磁门锁3 = 38,
        电磁门锁4 = 39,
        电磁门锁5 = 40,
        电磁门锁6 = 41,
        电磁门锁7 = 42,
        电磁门锁8 = 43,
        除静电1 = 44,
        除静电2 = 45,
        [Browsable(false)]
        预留13 = 46,
        [Browsable(false)]
        预留14 = 47,
        [Browsable(false)]
        预留15 = 48,
        [Browsable(false)]
        预留16 = 49,
        [Browsable(false)]
        预留17 = 50,
        [Browsable(false)]
        预留18 = 51,
        [Browsable(false)]
        预留19 = 52,
        [Browsable(false)]
        预留20 = 53,
        [Browsable(false)]
        预留21 = 54,
        [Browsable(false)]
        预留22 = 55,
    }


    public enum E_ScanCode
    {
        工位1扫码枪,
        工位2扫码枪,
    }

    public enum E_WorkSpace
    {
        工位1,
        工位2,
    }

    public enum E_Camera
    {
        OCR相机,
    }

    public enum E_WafeSize
    {
        _8寸,
        _12寸
    }


    public enum E_Mechanisms
    {
        工位1上晶圆模组,
        工位2上晶圆模组,
        工位1推拉晶圆模组,
        工位2推拉晶圆模组,
        OCR识别模组,
        检测数据模组,
        SECSGEM通讯模组,

    }


    public enum E_WorkStation
    {
        工位1上下料工站,
        OCR检测工站,
        工位1拉料工站,

        工位2上下料工站,
        工位2拉料工站,
    }


    /// <summary>
    /// 物料检测状态枚举
    /// </summary>
    public enum E_DetectionStatus
    {
        待检测,
        检测中,
        检测完成,
        上传NG,

    }


    public enum E_LightController
    {
        康视达_COM
    }

    #region 参数枚举

    public enum E_Params
    {
        #region 上下料模组参数

        [Category("上下料模组参数")]
        [Description("8寸晶圆层间距(um)")]
        [DefaultValue(10)]
        LayerPitch_8,



        [Category("上下料模组参数")]
        [Description("8寸晶圆同层允许最大间距")]
        [DefaultValue(10)]
        SameLayerMaximum_8,

        [Category("上下料模组参数")]
        [Description("12寸晶圆同层允许最大间距")]
        [DefaultValue(10)]
        SameLayerMaximum_12,


        [Category("上下料模组参数")]
        [Description("12寸晶圆层间距")]
        [DefaultValue(15)]
        LayerPitch_12,

        [Category("上下料模组参数")]
        [Description("8寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningPositiveOffset_8,

        [Category("上下料模组参数")]
        [Description("12寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningPositiveOffset_12,

        [Category("上下料模组参数")]
        [Description("8寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningNegativeOffset_8,

        [Category("上下料模组参数")]
        [Description("12寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningNegativeOffset_12,

        [Category("上下料模组参数")]
        [Description("扫层速度")]
        [DefaultValue(1)]
        ZScanSpeed,
        #endregion



        #region 超时参数
        [Category("超时参数")]
        [Description("轴运动超时参数")]
        [DefaultValue(5000)]
        AxisMoveTimeout,

        [Category("超时参数")]
        [Description("轴回零超时参数")]
        [DefaultValue(10000)]
        AxisHomeTimeout,



        [Category("超时参数")]
        [Description("气缸超时参数")]
        [DefaultValue(10000)]
        CylinderTimeout,

        #endregion


        #region 扫码光源参数
        [Category("扫码光源参数")]
        [Description("工位1光源亮度")]
        [DefaultValue(1)]
        WorkStation1LightBrightness,
        [Category("扫码光源参数")]
        [Description("工位2光源亮度")]
        [DefaultValue(1)]
        WorkStation2LightBrightness,
        #endregion 扫码光源参数


        #region OCR相机参数

        [Category("OCR相机参数")]
        [Description("OCR相机图片原始路径")]
        [DefaultValue("C//OCRImagePath")]
        OCRCameraImageOriginalPath,


        [Category("OCR相机参数")]
        [Description("OCR相机图片保存路径")]
        [DefaultValue("E//OCRImagePath")]
        OCRCameraImageSavePath,

        #endregion OCR相机参数


    }




    #endregion

}
