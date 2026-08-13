namespace PF.Infrastructure.Hardware.Vision.Hikvision
{
    /// <summary>
    /// GenICam 节点中文释义表。
    ///
    /// <para><b>为什么需要它</b>：设备 XML 自带的 ToolTip 是英文且往往极简
    /// （"Controls the device temperature." 之类），面对上千个节点，现场判断
    /// "这个到底是干什么的、能不能动"成本很高。这里对**标准 SFNC 节点**与
    /// **海康线扫/采集卡特有节点**给出中文说明，查不到的仍回退设备自带描述。</para>
    ///
    /// <para><b>覆盖范围</b>：只收录含义明确的节点。刻意不做"猜个大概"的条目——
    /// 调试面板上一句似是而非的中文，比一句英文原文更容易把人带偏。
    /// 缺条目时补充到这里即可，不影响其他逻辑。</para>
    ///
    /// <para>匹配为**精确名**优先，其次去掉尾部数字后再试一次
    /// （EncoderSourceA / LineSelector0 这类带序号的节点共用同一条释义）。</para>
    /// </summary>
    internal static class GenICamNodeGlossary
    {
        /// <summary>取节点的中文释义；无对应条目时返回 null。</summary>
        public static string? Describe(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName)) return null;

            if (_map.TryGetValue(nodeName, out var text)) return text;

            // 去掉结尾序号再试：Line0/Line1、Timer0Duration 这类节点共用释义
            string trimmed = nodeName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (trimmed.Length > 0 && trimmed.Length != nodeName.Length
                && _map.TryGetValue(trimmed, out text))
                return text;

            return null;
        }

        private static readonly Dictionary<string, string> _map = new(StringComparer.Ordinal)
        {
            // ── 设备信息 ────────────────────────────────────────────────────
            ["DeviceVendorName"] = "厂商名称",
            ["DeviceModelName"] = "设备型号",
            ["DeviceManufacturerInfo"] = "厂商附加信息",
            ["DeviceVersion"] = "设备版本",
            ["DeviceFirmwareVersion"] = "固件版本",
            ["DeviceSerialNumber"] = "设备序列号",
            ["DeviceUserID"] = "用户自定义设备名（可改，用于区分同型号多台设备）",
            ["DeviceID"] = "设备唯一标识",
            ["DeviceScanType"] = "扫描类型：Areascan 面阵 / Linescan 线阵",
            ["DeviceTemperature"] = "设备当前温度（℃）",
            ["DeviceTemperatureSelector"] = "温度测点选择（选定后由 DeviceTemperature 读数）",
            ["DeviceReset"] = "重启设备（命令）。执行后连接会断开，需重新连接",
            ["DeviceUptime"] = "设备已运行时长（秒）",
            ["DeviceLinkSelector"] = "链路选择",
            ["DeviceLinkSpeed"] = "链路速率",
            ["DeviceMaxThroughput"] = "设备最大数据吞吐量（字节/秒）",
            ["DeviceRegistersStreamingStart"] = "开始批量写寄存器（命令）",
            ["DeviceRegistersStreamingEnd"] = "结束批量写寄存器（命令）",

            // ── 图像格式与 ROI ──────────────────────────────────────────────
            ["Width"] = "图像宽度（像素）。线扫相机即传感器有效像元数",
            ["Height"] = "图像高度（像素）。**线扫相机直连时即一帧累计多少行**",
            ["WidthMax"] = "传感器最大宽度",
            ["HeightMax"] = "传感器最大高度",
            ["OffsetX"] = "ROI 水平起始位置",
            ["OffsetY"] = "ROI 垂直起始位置",
            ["PixelFormat"] = "像素格式，如 Mono8 / Mono12 / BayerRG8。改它会影响可用的压缩模式",
            ["PixelSize"] = "像素位深",
            ["PixelColorFilter"] = "Bayer 滤镜排列方式",
            ["ReverseX"] = "图像水平镜像",
            ["ReverseY"] = "图像垂直镜像",
            ["BinningHorizontal"] = "水平合并像素数（提高灵敏度、降低分辨率）",
            ["BinningVertical"] = "垂直合并像素数",
            ["DecimationHorizontal"] = "水平抽点（隔点取样，降低分辨率与数据量）",
            ["DecimationVertical"] = "垂直抽点",
            ["TestPattern"] = "测试图案。出图异常时用它区分是传感器问题还是传输问题",
            ["ImageCompressionMode"] = "无损压缩模式（HB）。开启后取到的是压缩流，需先解码才是裸像素",
            ["ImageCompressionQuality"] = "压缩质量",
            ["ScanMode"] = "扫描模式：LineScan 线阵 / FrameScan 面阵",

            // ── 采集控制 ────────────────────────────────────────────────────
            ["AcquisitionMode"] = "采集模式：Continuous 连续 / SingleFrame 单帧 / MultiFrame 多帧",
            ["AcquisitionStart"] = "开始采集（命令）",
            ["AcquisitionStop"] = "停止采集（命令）",
            ["AcquisitionBurstFrameCount"] = "一次触发采集的帧数",
            ["AcquisitionFrameRate"] = "采集帧率（帧/秒）",
            ["AcquisitionFrameRateEnable"] = "启用帧率控制。不启用时按曝光与传输能力自由出图",
            ["AcquisitionLineRate"] = "行频（行/秒）。**线扫相机每秒扫多少行**",
            ["AcquisitionLineRateEnable"] = "启用内部行频。台架无编码器时开它即可自行出图",
            ["ResultingFrameRate"] = "当前参数下的实际帧率（只读）",
            ["ResultingLineRate"] = "当前参数下的实际行频（只读）",

            // ── 触发控制 ────────────────────────────────────────────────────
            ["TriggerSelector"] = "触发类型选择：FrameStart 帧开始 / LineStart 行开始 / FrameBurstStart 帧组开始。**先选它，后面的 TriggerMode/TriggerSource 才是对应这一类的**",
            ["TriggerMode"] = "当前所选触发类型的开关：On 外部触发 / Off 自由运行",
            ["TriggerSource"] = "触发源：Software 软触发 / Line0~3 硬件线 / Counter / EncoderModuleOut 编码器",
            ["TriggerActivation"] = "触发有效边沿：RisingEdge 上升沿 / FallingEdge 下降沿 / LevelHigh 高电平 / LevelLow 低电平",
            ["TriggerDelay"] = "触发延时（微秒）。信号到达后延迟这么久才真正触发",
            ["TriggerSoftware"] = "发送一次软触发（命令）。需先把触发源设为 Software",
            ["TriggerCacheEnable"] = "触发缓存：相机忙时到来的触发信号是否缓存下来补发",
            ["LineTriggerControl"] = "行触发控制组（新固件才有；据此判断走新还是老节点树）",
            ["LineTriggerMode"] = "行触发开关：开启后每行由外部信号驱动，关闭则按内部行频",
            ["LineTriggerSource"] = "行触发源。编码器模式固定为 EncoderModuleOut",
            ["FrameTriggerControl"] = "帧触发控制组（新固件才有）",
            ["FrameTriggerMode"] = "帧触发开关：开启后由外部信号决定一帧从哪里开始",
            ["FrameTriggerSource"] = "帧触发源",

            // ── 编码器 ──────────────────────────────────────────────────────
            ["EncoderSelector"] = "编码器选择（Encoder0/1...）。**先选它，后面的编码器参数才是对应这一路的**",
            ["EncoderSourceA"] = "编码器 A 相接入的 IO 线",
            ["EncoderSourceB"] = "编码器 B 相接入的 IO 线。A/B 两相共同决定运动方向",
            ["EncoderTriggerMode"] = "编码器触发方式：单向只在正向计数时出触发，双向正反都出",
            ["EncoderCounterMode"] = "计数模式",
            ["EncoderCounter"] = "编码器当前计数值",
            ["EncoderCounterMax"] = "计数最大值",
            ["EncoderCounterReset"] = "计数清零（命令）",
            ["EncoderMaxReverseCounter"] = "允许的最大反向计数。超过后重新正向运动才恢复出触发，用于抑制抖动",
            ["EncoderReverseCounterReset"] = "反向计数清零（命令）",
            ["EncoderResetSource"] = "计数复位信号源",
            ["EncoderDivider"] = "编码器分频系数。每 N 个脉冲触发一行，直接决定行间距",
            ["EncoderMultiplier"] = "编码器倍频系数",
            ["EncoderOutputMode"] = "编码器输出模式",

            // ── 曝光与增益 ──────────────────────────────────────────────────
            ["ExposureMode"] = "曝光模式：Timed 按设定时长 / TriggerWidth 由触发脉宽决定",
            ["ExposureTime"] = "曝光时间（微秒）。线扫相机它同时限制最高行频",
            ["ExposureAuto"] = "自动曝光：Off / Once / Continuous。**开启时 ExposureTime 只读**",
            ["AutoExposureTimeLowerLimit"] = "自动曝光下限（微秒）",
            ["AutoExposureTimeUpperLimit"] = "自动曝光上限（微秒）",
            ["Gain"] = "增益（dB）。放大信号同时也放大噪声",
            ["GainAuto"] = "自动增益：Off / Once / Continuous。开启时 Gain 只读",
            ["AutoGainLowerLimit"] = "自动增益下限",
            ["AutoGainUpperLimit"] = "自动增益上限",
            ["PreampGain"] = "模拟增益档位（枚举，非数值）。在 AD 转换前放大，噪声表现优于数字增益",
            ["DigitalShift"] = "数字增益。AD 之后按倍数放大，会同时放大量化噪声",
            ["DigitalShiftEnable"] = "启用数字增益",
            ["Brightness"] = "目标亮度（自动曝光/自动增益的调节目标值）",
            ["BlackLevel"] = "黑电平，整体抬高或压低底噪基准",
            ["BlackLevelEnable"] = "启用黑电平调节",
            ["Gamma"] = "伽马值。小于 1 提亮暗部，大于 1 压暗",
            ["GammaEnable"] = "启用伽马校正",
            ["GammaSelector"] = "伽马类型：User 自定义 / sRGB 标准",
            ["Sharpness"] = "锐度",
            ["SharpnessEnable"] = "启用锐化",
            ["NoiseReduction"] = "降噪强度",
            ["BalanceWhiteAuto"] = "自动白平衡（彩色相机）",
            ["BalanceRatioSelector"] = "白平衡通道选择：Red / Green / Blue",
            ["BalanceRatio"] = "所选通道的白平衡系数",

            // ── 平场校正 / 线扫特有 ─────────────────────────────────────────
            ["FFCEnable"] = "平场校正开关。线扫必备：补偿光照不均与像元响应差异",
            ["FFCCoefficient"] = "平场校正系数",
            ["FFCGenerate"] = "生成平场校正数据（命令）。需在均匀白场下执行",
            ["FFCGenerateStatus"] = "平场校正数据生成状态",
            ["FFCUserCoefficientSelector"] = "平场校正系数组选择",
            ["PRNUEnable"] = "像元响应非均匀性校正开关",
            ["DSNUEnable"] = "暗场非均匀性校正开关",
            ["LineSpacing"] = "行间距",

            // ── 数字 IO ─────────────────────────────────────────────────────
            ["LineSelector"] = "IO 线选择（Line0/1/2...）。**先选它，后面的 Line* 参数才是对应这一路的**",
            ["LineMode"] = "线模式：Input 输入 / Output 输出 / Strobe 闪光",
            ["LineInverter"] = "电平反相",
            ["LineStatus"] = "该线当前电平状态（只读）",
            ["LineStatusAll"] = "全部 IO 线电平状态位图（只读）",
            ["LineSource"] = "输出线的信号来源：ExposureActive 曝光中 / StrobeSignal 闪光 / UserOutput 用户自定义",
            ["LineFormat"] = "电气类型：OptoCoupled 光耦 / TTL / LVDS",
            ["LineDebouncerTime"] = "输入去抖时间（微秒）。短于此时长的脉冲被忽略，抑制干扰误触发",
            ["StrobeEnable"] = "闪光输出使能",
            ["StrobeLineSelector"] = "闪光输出线选择",
            ["StrobeLineDuration"] = "闪光持续时间（微秒）",
            ["StrobeLineDelay"] = "闪光延时（微秒）",
            ["StrobeLinePreDelay"] = "闪光提前量（微秒）。用于补偿光源自身的点亮延迟",
            ["UserOutputSelector"] = "用户输出位选择",
            ["UserOutputValue"] = "用户输出位电平",

            // ── 传输层（GigE） ──────────────────────────────────────────────
            ["GevSCPSPacketSize"] = "网络包大小（字节）。设成链路支持的最大值可显著降低丢包与 CPU 占用",
            ["GevSCPD"] = "包间隔。多相机共用一条链路时增大它可缓解拥塞",
            ["GevCurrentIPAddress"] = "当前 IP 地址",
            ["GevCurrentSubnetMask"] = "当前子网掩码",
            ["GevCurrentDefaultGateway"] = "当前网关",
            ["GevPersistentIPAddress"] = "静态 IP 地址",
            ["GevHeartbeatTimeout"] = "心跳超时（毫秒）。超时未收到心跳，相机会主动断开",
            ["GevIPConfigurationStatus"] = "IP 获取方式（只读）",
            ["GevSCPSFireTestPacket"] = "发送测试包，用于探测链路最大包长",

            // ── 采集卡（Interface）节点 ─────────────────────────────────────
            ["StreamSelector"] = "流通道选择。一卡多路时先选它，后面的流参数才是对应这一路的",
            ["CurrentStreamDevice"] = "当前流绑定的相机（只读）。**未绑定时流参数不可写**",
            ["ImageHeight"] = "帧长：一帧累计多少行。**经采集卡时线扫的帧长写在这里**",
            ["FrameTimeoutTime"] = "帧超时（毫秒）。行数攒不满一帧时的兜底出图时间，避免永久等待",
            ["MinFrameDelay"] = "最小帧间隔",
            ["CameraType"] = "相机类型/位宽匹配。CameraLink 必须与相机侧一致，否则出不了图或图像横向错位",
            ["PartialImageOutputMode"] = "残帧输出策略（XoF 卡）：不足一帧的数据是否输出",
            ["StreamPartialImageControl"] = "残帧输出策略（CameraLink 卡）",
            ["StreamTriggerEnable"] = "卡侧帧触发开关。关闭即连续模式，攒满帧长自动出图",
            ["StreamTriggerSource"] = "卡侧帧触发源：SoftwareSignal0 软触发 / QuickSoftwareTrigger0 快速软触发（需固件支持）/ 硬件信号",
            ["StreamTriggerActivation"] = "卡侧帧触发有效边沿",
            ["StreamSoftwareTrigger"] = "发送一次卡侧帧软触发（命令）。需先开启 StreamTriggerEnable",
            ["QuickSoftwareTrigger"] = "快速软触发（命令）。延迟低于普通软触发，仅部分卡固件支持",
            ["StreamEnableStatus"] = "流使能状态",
            ["InterfaceID"] = "采集卡标识",
            ["BayerCFAEnable"] = "Bayer 转换使能（卡侧）",
            ["IspGamma"] = "卡侧伽马值",
            ["IspGammaEnable"] = "卡侧伽马使能",

            // ── 计数器与定时器 ──────────────────────────────────────────────
            ["CounterSelector"] = "计数器选择",
            ["CounterEventSource"] = "计数事件源",
            ["CounterValue"] = "计数器当前值",
            ["CounterReset"] = "计数器清零（命令）",
            ["TimerSelector"] = "定时器选择",
            ["TimerDuration"] = "定时器时长（微秒）",
            ["TimerDelay"] = "定时器延时（微秒）",

            // ── 用户参数组 ──────────────────────────────────────────────────
            ["UserSetSelector"] = "参数组选择：Default 出厂默认 / UserSet1~3 用户组",
            ["UserSetLoad"] = "加载所选参数组（命令）。会覆盖当前所有参数",
            ["UserSetSave"] = "保存当前参数到所选组（命令）。Default 组不可写",
            ["UserSetDefault"] = "开机默认加载的参数组",
            ["UserSetCurrent"] = "当前生效的参数组（只读）",

            // ── 事件与传输统计 ──────────────────────────────────────────────
            ["EventSelector"] = "事件选择",
            ["EventNotification"] = "事件上报开关",
            ["PayloadSize"] = "单帧数据负载大小（字节，只读）",
            ["TLParamsLocked"] = "取流期间锁定参数（只读）。取流中大量参数会因此转为只读",
            ["ChunkModeActive"] = "帧附加信息（Chunk）开关",
            ["ChunkSelector"] = "附加信息项选择",
            ["ChunkEnable"] = "启用所选附加信息项",
        };
    }
}
