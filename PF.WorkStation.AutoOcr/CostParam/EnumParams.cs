using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        上晶圆左铁环突片检测 = 0,
        上晶圆左8寸铁环防反检测 = 1,
        上晶圆左12寸铁环防反检测 = 2,
        [Browsable(false)]
        预留1 = 3,
        上晶圆左8寸料盒挡杆检测 = 4,
        上晶圆左12寸料盒挡杆检测1 = 5,
        上晶圆左12寸料盒挡杆检测2 = 6,
        [Browsable(false)]
        预留2 = 7,
        上晶圆左料盒公用到位检测 = 8,
        上晶圆左8寸料盒到位检测 = 9,
        上晶圆左12寸到料盒位检测 = 10,
        [Browsable(false)]
        预留3 = 11,
        上晶圆左错层共用检测 = 12,
        上晶圆公用错层检测 = 13,
        上晶圆左启动按钮 = 14,
        [Browsable(false)]
        预留4 = 15,

        上晶圆右铁环铁环突片检测 = 16,
        上晶圆右8寸铁环防反检测 = 17,
        上晶圆右12寸铁环防反检测 = 18,
        [Browsable(false)]
        预留5 = 19,
        上晶圆右8寸料盒挡杆检测 = 20,
        上晶圆右12寸料盒挡杆检测1 = 21,
        上晶圆右12寸料盒挡杆检测2 = 22,
        [Browsable(false)]
        预留6 = 23,
        上晶圆右料盒公用到位 = 24,
        上晶圆右8寸料盒到位检测 = 25,
        上晶圆右12寸到料盒位检测 = 26,
        [Browsable(false)]
        预留7 = 27,
        上晶圆右错层共用检测 = 28,
        上晶圆右公用错层检测 = 29,
        上晶圆右启动按钮 = 30,
        [Browsable(false)]
        预留8 = 31,
        晶圆夹爪左气缸张开 = 32,
        晶圆夹爪左气缸闭合 = 33,
        晶圆夹爪左铁环有无检测 = 34,
        夹爪左叠料检测 = 35,
        晶圆夹爪左卡料检测 = 36,
        晶圆夹爪左12寸气缸打开 = 37,
        晶圆夹爪左8寸气缸缩回 = 38,
        [Browsable(false)]
        预留9 = 39,
        晶圆夹爪右气缸张开 = 40,
        晶圆夹爪右气缸闭合 = 41,
        晶圆夹爪右铁环有无检测 = 42,
        夹爪右叠料检测 = 43,
        晶圆夹爪右卡料检测 = 44,
        晶圆夹爪右12寸气缸打开 = 45,
        晶圆夹爪右8寸气缸缩回 = 46,
        [Browsable(false)]
        预留10 = 47,
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
        [Browsable(false)]
        预留11 = 60,
        [Browsable(false)]
        预留12 = 61,
        [Browsable(false)]
        预留13 = 62,
        [Browsable(false)]
        预留14 = 63,

    }


    public enum E_OutPutName
    {

        上晶圆左铁环突片检测开关 = 0,
        [Browsable(false)]
        预留2 = 1,
        [Browsable(false)]
        预留3 = 2,
        [Browsable(false)]
        预留4 = 3,
        上晶圆右铁环突片检测开关 = 4,
        [Browsable(false)]
        预留6 = 5,
        [Browsable(false)]
        预留7 = 6,
        [Browsable(false)]
        预留8 = 7,
        夹爪气缸左闭合 = 8,
        夹爪气缸左张开 = 9,
        夹爪左X轴气缸伸出 = 10,
        夹爪左X轴气缸缩回 = 11,
        晶圆轨道左调宽气缸伸出 = 12,
        晶圆轨道左调宽气缸收回 = 13,
        [Browsable(false)]
        预留9 = 14,
        [Browsable(false)]
        预留10 = 15,
        夹爪气缸右闭合 = 16,
        夹爪气缸右张开 = 17,
        夹爪右X轴气缸伸出 = 18,
        夹爪右X轴气缸缩回 = 19,
        晶圆轨道右调宽气缸伸出 = 20,
        晶圆轨道右调宽气缸收回 = 21,
        [Browsable(false)]
        预留11 = 22,
        [Browsable(false)]
        预留12 = 23,
        三色灯红 = 24,
        三色灯黄 = 25,
        三色灯绿 = 26,
        蜂鸣器 = 27,
        电磁门锁1 = 28,
        电磁门锁2 = 29,
        电磁门锁3 = 30,
        电磁门锁4 = 31,
        电磁门锁5 = 32,
        电磁门锁6 = 33,
        电磁门锁7 = 34,
        电磁门锁8 = 35,
        除静电1 = 36,
        除静电2 = 37,
        [Browsable(false)]
        预留13 = 38,
        [Browsable(false)]
        预留14 = 39,
        [Browsable(false)]
        预留15 = 40,
        [Browsable(false)]
        预留16 = 41,
        [Browsable(false)]
        预留17 = 42,
        [Browsable(false)]
        预留18 = 43,
        [Browsable(false)]
        预留19 = 44,
        [Browsable(false)]
        预留20 = 45,
        [Browsable(false)]
        预留21 = 46,
        [Browsable(false)]
        预留22 = 47,
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
        OCR识别模组

    }


    public enum E_TimeOut
    {
        电机回零超时,
    }



}
