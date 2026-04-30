using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Workstation.AutoOcr.CostParam
{
    /// <summary>
    /// 轴名称枚举
    /// </summary>
    public enum E_AxisName
    {
        /// <summary>视觉X轴</summary>
        视觉X轴,
        /// <summary>视觉Y轴</summary>
        视觉Y轴,
        /// <summary>视觉Z轴</summary>
        视觉Z轴,
        /// <summary>工位1上料Z轴</summary>
        工位1上料Z轴,
        /// <summary>工位1挡料X轴</summary>
        工位1挡料X轴,
        /// <summary>工位1拉料Y轴</summary>
        工位1拉料Y轴,
        /// <summary>工位2上料Z轴</summary>
        工位2上料Z轴,
        /// <summary>工位2挡料X轴</summary>
        工位2挡料X轴,
        /// <summary>工位2拉料Y轴</summary>
        工位2拉料Y轴,
    }

    /// <summary>
    /// 输入信号名称
    /// </summary>
    public enum E_InPutName
    {
        /// <summary>上晶圆左错层公共检测</summary>
        上晶圆左错层公共检测=0,
        /// <summary>上晶圆左错层12寸检测</summary>
        上晶圆左错层12寸检测=1,
        /// <summary>上晶圆左错层8寸检测</summary>
        上晶圆左错层8寸检测 = 2,
        /// <summary>预留2</summary>
        [Browsable(false)]
        预留2 = 3,
        /// <summary>上晶圆右错层公共检测</summary>
        上晶圆右错层公共检测 = 4,
        /// <summary>上晶圆右错层12寸检测</summary>
        上晶圆右错层8寸检测 = 5,
        /// <summary>上晶圆右错层8寸检测</summary>
        上晶圆右错层12寸检测 = 6,
        /// <summary>预留4</summary>
        [Browsable(false)]
        预留4 = 7,
        /// <summary>上晶圆左铁环突片检测</summary>
        上晶圆左铁环突片检测 =8,
        /// <summary>上晶圆左8寸铁环防反检测</summary>
        上晶圆左8寸铁环防反检测 = 9,
        /// <summary>上晶圆左12寸铁环防反检测</summary>
        上晶圆左12寸铁环防反检测 = 10,
        /// <summary>预留5</summary>
        [Browsable(false)]
        预留5 = 11,


        /// <summary>上晶圆左8寸料盒挡杆检测</summary>
        上晶圆左8寸料盒挡杆检测 = 12,
        /// <summary>上晶圆左12寸料盒挡杆检测1</summary>
        上晶圆左12寸料盒挡杆检测1 = 13,
        /// <summary>上晶圆左12寸料盒挡杆检测2</summary>
        上晶圆左12寸料盒挡杆检测2 = 14,
        /// <summary>预留6</summary>
        [Browsable(false)]
        预留6 = 15,


        /// <summary>上晶圆左料盒公用到位检测</summary>
        上晶圆左料盒公用到位检测 = 16,
        /// <summary>上晶圆左8寸料盒到位检测</summary>
        上晶圆左8寸料盒到位检测 = 17,
        /// <summary>上晶圆左12寸料盒到位检测</summary>
        上晶圆左12寸料盒到位检测 = 18,
        /// <summary>预留7</summary>
        [Browsable(false)]
        预留7= 19,




        /// <summary>上晶圆右铁环突片检测</summary>
        上晶圆右铁环突片检测 = 20,
        /// <summary>上晶圆右8寸铁环防反检测</summary>
        上晶圆右8寸铁环防反检测 = 21,
        /// <summary>上晶圆右12寸铁环防反检测</summary>
        上晶圆右12寸铁环防反检测 = 22,
        /// <summary>预留8</summary>
        [Browsable(false)]
        预留8 = 23,
        /// <summary>上晶圆右8寸料盒挡杆检测</summary>
        上晶圆右8寸料盒挡杆检测 = 24,
        /// <summary>上晶圆右12寸料盒挡杆检测1</summary>
        上晶圆右12寸料盒挡杆检测1 = 25,
        /// <summary>上晶圆右12寸料盒挡杆检测2</summary>
        上晶圆右12寸料盒挡杆检测2 = 26,
        /// <summary>预留9</summary>
        [Browsable(false)]
        预留9 = 27,
        /// <summary>上晶圆右料盒公用到位</summary>
        上晶圆右料盒公用到位 = 28,
        /// <summary>上晶圆右8寸料盒到位检测</summary>
        上晶圆右8寸料盒到位检测 = 29,
        /// <summary>上晶圆右12寸到料盒位检测</summary>
        上晶圆右12寸到料盒位检测 = 30,
        /// <summary>预留10</summary>
        [Browsable(false)]
        预留10 = 31,

        /// <summary>晶圆夹爪左气缸张开</summary>
        晶圆夹爪左气缸张开 = 32,
        /// <summary>晶圆夹爪左气缸闭合</summary>
        晶圆夹爪左气缸闭合 = 33,
        /// <summary>晶圆夹爪左铁环有无检测</summary>
        晶圆夹爪左铁环有无检测 = 34,
        /// <summary>夹爪左叠料检测</summary>
        夹爪左叠料检测 = 35,
        /// <summary>晶圆夹爪左卡料检测</summary>
        晶圆夹爪左卡料检测 = 36,
        /// <summary>晶圆夹爪左12寸气缸打开</summary>
        晶圆夹爪左12寸气缸打开 = 37,
        /// <summary>晶圆夹爪左8寸气缸缩回</summary>
        晶圆夹爪左8寸气缸缩回 = 38,
        /// <summary>预留11</summary>
        [Browsable(false)]
        预留11 = 39,
        /// <summary>晶圆夹爪右气缸张开</summary>
        晶圆夹爪右气缸张开 = 40,
        /// <summary>晶圆夹爪右气缸闭合</summary>
        晶圆夹爪右气缸闭合 = 41,
        /// <summary>晶圆夹爪右铁环有无检测</summary>
        晶圆夹爪右铁环有无检测 = 42,
        /// <summary>夹爪右叠料检测</summary>
        夹爪右叠料检测 = 43,
        /// <summary>晶圆夹爪右卡料检测</summary>
        晶圆夹爪右卡料检测 = 44,
        /// <summary>晶圆夹爪右12寸气缸打开</summary>
        晶圆夹爪右12寸气缸打开 = 45,
        /// <summary>晶圆夹爪右8寸气缸缩回</summary>
        晶圆夹爪右8寸气缸缩回 = 46,
        /// <summary>预留12</summary>
        [Browsable(false)]
        预留12 = 47,
        /// <summary>晶圆轨道左调宽气缸打开</summary>
        晶圆轨道左调宽气缸打开 = 48,
        /// <summary>晶圆轨道左调宽气缸缩回</summary>
        晶圆轨道左调宽气缸缩回 = 49,
        /// <summary>晶圆轨道左晶圆在位检测1</summary>
        晶圆轨道左晶圆在位检测1 = 50,
        /// <summary>晶圆轨道左晶圆在位检测2</summary>
        晶圆轨道左晶圆在位检测2 = 51,
        /// <summary>晶圆轨道右调宽气缸打开</summary>
        晶圆轨道右调宽气缸打开 = 52,
        /// <summary>晶圆轨道右调宽气缸缩回</summary>
        晶圆轨道右调宽气缸缩回 = 53,
        /// <summary>晶圆轨道右晶圆在位检测1</summary>
        晶圆轨道右晶圆在位检测1 = 54,
        /// <summary>晶圆轨道右晶圆在位检测2</summary>
        晶圆轨道右晶圆在位检测2 = 55,
        /// <summary>电磁门锁1_2信号</summary>
        电磁门锁1_2信号 = 56,
        /// <summary>电磁门锁3_4信号</summary>
        电磁门锁3_4信号 = 57,
        /// <summary>电磁门锁5_6信号</summary>
        电磁门锁5_6信号 = 58,
        /// <summary>电磁门锁7_8信号</summary>
        电磁门锁7_8信号 = 59,
        /// <summary>上晶圆左启动按钮</summary>
        上晶圆左启动按钮 = 60,
        /// <summary>上晶圆右启动按钮</summary>
        上晶圆右启动按钮 = 61,
        /// <summary>预留13</summary>
        [Browsable(false)]
        预留13 = 62,
        /// <summary>预留14</summary>
        [Browsable(false)]
        预留14 = 63,
        /// <summary>预留15</summary>
        [Browsable(false)]
        预留15 = 64,
        /// <summary>预留16</summary>
        [Browsable(false)]
        预留16 = 65,
        /// <summary>预留17</summary>
        [Browsable(false)]
        预留17 = 66,
        /// <summary>预留18</summary>
        [Browsable(false)]
        预留18 = 67,
        /// <summary>预留19</summary>
        [Browsable(false)]
        预留19 = 68,
        /// <summary>预留20</summary>
        [Browsable(false)]
        预留20 = 69,
        /// <summary>预留21</summary>
        [Browsable(false)]
        预留21 = 70,
        /// <summary>预留22</summary>
        [Browsable(false)]
        预留22 = 71,

    }


    /// <summary>
    /// 输出信号名称枚举
    /// </summary>
    public enum E_OutPutName
    {
        /// <summary>预留23</summary>
        [Browsable(false)]
        预留23 = 0,
        /// <summary>预留24</summary>
        [Browsable(false)]
        预留24= 1,
        /// <summary>预留25</summary>
        [Browsable(false)]
        预留25 = 2,
        /// <summary>预留26</summary>
        [Browsable(false)]
        预留26 = 3,
        /// <summary>预留27</summary>
        [Browsable(false)]
        预留27 = 4,
        /// <summary>预留28</summary>
        [Browsable(false)]
        预留28 = 5,
        /// <summary>预留29</summary>
        [Browsable(false)]
        预留29 = 6,
        /// <summary>预留30</summary>
        [Browsable(false)]
        预留30 = 7,
        /// <summary>上晶圆左铁环突片检测开关</summary>
        上晶圆左铁环突片检测开关 = 8,
        /// <summary>预留2</summary>
        [Browsable(false)]
        预留2 = 9,
        /// <summary>预留3</summary>
        [Browsable(false)]
        预留3 = 10,
        /// <summary>预留4</summary>
        [Browsable(false)]
        预留4 = 11,
        /// <summary>上晶圆右铁环突片检测开关</summary>
        上晶圆右铁环突片检测开关 = 12,
        /// <summary>预留6</summary>
        [Browsable(false)]
        预留6 = 13,
        /// <summary>预留7</summary>
        [Browsable(false)]
        预留7 = 14,
        /// <summary>预留8</summary>
        [Browsable(false)]
        预留8 = 15,
        /// <summary>夹爪气缸左闭合</summary>
        夹爪气缸左闭合 = 16,
        /// <summary>夹爪气缸左张开</summary>
        夹爪气缸左张开 = 17,
        /// <summary>夹爪左X轴气缸伸出</summary>
        夹爪左X轴气缸伸出 = 18,
        /// <summary>夹爪左X轴气缸缩回</summary>
        夹爪左X轴气缸缩回 = 19,
        /// <summary>晶圆轨道左调宽气缸伸出</summary>
        晶圆轨道左调宽气缸伸出 = 20,
        /// <summary>晶圆轨道左调宽气缸收回</summary>
        晶圆轨道左调宽气缸收回 = 21,
        /// <summary>预留9</summary>
        [Browsable(false)]
        预留9 = 22,
        /// <summary>预留10</summary>
        [Browsable(false)]
        预留10 = 23,
        /// <summary>夹爪气缸右闭合</summary>
        夹爪气缸右闭合 =   24,
        /// <summary>夹爪气缸右张开</summary>
        夹爪气缸右张开 = 25,
        /// <summary>夹爪右X轴气缸伸出</summary>
        夹爪右X轴气缸伸出 = 26,
        /// <summary>夹爪右X轴气缸缩回</summary>
        夹爪右X轴气缸缩回 = 27,
        /// <summary>晶圆轨道右调宽气缸伸出</summary>
        晶圆轨道右调宽气缸伸出 = 28,
        /// <summary>晶圆轨道右调宽气缸收回</summary>
        晶圆轨道右调宽气缸收回 = 29,
        /// <summary>预留11</summary>
        [Browsable(false)]
        预留11 = 30,
        /// <summary>预留12</summary>
        [Browsable(false)]
        预留12 = 31,
        /// <summary>三色灯红</summary>
        三色灯红 = 32,
        /// <summary>三色灯黄</summary>
        三色灯黄 = 33,
        /// <summary>三色灯绿</summary>
        三色灯绿 = 34,
        /// <summary>蜂鸣器</summary>
        蜂鸣器 = 35,
        /// <summary>电磁门锁1</summary>
        电磁门锁1 = 36,
        /// <summary>电磁门锁2</summary>
        电磁门锁2 = 37,
        /// <summary>电磁门锁3</summary>
        电磁门锁3 = 38,
        /// <summary>电磁门锁4</summary>
        电磁门锁4 = 39,
        /// <summary>电磁门锁5</summary>
        电磁门锁5 = 40,
        /// <summary>电磁门锁6</summary>
        电磁门锁6 = 41,
        /// <summary>电磁门锁7</summary>
        电磁门锁7 = 42,
        /// <summary>电磁门锁8</summary>
        电磁门锁8 = 43,
        /// <summary>除静电1</summary>
        除静电1 = 44,
        /// <summary>除静电2</summary>
        除静电2 = 45,
        /// <summary>预留13</summary>
        [Browsable(false)]
        预留13 = 46,
        /// <summary>预留14</summary>
        [Browsable(false)]
        预留14 = 47,
        /// <summary>预留15</summary>
        [Browsable(false)]
        预留15 = 48,
        /// <summary>预留16</summary>
        [Browsable(false)]
        预留16 = 49,
        /// <summary>预留17</summary>
        [Browsable(false)]
        预留17 = 50,
        /// <summary>预留18</summary>
        [Browsable(false)]
        预留18 = 51,
        /// <summary>预留19</summary>
        [Browsable(false)]
        预留19 = 52,
        /// <summary>预留20</summary>
        [Browsable(false)]
        预留20 = 53,
        /// <summary>预留21</summary>
        [Browsable(false)]
        预留21 = 54,
        /// <summary>预留22</summary>
        [Browsable(false)]
        预留22 = 55,
    }


    /// <summary>
    /// 扫码枪枚举
    /// </summary>
    public enum E_ScanCode
    {
        /// <summary>工位1扫码枪</summary>
        工位1扫码枪,
        /// <summary>工位2扫码枪</summary>
        工位2扫码枪,
    }

    /// <summary>
    /// 工位枚举
    /// </summary>
    public enum E_WorkSpace
    {
        /// <summary>工位1</summary>
        工位1,
        /// <summary>工位2</summary>
        工位2,
    }

    /// <summary>
    /// 相机枚举
    /// </summary>
    public enum E_Camera
    {
        /// <summary>OCR相机</summary>
        OCR相机,
    }

    /// <summary>
    /// 晶圆尺寸枚举
    /// </summary>
    public enum E_WafeSize
    {
        /// <summary>8寸</summary>
        _8寸,
        /// <summary>12寸</summary>
        _12寸
    }


    /// <summary>
    /// 模组枚举
    /// </summary>
    public enum E_Mechanisms
    {
        /// <summary>工位1上晶圆模组</summary>
        工位1上晶圆模组,
        /// <summary>工位2上晶圆模组</summary>
        工位2上晶圆模组,
        /// <summary>工位1推拉晶圆模组</summary>
        工位1推拉晶圆模组,
        /// <summary>工位2推拉晶圆模组</summary>
        工位2推拉晶圆模组,
        /// <summary>OCR识别模组</summary>
        OCR识别模组,
        /// <summary>检测数据模组</summary>
        检测数据模组,
        /// <summary>SECSGEM通讯模组</summary>
        SECSGEM通讯模组,

    }


    /// <summary>
    /// 工站枚举
    /// </summary>
    public enum E_WorkStation
    {
        /// <summary>工位1上下料工站</summary>
        工位1上下料工站,
        /// <summary>OCR检测工站</summary>
        OCR检测工站,
        /// <summary>工位1拉料工站</summary>
        工位1拉料工站,

        /// <summary>工位2上下料工站</summary>
        工位2上下料工站,
        /// <summary>工位2拉料工站</summary>
        工位2拉料工站,

    }


    /// <summary>
    /// 物料检测状态枚举
    /// </summary>
    public enum E_DetectionStatus
    {
        /// <summary>待检测</summary>
        待检测,
        /// <summary>检测中</summary>
        检测中,
        /// <summary>检测完成</summary>
        检测完成,
        /// <summary>上传NG</summary>
        上传NG,

    }


    /// <summary>
    /// 光源控制器枚举
    /// </summary>
    public enum E_LightController
    {
        /// <summary>康视达COM</summary>
        康视达_COM
    }

    #region 参数枚举

    /// <summary>
    /// 参数枚举
    /// </summary>
    public enum E_Params
    {
        #region 上下料模组参数

        /// <summary>8寸晶圆层间距(um)</summary>
        [Category("上下料模组参数")]
        [Description("8寸晶圆层间距(um)")]
        [DefaultValue(10)]
        LayerPitch_8,



        /// <summary>8寸晶圆同层允许最大间距</summary>
        [Category("上下料模组参数")]
        [Description("8寸晶圆同层允许最大间距")]
        [DefaultValue(10)]
        SameLayerMaximum_8,

        /// <summary>12寸晶圆同层允许最大间距</summary>
        [Category("上下料模组参数")]
        [Description("12寸晶圆同层允许最大间距")]
        [DefaultValue(10)]
        SameLayerMaximum_12,


        /// <summary>12寸晶圆层间距</summary>
        [Category("上下料模组参数")]
        [Description("12寸晶圆层间距")]
        [DefaultValue(15)]
        LayerPitch_12,

        /// <summary>8寸晶圆扫描正偏移</summary>
        [Category("上下料模组参数")]
        [Description("8寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningPositiveOffset_8,

        /// <summary>12寸晶圆扫描正偏移</summary>
        [Category("上下料模组参数")]
        [Description("12寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningPositiveOffset_12,

        /// <summary>8寸晶圆扫描负偏移</summary>
        [Category("上下料模组参数")]
        [Description("8寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningNegativeOffset_8,

        /// <summary>12寸晶圆扫描负偏移</summary>
        [Category("上下料模组参数")]
        [Description("12寸晶圆扫描正偏移")]
        [DefaultValue(0.0)]
        WaferScanningNegativeOffset_12,

        /// <summary>扫层速度</summary>
        [Category("上下料模组参数")]
        [Description("扫层速度")]
        [DefaultValue(1)]
        ZScanSpeed,
        #endregion



        #region 超时参数
        /// <summary>轴运动超时参数</summary>
        [Category("超时参数")]
        [Description("轴运动超时参数")]
        [DefaultValue(5000)]
        AxisMoveTimeout,

        /// <summary>轴回零超时参数</summary>
        [Category("超时参数")]
        [Description("轴回零超时参数")]
        [DefaultValue(10000)]
        AxisHomeTimeout,



        /// <summary>气缸超时参数</summary>
        [Category("超时参数")]
        [Description("气缸超时参数")]
        [DefaultValue(10000)]
        CylinderTimeout,

        #endregion


        #region 扫码光源参数
        /// <summary>工位1光源亮度</summary>
        [Category("扫码光源参数")]
        [Description("工位1光源亮度")]
        [DefaultValue(1)]
        WorkStation1LightBrightness,
        /// <summary>工位2光源亮度</summary>
        [Category("扫码光源参数")]
        [Description("工位2光源亮度")]
        [DefaultValue(1)]
        WorkStation2LightBrightness,
        #endregion 扫码光源参数


        #region OCR相机参数

        /// <summary>OCR相机图片原始路径</summary>
        [Category("OCR相机参数")]
        [Description("OCR相机图片原始路径")]
        [DefaultValue("C//OCRImagePath")]
        OCRCameraImageOriginalPath,


        /// <summary>OCR相机图片保存路径</summary>
        [Category("OCR相机参数")]
        [Description("OCR相机图片保存路径")]
        [DefaultValue("E//OCRImagePath")]
        OCRCameraImageSavePath,


        /// <summary>工位X方向间距_8寸</summary>
        [Category("OCR相机参数")]
        [Description("工位X方向间距_8寸")]
        [DefaultValue(645.0)]
        OCRStationDistance_8,


        /// <summary>工位X方向间距_12寸</summary>
        [Category("OCR相机参数")]
        [Description("工位X方向间距_12寸")]
        [DefaultValue(540.0)]
        OCRStationDistance_12,
        #endregion OCR相机参数


        #region 安全门参数

        /// <summary>屏蔽安全门1-2检测（调试用，正式生产必须关闭）</summary>
        [Category("屏蔽参数")]
        [Description("屏蔽安全门1-2检测")]
        [DefaultValue(false)]
        SafeDoor_1_2_Muted,

        /// <summary>屏蔽安全门3-4检测（调试用，正式生产必须关闭）</summary>
        [Category("屏蔽参数")]
        [Description("屏蔽安全门3-4检测")]
        [DefaultValue(false)]
        SafeDoor_3_4_Muted,

        /// <summary>屏蔽安全门5-6检测（调试用，正式生产必须关闭）</summary>
        [Category("屏蔽参数")]
        [Description("屏蔽安全门5-6检测")]
        [DefaultValue(false)]
        SafeDoor_5_6_Muted,

        /// <summary>屏蔽安全门7-8检测（调试用，正式生产必须关闭）</summary>
        [Category("屏蔽参数")]
        [Description("屏蔽安全门7-8检测")]
        [DefaultValue(false)]
        SafeDoor_7_8_Muted,

        #endregion

        #region 三色灯参数

        /// <summary>全局蜂鸣器屏蔽（调试模式静音用，正式生产必须关闭）</summary>
        [Category("屏蔽参数")]
        [Description("全局蜂鸣器屏蔽")]
        [DefaultValue(false)]
        BuzzerMuted,

        #endregion



        #region 加载离线配方参数
        /// <summary>
        /// 工位1默认加载参数
        /// </summary>
        [Category("离线配方参数")]
        [Description("工位1默认加载参数")]
        [DefaultValue("New_Recipe_100349")]
        Station1Recipe,
        /// <summary>
        /// 工位2默认加载参数
        /// </summary>
        [Category("离线配方参数")]
        [Description("工位2默认加载参数")]
        [DefaultValue("New_Recipe_100349")]
        Station2Recipe,

        #endregion 加载配方参数

    }




    #endregion

}
