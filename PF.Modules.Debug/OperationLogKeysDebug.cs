using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.Debug
{
    /// <summary>
    /// PF.Modules.Debug 界面的操作日志键目录。内层嵌套类名 = 页面名（对应 NavigationConstants.Views.XXX
    /// 或 nameof(View)，挂在对应 View 根节点的 OperationLog.PageName 上），const 字段 = Key，
    /// 字段上的 DescriptionAttribute = 默认描述，OperationLogCriticalAttribute 标记默认启用记录的关键操作。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysDebug
    {
        /// <summary>轴调试视图</summary>
        public static class AxisDebugView
        {
            [Description("打开轴参数配置")]
            public const string ShowAxisParamDialog = nameof(ShowAxisParamDialog);
            [Description("连接硬件"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("伺服使能"), OperationLogCritical]
            public const string Enable = nameof(Enable);
            [Description("断开使能"), OperationLogCritical]
            public const string Disable = nameof(Disable);
            [Description("执行回零"), OperationLogCritical]
            public const string Home = nameof(Home);
            [Description("急停"), OperationLogCritical]
            public const string Stop = nameof(Stop);
            [Description("报警复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟硬件报警"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("绝对目标位置")]
            public const string TargetPosition = nameof(TargetPosition);
            [Description("绝对运动速度")]
            public const string AbsVelocity = nameof(AbsVelocity);
            [Description("执行绝对运动"), OperationLogCritical]
            public const string MoveAbsolute = nameof(MoveAbsolute);
            [Description("相对运动距离")]
            public const string RelativeDistance = nameof(RelativeDistance);
            [Description("相对运动速度")]
            public const string RelVelocity = nameof(RelVelocity);
            [Description("执行相对运动"), OperationLogCritical]
            public const string MoveRelative = nameof(MoveRelative);
            [Description("点动速度")]
            public const string JogVelocity = nameof(JogVelocity);
            [Description("负向点动"), OperationLogCritical]
            public const string JogNegative = nameof(JogNegative);
            [Description("正向点动"), OperationLogCritical]
            public const string JogPositive = nameof(JogPositive);
            [Description("点表-序号")]
            public const string PointSortOrder = nameof(PointSortOrder);
            [Description("点表-点位名称")]
            public const string PointName = nameof(PointName);
            [Description("点表-目标位置"), OperationLogCritical]
            public const string PointTargetPosition = nameof(PointTargetPosition);
            [Description("点表-速度"), OperationLogCritical]
            public const string PointSpeed = nameof(PointSpeed);
            [Description("点表-加速度"), OperationLogCritical]
            public const string PointAcc = nameof(PointAcc);
            [Description("点表-减速度"), OperationLogCritical]
            public const string PointDec = nameof(PointDec);
            [Description("点表-S段时间"), OperationLogCritical]
            public const string PointSTime = nameof(PointSTime);
            [Description("点表-备注说明")]
            public const string PointDescription = nameof(PointDescription);
            [Description("点表-移动到该点"), OperationLogCritical]
            public const string GoToPoint = nameof(GoToPoint);
            [Description("点表-获取当前位置新增点位"), OperationLogCritical]
            public const string AddPoint = nameof(AddPoint);
            [Description("点表-删除选中点位"), OperationLogCritical]
            public const string DeletePoint = nameof(DeletePoint);
            [Description("点表-保存到本地"), OperationLogCritical]
            public const string SavePoints = nameof(SavePoints);
        }

        /// <summary>IO 调试视图</summary>
        public static class IODebugView
        {
            [Description("连接硬件"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("模块复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("强制切换输出通道状态"), OperationLogCritical]
            public const string ToggleOutput = nameof(ToggleOutput);
        }

        /// <summary>扫码枪调试视图</summary>
        public static class BarcodeScanDebugView
        {
            [Description("连接硬件"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("报警复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("用户参数集输入")]
            public const string UserInfoInput = nameof(UserInfoInput);
            [Description("应用用户参数修改"), OperationLogCritical]
            public const string ChangeUserParam = nameof(ChangeUserParam);
            [Description("触发扫码"), OperationLogCritical]
            public const string TriggerScan = nameof(TriggerScan);
            [Description("清空读取历史"), OperationLogCritical]
            public const string ClearHistory = nameof(ClearHistory);
        }

        /// <summary>智能相机调试视图</summary>
        public static class CameraDebugView
        {
            [Description("连接硬件"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("清除报警"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("选择程式号")]
            public const string TargetJob = nameof(TargetJob);
            [Description("切换程序"), OperationLogCritical]
            public const string ChangeJob = nameof(ChangeJob);
            [Description("触发拍照"), OperationLogCritical]
            public const string Trigger = nameof(Trigger);
        }

        /// <summary>运动控制卡调试视图</summary>
        public static class CardDebugView
        {
            [Description("连接板卡"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("板卡复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
        }

        /// <summary>光源控制器调试视图</summary>
        public static class LightControllerDebugView
        {
            [Description("连接硬件"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开连接"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("读取亮度")]
            public const string ReadLightValue = nameof(ReadLightValue);
            [Description("清除报警"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("通道亮度输入框"), OperationLogCritical]
            public const string ChannelValueText = nameof(ChannelValueText);
        }

        /// <summary>线阵相机调试视图</summary>
        public static class LineScanCameraDebugView
        {
            [Description("打开相机"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("关闭相机"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("枚举在线相机")]
            public const string Discover = nameof(Discover);
            [Description("开始采集"), OperationLogCritical]
            public const string StartGrab = nameof(StartGrab);
            [Description("停止采集"), OperationLogCritical]
            public const string StopGrab = nameof(StopGrab);
            [Description("帧软触发"), OperationLogCritical]
            public const string SoftwareTrigger = nameof(SoftwareTrigger);
            [Description("取一帧"), OperationLogCritical]
            public const string GrabOne = nameof(GrabOne);
            [Description("取帧超时设置")]
            public const string WaitTimeoutMs = nameof(WaitTimeoutMs);
            [Description("存盘目录")]
            public const string SaveDirectory = nameof(SaveDirectory);
            [Description("存图片"), OperationLogCritical]
            public const string SaveImage = nameof(SaveImage);
            [Description("扫描模式")]
            public const string ScanMode = nameof(ScanMode);
            [Description("像素格式（选中即下发）"), OperationLogCritical]
            public const string PixelFormat = nameof(PixelFormat);
            [Description("无损压缩模式（选中即下发）"), OperationLogCritical]
            public const string ImageCompressionMode = nameof(ImageCompressionMode);
            [Description("模拟增益档位（选中即下发）"), OperationLogCritical]
            public const string PreampGain = nameof(PreampGain);
            [Description("曝光时间")]
            public const string ExposureTimeUs = nameof(ExposureTimeUs);
            [Description("数字增益")]
            public const string DigitalShift = nameof(DigitalShift);
            [Description("读取相机当前参数")]
            public const string RefreshParams = nameof(RefreshParams);
            [Description("行触发方式")]
            public const string LineTriggerMode = nameof(LineTriggerMode);
            [Description("行触发源")]
            public const string LineTriggerSource = nameof(LineTriggerSource);
            [Description("启用内部行频")]
            public const string LineRateEnable = nameof(LineRateEnable);
            [Description("内部行频")]
            public const string AcquisitionLineRate = nameof(AcquisitionLineRate);
            [Description("编码器选择器")]
            public const string EncoderSelector = nameof(EncoderSelector);
            [Description("编码器A相信号源")]
            public const string EncoderSourceA = nameof(EncoderSourceA);
            [Description("编码器B相信号源")]
            public const string EncoderSourceB = nameof(EncoderSourceB);
            [Description("编码器当量")]
            public const string PulseEquivalentUm = nameof(PulseEquivalentUm);
            [Description("分频/倍频系数")]
            public const string DividerRatio = nameof(DividerRatio);
            [Description("帧长(行)")]
            public const string ImageHeight = nameof(ImageHeight);
            [Description("帧超时")]
            public const string FrameTimeoutMs = nameof(FrameTimeoutMs);
            [Description("帧触发源")]
            public const string FrameTriggerSource = nameof(FrameTriggerSource);
            [Description("帧触发有效边沿")]
            public const string FrameTriggerActivation = nameof(FrameTriggerActivation);
            [Description("流选择器")]
            public const string StreamSelector = nameof(StreamSelector);
            [Description("相机类型")]
            public const string CameraType = nameof(CameraType);
            [Description("启用帧触发")]
            public const string FrameTriggerEnable = nameof(FrameTriggerEnable);
            [Description("读回设备当前值")]
            public const string RefreshParams2 = nameof(RefreshParams2);
            [Description("下发完整配置"), OperationLogCritical]
            public const string ApplyConfig = nameof(ApplyConfig);
            [Description("枚举全部节点")]
            public const string EnumerateNodes = nameof(EnumerateNodes);
            [Description("节点过滤")]
            public const string NodeFilter = nameof(NodeFilter);
            [Description("只看可写节点")]
            public const string WritableNodesOnly = nameof(WritableNodesOnly);
            [Description("节点名")]
            public const string NodeName = nameof(NodeName);
            [Description("节点值")]
            public const string NodeValue = nameof(NodeValue);
            [Description("读取节点")]
            public const string ReadNode = nameof(ReadNode);
            [Description("写入节点"), OperationLogCritical]
            public const string WriteNode = nameof(WriteNode);
            [Description("执行节点"), OperationLogCritical]
            public const string ExecuteNode = nameof(ExecuteNode);
        }

        /// <summary>图像采集卡调试视图</summary>
        public static class FrameGrabberDebugView
        {
            [Description("打开采集卡"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("关闭采集卡"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("复位"), OperationLogCritical]
            public const string Reset = nameof(Reset);
            [Description("模拟报警测试"), OperationLogCritical]
            public const string SimulateAlarm = nameof(SimulateAlarm);
            [Description("刷新状态")]
            public const string RefreshState = nameof(RefreshState);
            [Description("枚举本卡下的相机")]
            public const string DiscoverCameras = nameof(DiscoverCameras);
            [Description("帧长(行)")]
            public const string ImageHeight = nameof(ImageHeight);
            [Description("帧超时")]
            public const string FrameTimeoutMs = nameof(FrameTimeoutMs);
            [Description("帧触发源")]
            public const string TriggerSource = nameof(TriggerSource);
            [Description("帧触发有效边沿")]
            public const string TriggerActivation = nameof(TriggerActivation);
            [Description("流选择器")]
            public const string StreamSelector = nameof(StreamSelector);
            [Description("相机类型")]
            public const string CameraType = nameof(CameraType);
            [Description("残帧策略")]
            public const string PartialImageControl = nameof(PartialImageControl);
            [Description("启用帧触发")]
            public const string TriggerEnable = nameof(TriggerEnable);
            [Description("下发帧控制配置"), OperationLogCritical]
            public const string ApplyFrameControl = nameof(ApplyFrameControl);
            [Description("发送帧软触发"), OperationLogCritical]
            public const string SoftwareTrigger = nameof(SoftwareTrigger);
            [Description("枚举全部节点")]
            public const string EnumerateNodes = nameof(EnumerateNodes);
            [Description("节点过滤")]
            public const string NodeFilter = nameof(NodeFilter);
            [Description("只看可写节点")]
            public const string WritableNodesOnly = nameof(WritableNodesOnly);
            [Description("节点名")]
            public const string NodeName = nameof(NodeName);
            [Description("节点值")]
            public const string NodeValue = nameof(NodeValue);
            [Description("读取节点")]
            public const string ReadNode = nameof(ReadNode);
            [Description("写入节点"), OperationLogCritical]
            public const string WriteNode = nameof(WriteNode);
            [Description("执行节点"), OperationLogCritical]
            public const string ExecuteNode = nameof(ExecuteNode);
        }

        /// <summary>硬件综合调试视图</summary>
        public static class HardwareDebugView
        {
            [Description("一键切换全局模拟模式"), OperationLogCritical]
            public const string ToggleGlobalSimulation = nameof(ToggleGlobalSimulation);
            [Description("切换单设备模拟模式"), OperationLogCritical]
            public const string ToggleDeviceSimulation = nameof(ToggleDeviceSimulation);
        }

        /// <summary>总工站调试视图</summary>
        public static class StationDebugView
        {
            [Description("全线初始化"), OperationLogCritical]
            public const string InitializeAll = nameof(InitializeAll);
            [Description("启动"), OperationLogCritical]
            public const string StartAll = nameof(StartAll);
            [Description("暂停"), OperationLogCritical]
            public const string PauseAll = nameof(PauseAll);
            [Description("恢复"), OperationLogCritical]
            public const string ResumeAll = nameof(ResumeAll);
            [Description("复位"), OperationLogCritical]
            public const string ResetAll = nameof(ResetAll);
            [Description("停止"), OperationLogCritical]
            public const string StopAll = nameof(StopAll);
            [Description("释放信号量"), OperationLogCritical]
            public const string ReleaseSignal = nameof(ReleaseSignal);
            [Description("复位信号量"), OperationLogCritical]
            public const string ResetSignal = nameof(ResetSignal);
        }

        /// <summary>线扫检测模组调试视图</summary>
        public static class LineScanDetectionModuleDebugView
        {
            [Description("选择当前模组")]
            public const string SelectedModule = nameof(SelectedModule);
            [Description("初始化模组"), OperationLogCritical]
            public const string InitializeModule = nameof(InitializeModule);
            [Description("模组报警复位"), OperationLogCritical]
            public const string ResetModule = nameof(ResetModule);
            [Description("模组停止"), OperationLogCritical]
            public const string Stop = nameof(Stop);
            [Description("移动到扫描起点"), OperationLogCritical]
            public const string GotoStart = nameof(GotoStart);
            [Description("开始扫描"), OperationLogCritical]
            public const string Scan = nameof(Scan);
            [Description("中止扫描"), OperationLogCritical]
            public const string Abort = nameof(Abort);
        }

        /// <summary>TCP 服务端调试视图</summary>
        public static class TcpServerDebugView
        {
            [Description("启动"), OperationLogCritical]
            public const string Start = nameof(Start);
            [Description("停止"), OperationLogCritical]
            public const string Stop = nameof(Stop);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("发送测试数据")]
            public const string SendText = nameof(SendText);
            [Description("广播发送"), OperationLogCritical]
            public const string Broadcast = nameof(Broadcast);
            [Description("发送到选中客户端"), OperationLogCritical]
            public const string SendToClient = nameof(SendToClient);
            [Description("断开选中客户端"), OperationLogCritical]
            public const string DisconnectClient = nameof(DisconnectClient);
            [Description("选择客户端")]
            public const string SelectedClient = nameof(SelectedClient);
        }

        /// <summary>TCP 客户端调试视图</summary>
        public static class TcpClientDebugView
        {
            [Description("连接"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("发送测试数据")]
            public const string SendText = nameof(SendText);
            [Description("发送"), OperationLogCritical]
            public const string Send = nameof(Send);
        }

        /// <summary>文件传输通道调试视图</summary>
        public static class FileTransferDebugView
        {
            [Description("启动"), OperationLogCritical]
            public const string Start = nameof(Start);
            [Description("停止"), OperationLogCritical]
            public const string Stop = nameof(Stop);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("业务标签")]
            public const string TestTag = nameof(TestTag);
            [Description("测试数据大小(MB)")]
            public const string TestSizeMb = nameof(TestSizeMb);
            [Description("生成并发送测试数据"), OperationLogCritical]
            public const string SendTestData = nameof(SendTestData);
            [Description("发送文件路径")]
            public const string SendFilePath = nameof(SendFilePath);
            [Description("浏览发送文件")]
            public const string BrowseFile = nameof(BrowseFile);
            [Description("发送文件"), OperationLogCritical]
            public const string SendFile = nameof(SendFile);
            [Description("分片级诊断日志开关")]
            public const string ChunkDiagnosticsEnabled = nameof(ChunkDiagnosticsEnabled);
            [Description("接收消费方式")]
            public const string ConsumeMode = nameof(ConsumeMode);
            [Description("保存目录")]
            public const string SaveDirectory = nameof(SaveDirectory);
            [Description("浏览保存目录")]
            public const string BrowseSaveDir = nameof(BrowseSaveDir);
        }

        /// <summary>Modbus RTU 主站调试视图</summary>
        public static class ModbusRtuDebugView
        {
            [Description("打开"), OperationLogCritical]
            public const string Open = nameof(Open);
            [Description("关闭"), OperationLogCritical]
            public const string Close = nameof(Close);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("地址/写入值按十六进制填写")]
            public const string UseHexInput = nameof(UseHexInput);
            [Description("功能码")]
            public const string FunctionCode = nameof(FunctionCode);
            [Description("从站地址")]
            public const string UnitId = nameof(UnitId);
            [Description("起始地址")]
            public const string AddressText = nameof(AddressText);
            [Description("读写个数")]
            public const string Quantity = nameof(Quantity);
            [Description("写入值")]
            public const string WriteValueText = nameof(WriteValueText);
            [Description("执行读写测试"), OperationLogCritical]
            public const string ExecuteTest = nameof(ExecuteTest);
            [Description("写多个-逐位置填值")]
            public const string MultiWriteValue = nameof(MultiWriteValue);
            [Description("原始报文 PDU")]
            public const string RawPduText = nameof(RawPduText);
            [Description("发送原始报文"), OperationLogCritical]
            public const string SendRaw = nameof(SendRaw);
        }

        /// <summary>Modbus TCP 主站调试视图</summary>
        public static class ModbusTcpDebugView
        {
            [Description("连接"), OperationLogCritical]
            public const string Connect = nameof(Connect);
            [Description("断开"), OperationLogCritical]
            public const string Disconnect = nameof(Disconnect);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("地址/写入值按十六进制填写")]
            public const string UseHexInput = nameof(UseHexInput);
            [Description("事务号递增")]
            public const string TransactionIdIncrement = nameof(TransactionIdIncrement);
            [Description("重置事务号")]
            public const string ResetTransactionId = nameof(ResetTransactionId);
            [Description("固定事务号")]
            public const string FixedTransactionIdText = nameof(FixedTransactionIdText);
            [Description("功能码")]
            public const string FunctionCode = nameof(FunctionCode);
            [Description("从站地址")]
            public const string UnitId = nameof(UnitId);
            [Description("起始地址")]
            public const string AddressText = nameof(AddressText);
            [Description("读写个数")]
            public const string Quantity = nameof(Quantity);
            [Description("写入值")]
            public const string WriteValueText = nameof(WriteValueText);
            [Description("执行读写测试"), OperationLogCritical]
            public const string ExecuteTest = nameof(ExecuteTest);
            [Description("写多个-逐位置填值")]
            public const string MultiWriteValue = nameof(MultiWriteValue);
            [Description("原始报文 PDU")]
            public const string RawPduText = nameof(RawPduText);
            [Description("发送原始报文"), OperationLogCritical]
            public const string SendRaw = nameof(SendRaw);
        }

        /// <summary>串口调试视图</summary>
        public static class SerialPortDebugView
        {
            [Description("打开串口"), OperationLogCritical]
            public const string Open = nameof(Open);
            [Description("关闭串口"), OperationLogCritical]
            public const string Close = nameof(Close);
            [Description("打开参数设置")]
            public const string ShowParamDialog = nameof(ShowParamDialog);
            [Description("发送测试数据")]
            public const string SendText = nameof(SendText);
            [Description("十六进制发送")]
            public const string SendAsHex = nameof(SendAsHex);
            [Description("发送"), OperationLogCritical]
            public const string Send = nameof(Send);
            [Description("十六进制显示")]
            public const string DisplayAsHex = nameof(DisplayAsHex);
            [Description("清空收发记录"), OperationLogCritical]
            public const string ClearLog = nameof(ClearLog);
        }

        /// <summary>TCP 服务端参数设置对话框</summary>
        public static class TcpServerParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>TCP 客户端参数设置对话框</summary>
        public static class TcpClientParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>文件传输通道参数设置对话框</summary>
        public static class FileTransferParamDialog
        {
            [Description("新增 Lane"), OperationLogCritical]
            public const string AddLane = nameof(AddLane);
            [Description("删除 Lane"), OperationLogCritical]
            public const string RemoveLane = nameof(RemoveLane);
            [Description("选择 Lane")]
            public const string SelectedLane = nameof(SelectedLane);
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>Modbus RTU 参数设置对话框</summary>
        public static class ModbusRtuParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>Modbus TCP 参数设置对话框</summary>
        public static class ModbusTcpParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>串口参数设置对话框</summary>
        public static class SerialPortParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }

        /// <summary>轴参数设置对话框</summary>
        public static class AxisParamDialog
        {
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
            [Description("确认保存参数"), OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }
    }
}
