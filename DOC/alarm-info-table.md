# 报警信息表

序号 | 报警代码 | 报警ID | 分类 | 中文报警信息 | 英文报警信息 | 严重级别
--- | --- | --- | --- | --- | --- | ---
1 | HW_SRV_001 | 10000 | 硬件异常 | 伺服驱动器离线或报错 | Servo drive offline or error | 3
2 | HW_IO_001 | 10001 | 硬件异常 | IO 模块连接失败 | IO module connection failed | 2
3 | HW_IO_002 | 10002 | 硬件异常 | IO 读取异常 | IO read error | 2
4 | HW_IO_003 | 10003 | 硬件异常 | IO 设置异常 | IO set error | 2
5 | HW_CARD_001 | 10004 | 硬件异常 | 运动控制卡初始化失败 | Motion card init failed | 3
6 | HW_CARD_002 | 10005 | 硬件异常 | 运动控制卡总线通讯错误（运行期检测） | Motion card bus comm error | 3
7 | HW_CAM_001 | 10006 | 硬件异常 | 相机连接超时 | Camera connection timeout | 2
8 | HW_CAM_002 | 10007 | 硬件异常 | 相机通讯心跳超时（TCP 连接丢失） | Camera heartbeat timeout | 2
9 | HW_BCR_001 | 10008 | 硬件异常 | 条码扫描枪连接失败 | Barcode scanner conn failed | 1
10 | HW_BCR_002 | 10009 | 硬件异常 | 扫码枪通讯心跳超时（TCP 连接丢失） | Scanner heartbeat timeout | 1
11 | HW_LGT_001 | 10010 | 硬件异常 | 光源控制器通讯异常 | Light controller comm error | 1
12 | HW_AXIS_002 | 10011 | 硬件异常 | 伺服轴触发限位保护（PEL/MEL） | Servo axis limit triggered | 2
13 | HW_AXIS_003 | 10012 | 运动超时 | 伺服轴运动完成等待超时 | Axis motion done timeout | 2
14 | HW_AXIS_004 | 10013 | 运动超时 | 伺服轴回原点完成等待超时 | Axis homing done timeout | 2
15 | HW_AXIS_005 | 10014 | 定位异常 | 伺服轴到位精度超限，实际位置与目标位置偏差过大 | Axis positioning accuracy exceeded | 2
16 | HW_AXIS_006 | 10015 | 定位异常 | 伺服轴获取当前位置失败，无法进行定位精度校验 | Axis position read failed | 2
17 | SYS_INIT_001 | 10016 | 系统异常 | 系统初始化超时，硬件未全部就绪 | System init timeout, HW not ready | 3
18 | SYS_DB_001 | 10017 | 系统异常 | 数据库写入失败 | Database write failed | 2
19 | SYS_SYNC_001 | 10018 | 系统异常 | 工站同步服务异常 | Station sync service error | 2
20 | SYS_UNDEFINED_001 | 10019 | 系统异常 | 报警触发时未提供错误码（已兜底） | No error code provided | 2
21 | SYS_TEST_001 | 10020 | 调试测试 | 调试页面手动触发的模拟报警 | Debug page manual test alarm | 1
22 | SYS_SYNC_002 | 10021 | 系统异常 | 状态机指针漂移，进入未定义步序 | State machine undefined step | 3
23 | HW_SAFE_001 | 10022 | 安全防护 | 安全门打开，设备已暂停 | Safety door open, paused | 1
24 | HW_SAFE_001_1 | 10023 | 安全防护 | 工位1安全门打开，设备已暂停 | Station 1 safety door open | 1
25 | HW_SAFE_001_2 | 10024 | 安全防护 | 工位2安全门打开，设备已暂停 | Station 2 safety door open | 1
26 | HW_SAFE_002 | 10025 | 安全防护 | 安全监控IO连续读取失败，安全门检测可能已失效 | Safety IO fail, door detect lost | 3
27 | PROC_WS1F_DATA_001 | 10026 | 流程异常/数据 | 工位1上下料-批次产品个数为0 | WS1 load/unload: batch count 0 | 2
28 | PROC_WS1F_DATA_002 | 10027 | 流程异常/数据 | 工位1上下料-料盒尺寸与配方不匹配 | WS1 load/unload: box size mismatch | 2
29 | PROC_WS1F_DATA_003 | 10028 | 流程异常/数据 | 工位1上下料-配方参数为空 | WS1 load/unload: recipe empty | 2
30 | PROC_WS1F_ALG_001 | 10029 | 流程异常/算法 | 工位1上下料-寻层算法判定为0层 | WS1 load/unload: layer find = 0 | 2
31 | PROC_WS1F_ALG_002 | 10030 | 流程异常/算法 | 工位1上下料-寻层算法出现严重异常 | WS1 LD/ULD: layer find fatal err | 2
32 | PROC_WS1F_SEN_001 | 10031 | 流程异常/传感器 | 工位1上下料-料盒尺寸识别失败（传感器信号异常） | WS1 LD/ULD: box size detect fail | 2
33 | PROC_WS1F_MOT_001 | 10032 | 流程异常/运动 | 工位1上下料-Z轴运动条件不满足 | WS1 LD/ULD: Z-axis mot not ready | 2
34 | PROC_WS1F_MOT_002 | 10033 | 流程异常/运动 | 工位1上下料-X轴运动条件不满足 | WS1 LD/ULD: X-axis mot not ready | 2
35 | PROC_WS1F_SEN_002 | 10034 | 流程异常/传感器 | 工位1上下料-Z轴寻层扫描异常（结果为空或过程出错） | WS1 load/unload: Z layer scan error | 2
36 | PROC_WS1F_MAT_001 | 10035 | 流程异常/物料 | 工位1上下料-物料错层翘起，禁止拉料 | WS1 LD/ULD: wafer warp, pull locked | 2
37 | PROC_WS1F_MOT_003 | 10036 | 流程异常/运动 | 工位1上下料-Z轴运动超时 | WS1 LD/ULD: Z-axis mot timeout | 2
38 | PROC_WS1F_MOT_004 | 10037 | 流程异常/运动 | 工位1上下料-X轴运动超时 | WS1 LD/ULD: X-axis mot timeout | 2
39 | PROC_WS1F_MOT_005 | 10038 | 流程异常/运动 | 工位1上下料-初始化上料状态失败（Z/X轴运动到待机位失败） | WS1 LD/ULD: init LD state fail | 2
40 | PROC_WS1F_MOT_007 | 10039 | 流程异常/运动 | 工位1上下料-切换阵列配方尺寸失败 | WS1 LD/ULD: recipe size switch fail | 2
41 | PROC_WS1F_SEN_003 | 10040 | 流程异常/传感器 | 工位1上下料-料盒公用底座未检测到物体 | WS1 load/unload: box base no object | 2
42 | PROC_WS1F_SEN_006 | 10041 | 流程异常/传感器 | 工位1上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发） | WS1: box size sensor clash | 2
43 | PROC_WS1F_ALG_003 | 10042 | 流程异常/算法 | 工位1上下料-目标层数超出有效范围 | WS1: target layer out of range | 2
44 | PROC_WS1F_ALG_004 | 10043 | 流程异常/算法 | 工位1上下料-未找到目标层的阵列点位（可能未执行生产状态切换） | WS1: target layer missing | 2
45 | PROC_WS1F_MOT_006 | 10044 | 流程异常/运动 | 工位1上下料-Z轴切换层运动失败 | WS1 LD/ULD: Z layer switch fail | 2
46 | PROC_WS1F_MOT_011 | 10045 | 流程异常/运动 | 工位1上下料-Z轴互锁失败：料盒未到位禁止升降 | WS1: Z ILK, box not ready | 2
47 | PROC_WS1F_MOT_008 | 10046 | 流程异常/运动 | 工位1上下料-X轴互锁失败：存在铁环突片 | WS1: X ILK, ring tab present | 2
48 | PROC_WS1F_ACT_001 | 10047 | 流程异常/执行器 | 工位1上下料-拉料互锁失败：晶圆盒挡杆未打开 | WS1: pull ILK, latch closed | 2
49 | PROC_WS1F_MOT_009 | 10048 | 流程异常/运动 | 工位1上下料-寻层扫描移动到起点失败 | WS1 LD/ULD: scan move to start fail | 2
50 | PROC_WS1F_SEN_007 | 10049 | 流程异常/传感器 | 工位1上下料-寻层扫描硬件锁存配置失败 | WS1 LD/ULD: scan latch config fail | 2
51 | PROC_WS1F_MOT_010 | 10050 | 流程异常/运动 | 工位1上下料-寻层扫描移动到终点失败 | WS1 LD/ULD: scan move to end fail | 2
52 | PROC_WS1F_ALG_005 | 10051 | 流程异常/算法 | 工位1上下料-寻层算法理论层坐标未初始化 | WS1 LD: slot-find coords not init | 2
53 | PROC_WS1F_ALG_006 | 10052 | 流程异常/算法 | 工位1上下料-寻层算法传感器原始数据不足 | WS1 LD: low sensor raw data | 2
54 | PROC_WS1F_ALG_007 | 10053 | 流程异常/算法 | 工位1上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效） | WS1 LD: dual-sensor count mismatch | 2
55 | PROC_WS1F_ALG_008 | 10054 | 流程异常/算法 | 工位1上下料-寻层算法检测到严重斜片(Cross-slot) | WS1 Load: Cross-slot wafer detected | 2
56 | PROC_WS1F_ALG_009 | 10055 | 流程异常/算法 | 工位1上下料-寻层算法检测到重叠片(Double-wafer) | WS1 Load: Double-wafer detected | 2
57 | PROC_WS1F_ALG_010 | 10056 | 流程异常/算法 | 工位1上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底） | WS1 LD: wafer off-slot, not seated | 2
58 | PROC_WS1F_ALG_011 | 10057 | 流程异常/算法 | 工位1上下料-指定层与实际寻层结果不匹配 | WS1 Load: Target slot mismatch | 2
59 | PROC_WS1F_RSM_001 | 10058 | 断点续跑 | 工位1上下料-重启后物料状态与记忆不一致 | WS1 Load: Resume status mismatch | 3
60 | PROC_WS1F_INIT_001 | 10059 | 流程异常/初始化 | 工位1上下料-初始化异常 | WS1 Load: Initialization failed | 2
61 | PROC_WS1F_RSM_002 | 10060 | 断点续跑 | 工位1上下料-断点续跑异常 | WS1 Load: Resume run failed | 2
62 | PROC_WS2F_DATA_001 | 10061 | 流程异常/数据 | 工位2上下料-批次产品个数为0 | WS2 LD: batch product count is zero | 2
63 | PROC_WS2F_DATA_002 | 10062 | 流程异常/数据 | 工位2上下料-料盒尺寸与配方不匹配 | WS2 Load: Cassette size mismatch | 2
64 | PROC_WS2F_DATA_003 | 10063 | 流程异常/数据 | 工位2上下料-配方参数为空 | WS2 Load: Recipe params empty | 2
65 | PROC_WS2F_ALG_001 | 10064 | 流程异常/算法 | 工位2上下料-寻层算法判定为0层 | WS2 Load: Slot-find algo=0 layers | 2
66 | PROC_WS2F_ALG_002 | 10065 | 流程异常/算法 | 工位2上下料-寻层算法出现严重异常 | WS2 LD: slot-find algo critical err | 2
67 | PROC_WS2F_SEN_001 | 10066 | 流程异常/传感器 | 工位2上下料-料盒尺寸识别失败（传感器信号异常） | WS2 LD: cassette size detect fail | 2
68 | PROC_WS2F_MOT_001 | 10067 | 流程异常/运动 | 工位2上下料-Z轴运动条件不满足 | WS2 Load: Z-axis motion not ready | 2
69 | PROC_WS2F_MOT_002 | 10068 | 流程异常/运动 | 工位2上下料-X轴运动条件不满足 | WS2 Load: X-axis motion not ready | 2
70 | PROC_WS2F_SEN_002 | 10069 | 流程异常/传感器 | 工位2上下料-Z轴寻层扫描异常（结果为空或过程出错） | WS2 Load: Z-axis slot scan abnormal | 2
71 | PROC_WS2F_MAT_001 | 10070 | 流程异常/物料 | 工位2上下料-物料错层翘起，禁止拉料 | WS2 Load: Wafer翘曲, pull blocked | 2
72 | PROC_WS2F_MOT_003 | 10071 | 流程异常/运动 | 工位2上下料-Z轴运动超时 | WS2 Load: Z-axis motion timeout | 2
73 | PROC_WS2F_MOT_004 | 10072 | 流程异常/运动 | 工位2上下料-X轴运动超时 | WS2 Load: X-axis motion timeout | 2
74 | PROC_WS2F_MOT_005 | 10073 | 流程异常/运动 | 工位2上下料-初始化上料状态失败（Z/X轴运动到待机位失败） | WS2 Load: Init load state failed | 2
75 | PROC_WS2F_MOT_007 | 10074 | 流程异常/运动 | 工位2上下料-切换阵列配方尺寸失败 | WS2 LD: switch array recipe fail | 2
76 | PROC_WS2F_SEN_003 | 10075 | 流程异常/传感器 | 工位2上下料-料盒公用底座未检测到物体 | WS2 Load: Cassette base empty | 2
77 | PROC_WS2F_SEN_006 | 10076 | 流程异常/传感器 | 工位2上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发） | WS2 Load: Cassette sensor conflict | 2
78 | PROC_WS2F_ALG_003 | 10077 | 流程异常/算法 | 工位2上下料-目标层数超出有效范围 | WS2 Load: Target layer out of range | 2
79 | PROC_WS2F_ALG_004 | 10078 | 流程异常/算法 | 工位2上下料-未找到目标层的阵列点位（可能未执行生产状态切换） | WS2 LD: target layer missing | 2
80 | PROC_WS2F_MOT_006 | 10079 | 流程异常/运动 | 工位2上下料-Z轴切换层运动失败 | WS2 LD: Z-axis layer switch fail | 2
81 | PROC_WS2F_MOT_011 | 10080 | 流程异常/运动 | 工位2上下料-Z轴互锁失败：料盒未到位禁止升降 | WS2 LD: Z ILK, cassette not ready | 2
82 | PROC_WS2F_MOT_008 | 10081 | 流程异常/运动 | 工位2上下料-X轴互锁失败：存在铁环突片 | WS2 LD: X ILK, ring protrusion | 2
83 | PROC_WS2F_ACT_001 | 10082 | 流程异常/执行器 | 工位2上下料-拉料互锁失败：晶圆盒挡杆未打开 | WS2 LD: pull ILK, latch not open | 2
84 | PROC_WS2F_MOT_009 | 10083 | 流程异常/运动 | 工位2上下料-寻层扫描移动到起点失败 | WS2 LD: slot scan start move fail | 2
85 | PROC_WS2F_SEN_007 | 10084 | 流程异常/传感器 | 工位2上下料-寻层扫描硬件锁存配置失败 | WS2 Load: Scan latch config failed | 2
86 | PROC_WS2F_MOT_010 | 10085 | 流程异常/运动 | 工位2上下料-寻层扫描移动到终点失败 | WS2 LD: slot scan move to end fail | 2
87 | PROC_WS2F_ALG_005 | 10086 | 流程异常/算法 | 工位2上下料-寻层算法理论层坐标未初始化 | WS2 LD: slot-find coords not init | 2
88 | PROC_WS2F_ALG_006 | 10087 | 流程异常/算法 | 工位2上下料-寻层算法传感器原始数据不足 | WS2 LD: low sensor raw data | 2
89 | PROC_WS2F_ALG_007 | 10088 | 流程异常/算法 | 工位2上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效） | WS2 LD: dual-sensor count mismatch | 2
90 | PROC_WS2F_ALG_008 | 10089 | 流程异常/算法 | 工位2上下料-寻层算法检测到严重斜片(Cross-slot) | WS2 Load: Cross-slot wafer detected | 2
91 | PROC_WS2F_ALG_009 | 10090 | 流程异常/算法 | 工位2上下料-寻层算法检测到重叠片(Double-wafer) | WS2 Load: Double-wafer detected | 2
92 | PROC_WS2F_ALG_010 | 10091 | 流程异常/算法 | 工位2上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底） | WS2 LD: wafer off-slot, not seated | 2
93 | PROC_WS2F_ALG_011 | 10092 | 流程异常/算法 | 工位2上下料-指定层与实际寻层结果不匹配 | WS2 Load: Target slot mismatch | 2
94 | PROC_WS2F_RSM_001 | 10093 | 断点续跑 | 工位2上下料-重启后物料状态与记忆不一致 | WS2 Load: Resume status mismatch | 3
95 | PROC_WS2F_INIT_001 | 10094 | 流程异常/初始化 | 工位2上下料-初始化异常 | WS2 Load: Initialization failed | 2
96 | PROC_WS2F_RSM_002 | 10095 | 断点续跑 | 工位2上下料-断点续跑异常 | WS2 Load: Resume run failed | 2
97 | PROC_WS1P_DATA_001 | 10096 | 流程异常/数据 | 工位1拉料-配方参数为空 | WS1 Pull: Recipe params empty | 2
98 | PROC_WS1P_INIT_001 | 10097 | 流程异常/初始化 | 工位1拉料-初始化校验失败 | WS1 Pull: Init check failed | 2
99 | PROC_WS1P_MOT_001 | 10098 | 流程异常/运动 | 工位1拉料-调整流道尺寸失败 | WS1 Pull: Adjust track size failed | 2
100 | PROC_WS1P_MOT_002 | 10099 | 流程异常/运动 | 工位1拉料-Y轴移动到取料位失败 | WS1 Pull: Y-axis move to pick fail | 2
101 | PROC_WS1P_ACT_001 | 10100 | 流程异常/执行器 | 工位1拉料-关闭夹爪失败（未感应到闭合信号） | WS1 Pull: Close grip failed | 2
102 | PROC_WS1P_MAT_001 | 10101 | 流程异常/物料 | 工位1拉料-检测到叠料异常 | WS1 Pull: Stacked wafer detected | 2
103 | PROC_WS1P_SEN_004 | 10102 | 流程异常/传感器 | 工位1拉料-8寸晶圆放反 | WS1 Pull: 8" Wafer Reversed | 2
104 | PROC_WS1P_SEN_005 | 10103 | 流程异常/传感器 | 工位1拉料-12寸晶圆放反 | WS1 Pull: 12" Wafer Reversed | 2
105 | PROC_WS1P_MOT_003 | 10104 | 流程异常/运动 | 工位1拉料-拉出至检测位失败（运动被中断） | WS1 Pull: Pull to Check Failed | 2
106 | PROC_WS1P_MOT_004 | 10105 | 流程异常/运动 | 工位1拉料-推回至料盒失败（运动被中断） | WS1 Pull: push back fail | 2
107 | PROC_WS1P_ACT_002 | 10106 | 流程异常/执行器 | 工位1拉料-打开夹爪失败 | WS1 Pull: Open Gripper Failed | 2
108 | PROC_WS1P_MOT_005 | 10107 | 流程异常/运动 | 工位1拉料-Y轴退回待机位失败 | WS1 Pull: Y-Axis Return Home Failed | 2
109 | PROC_WS1P_MAT_002 | 10108 | 流程异常/物料 | 工位1拉料-退回安全位后夹爪仍检测到带料 | WS1 Pull: Material Still in Gripper | 2
110 | PROC_WS1P_MOT_006 | 10109 | 流程异常/运动 | 工位1拉料-初始化拉料流程失败（Y轴运动到待机位失败） | WS1 Pull: Init Sequence Failed | 2
111 | PROC_WS1P_MAT_003 | 10110 | 流程异常/物料 | 工位1拉料-轨道有物料，无法执行尺寸切换 | WS1 Pull: track busy, no switch | 2
112 | PROC_WS1P_ACT_003 | 10111 | 流程异常/执行器 | 工位1拉料-尺寸切换气缸IO操作失败 | WS1 Pull: Size Cylinder IO Failed | 2
113 | PROC_WS1P_ACT_004 | 10112 | 流程异常/执行器 | 工位1拉料-尺寸切换气缸动作超时 | WS1 Pull: Size Cylinder Timeout | 2
114 | PROC_WS1P_ACT_005 | 10113 | 流程异常/执行器 | 工位1拉料-夹爪张开气缸操作失败 | WS1 Pull: Gripper Open Cyl Failed | 2
115 | PROC_WS1P_ACT_006 | 10114 | 流程异常/执行器 | 工位1拉料-夹爪张开超时，未感应到张开信号 | WS1 Pull: Gripper Open Timeout | 2
116 | PROC_WS1P_ACT_007 | 10115 | 流程异常/执行器 | 工位1拉料-夹爪闭合气缸操作失败 | WS1 Pull: Gripper Close Cyl Failed | 2
117 | PROC_WS1P_ACT_008 | 10116 | 流程异常/执行器 | 工位1拉料-夹爪闭合超时，未感应到闭合信号 | WS1 Pull: Gripper Close Timeout | 2
118 | PROC_WS1P_SEN_001 | 10117 | 流程异常/传感器 | 工位1拉料-夹爪闭合后未检测到铁环（空夹） | WS1 Pull: No Ring Detected (Empty) | 2
119 | PROC_WS1P_MOT_007 | 10118 | 流程异常/运动 | 工位1拉料-移动到待机位失败 | WS1 Pull: Move to Home Failed | 2
120 | PROC_WS1P_SEN_002 | 10119 | 流程异常/传感器 | 工位1拉料-待机位检测到残留物料 | WS1 Pull: Residual Material at Home | 2
121 | PROC_WS1P_MOT_008 | 10120 | 流程异常/运动 | 工位1拉料-移动到待机位失败（强制复位） | WS1 Pull: move to home fail (force) | 2
122 | PROC_WS1P_MOT_009 | 10121 | 流程异常/运动 | 工位1拉料-移动到取出安全位置失败 | WS1 Pull: Move to Safe Pos Failed | 2
123 | PROC_WS1P_SEN_003 | 10122 | 流程异常/传感器 | 工位1拉料-卸料后夹爪物料粘连未脱落 | WS1 Pull: mat stuck, unload fail | 2
124 | PROC_WS1P_MOT_010 | 10123 | 流程异常/运动 | 工位1拉料-移动到取料位置失败 | WS1 Pull: Move to Pick Pos Failed | 2
125 | PROC_WS1P_MOT_011 | 10124 | 流程异常/运动 | 工位1拉料-拉出运动触发失败 | WS1 Pull: pull motion trigger fail | 2
126 | PROC_WS1P_MOT_012 | 10125 | 流程异常/运动 | 工位1拉料-拉出过程卡料报警，已紧急停止 | WS1 Pull: Pull Jam, E-Stop Active | 3
127 | PROC_WS1P_MOT_013 | 10126 | 流程异常/运动 | 工位1拉料-拉出过程丢料报警，已紧急停止 | WS1 Pull: Pull Drop, E-Stop Active | 3
128 | PROC_WS1P_MOT_014 | 10127 | 流程异常/运动 | 工位1拉料-Y轴拉出运动超时 | WS1 Pull: Y-Axis Pull Timeout | 2
129 | PROC_WS1P_MOT_015 | 10128 | 流程异常/运动 | 工位1拉料-送入运动触发失败 | WS1 Pull: feed motion trigger fail | 2
130 | PROC_WS1P_MOT_016 | 10129 | 流程异常/运动 | 工位1拉料-送入过程卡料报警，已紧急刹停 | WS1 Pull: Feed Jam, E-Stop Active | 3
131 | PROC_WS1P_MOT_017 | 10130 | 流程异常/运动 | 工位1拉料-送入过程丢料报警，已紧急刹停 | WS1 Pull: Feed Drop, E-Stop Active | 3
132 | PROC_WS1P_MOT_018 | 10131 | 流程异常/运动 | 工位1拉料-送入运动超时 | WS1 Pull: Feed Motion Timeout | 2
133 | PROC_WS1P_CAM_001 | 10132 | 流程异常/相机 | 工位1拉料-扫码失败或校验不合法 | WS1 Pull: Scan/Verify Failed | 2
134 | PROC_WS2P_DATA_001 | 10133 | 流程异常/数据 | 工位2拉料-配方参数为空 | WS2 Pull: Recipe Params Empty | 2
135 | PROC_WS2P_INIT_001 | 10134 | 流程异常/初始化 | 工位2拉料-初始化校验失败 | WS2 Pull: Init Check Failed | 2
136 | PROC_WS2P_MOT_001 | 10135 | 流程异常/运动 | 工位2拉料-调整流道尺寸失败 | WS2 Pull: Adjust Track Size Failed | 2
137 | PROC_WS2P_MOT_002 | 10136 | 流程异常/运动 | 工位2拉料-Y轴移动到取料位失败 | WS2 Pull: Y-axis move to pick fail | 2
138 | PROC_WS2P_ACT_001 | 10137 | 流程异常/执行器 | 工位2拉料-关闭夹爪失败（未感应到闭合信号） | WS2 Pull: Close Gripper Failed | 2
139 | PROC_WS2P_MAT_001 | 10138 | 流程异常/物料 | 工位2拉料-检测到叠料异常 | WS2 Pull: Double Material Detected | 2
140 | PROC_WS2P_MOT_003 | 10139 | 流程异常/运动 | 工位2拉料-拉出至检测位失败（运动被中断） | WS2 Pull: Pull to Check Failed | 2
141 | PROC_WS2P_MOT_004 | 10140 | 流程异常/运动 | 工位2拉料-推回至料盒失败（运动被中断） | WS2 Pull: push back fail | 2
142 | PROC_WS2P_ACT_002 | 10141 | 流程异常/执行器 | 工位2拉料-打开夹爪失败 | WS2 Pull: Open Gripper Failed | 2
143 | PROC_WS2P_MOT_005 | 10142 | 流程异常/运动 | 工位2拉料-Y轴退回待机位失败 | WS2 Pull: Y-Axis Return Home Failed | 2
144 | PROC_WS2P_MAT_002 | 10143 | 流程异常/物料 | 工位2拉料-退回安全位后夹爪仍检测到带料 | WS2 Pull: Material Still in Gripper | 2
145 | PROC_WS2P_MOT_006 | 10144 | 流程异常/运动 | 工位2拉料-初始化拉料流程失败（Y轴运动到待机位失败） | WS2 Pull: Init Sequence Failed | 2
146 | PROC_WS2P_MAT_003 | 10145 | 流程异常/物料 | 工位2拉料-轨道有物料，无法执行尺寸切换 | WS2 Pull: track busy, no switch | 2
147 | PROC_WS2P_ACT_003 | 10146 | 流程异常/执行器 | 工位2拉料-尺寸切换气缸IO操作失败 | WS2 Pull: Size Cylinder IO Failed | 2
148 | PROC_WS2P_ACT_004 | 10147 | 流程异常/执行器 | 工位2拉料-尺寸切换气缸动作超时 | WS2 Pull: Size Cylinder Timeout | 2
149 | PROC_WS2P_ACT_005 | 10148 | 流程异常/执行器 | 工位2拉料-夹爪张开气缸操作失败 | WS2 Pull: Gripper Open Cyl Failed | 2
150 | PROC_WS2P_ACT_006 | 10149 | 流程异常/执行器 | 工位2拉料-夹爪张开超时，未感应到张开信号 | WS2 Pull: Gripper Open Timeout | 2
151 | PROC_WS2P_ACT_007 | 10150 | 流程异常/执行器 | 工位2拉料-夹爪闭合气缸操作失败 | WS2 Pull: Gripper Close Cyl Failed | 2
152 | PROC_WS2P_ACT_008 | 10151 | 流程异常/执行器 | 工位2拉料-夹爪闭合超时，未感应到闭合信号 | WS2 Pull: Gripper Close Timeout | 2
153 | PROC_WS2P_SEN_001 | 10152 | 流程异常/传感器 | 工位2拉料-夹爪闭合后未检测到铁环（空夹） | WS2 Pull: No Ring Detected (Empty) | 2
154 | PROC_WS2P_MOT_007 | 10153 | 流程异常/运动 | 工位2拉料-移动到待机位失败 | WS2 Pull: move to standby pos fail | 2
155 | PROC_WS2P_SEN_002 | 10154 | 流程异常/传感器 | 工位2拉料-待机位检测到残留物料 | WS2 Pull: residue at standby | 2
156 | PROC_WS2P_MOT_008 | 10155 | 流程异常/运动 | 工位2拉料-移动到待机位失败（强制复位） | WS2 Pull: standby move fail (RST) | 2
157 | PROC_WS2P_MOT_009 | 10156 | 流程异常/运动 | 工位2拉料-移动到取出安全位置失败 | WS2 Pull: Move to Safe Pos Failed | 2
158 | PROC_WS2P_SEN_003 | 10157 | 流程异常/传感器 | 工位2拉料-卸料后夹爪物料粘连未脱落 | WS2 Pull: Gripper Material Sticking | 2
159 | PROC_WS2P_SEN_004 | 10158 | 流程异常/传感器 | 工位2拉料-8寸晶圆放反 | WS2 Pull: 8" Wafer Placed Reversed | 2
160 | PROC_WS2P_SEN_005 | 10159 | 流程异常/传感器 | 工位2拉料-12寸晶圆放反 | WS2 Pull: 12" Wafer Placed Reversed | 2
161 | PROC_WS2P_MOT_010 | 10160 | 流程异常/运动 | 工位2拉料-移动到取料位置失败 | WS2 Pull: Move to Pick Pos Failed | 2
162 | PROC_WS2P_MOT_011 | 10161 | 流程异常/运动 | 工位2拉料-拉出运动触发失败 | WS2 Pull: Pull Motion Trigger Fail | 2
163 | PROC_WS2P_MOT_012 | 10162 | 流程异常/运动 | 工位2拉料-拉出过程卡料报警，已紧急停止 | WS2 Pull: pull jam, E-stop on | 3
164 | PROC_WS2P_MOT_013 | 10163 | 流程异常/运动 | 工位2拉料-拉出过程丢料报警，已紧急停止 | WS2 Pull: pull drop, E-stop on | 3
165 | PROC_WS2P_MOT_014 | 10164 | 流程异常/运动 | 工位2拉料-Y轴拉出运动超时 | WS2 Pull: Y pull motion timeout | 2
166 | PROC_WS2P_MOT_015 | 10165 | 流程异常/运动 | 工位2拉料-送入运动触发失败 | WS2 Pull: Feed Motion Trigger Fail | 2
167 | PROC_WS2P_MOT_016 | 10166 | 流程异常/运动 | 工位2拉料-送入过程卡料报警，已紧急刹停 | WS2 Pull: feed jam, E-stop on | 3
168 | PROC_WS2P_MOT_017 | 10167 | 流程异常/运动 | 工位2拉料-送入过程丢料报警，已紧急刹停 | WS2 Pull: feed drop, E-stop on | 3
169 | PROC_WS2P_MOT_018 | 10168 | 流程异常/运动 | 工位2拉料-送入运动超时 | WS2 Pull: Feed Motion Timeout | 2
170 | PROC_WS2P_CAM_001 | 10169 | 流程异常/相机 | 工位2拉料-扫码失败或校验不合法 | WS2 Pull: Scan or Verify Failed | 2
171 | PROC_DET_SIG_001 | 10170 | 流程异常/信号 | OCR检测-等待工位检测信号任务池异常中断 | OCR: Task Pool Abnormal Interrupt | 2
172 | PROC_DET_MOT_001 | 10171 | 流程异常/运动 | OCR检测-龙门模组移动到检测位置失败 | OCR: gantry inspect pos move fail | 2
173 | PROC_DET_CAM_001 | 10172 | 流程异常/相机 | OCR检测-相机握手失败（光源或相机掉线） | OCR: Cam Handshake Fail (Light/Cam) | 2
174 | PROC_DET_MOT_002 | 10173 | 流程异常/运动 | OCR检测-相机Z轴无法抬起避位（紧急锁死防撞） | OCR: Z-Axis Lift Fail (Crash Lock) | 2
175 | PROC_DET_DATA_001 | 10174 | 流程异常/数据 | OCR检测-检测数据写入失败 | OCR: Data Write Failed | 2
176 | PROC_DET_MOT_003 | 10175 | 流程异常/运动 | OCR检测-移动到待机位失败（Z轴或XY轴运动失败） | OCR: Move Standby Fail (Z/XY Axis) | 2
177 | PROC_DET_MOT_004 | 10176 | 流程异常/运动 | OCR检测-Z轴移动到安全位置失败 | OCR: Z-Axis Move to Safe Pos Fail | 2
178 | PROC_DET_DATA_002 | 10177 | 流程异常/数据 | OCR检测-工位1配方为空，无法获取目标坐标 | OCR: WS1 Recipe Empty, No Coords | 2
179 | PROC_DET_MOT_005 | 10178 | 流程异常/运动 | OCR检测-移动到工位1轴运动触发失败 | OCR: WS1 Axis Trigger Fail | 2
180 | PROC_DET_CAM_002 | 10179 | 流程异常/相机 | OCR检测-切换到工位1的OCR配方失败 | OCR: Switch to WS1 Recipe Fail | 2
181 | PROC_DET_MOT_006 | 10180 | 流程异常/运动 | OCR检测-移动到工位1 XYZ轴运动超时 | OCR: WS1 XYZ Axis Motion Timeout | 2
182 | PROC_DET_DATA_003 | 10181 | 流程异常/数据 | OCR检测-工位2配方为空，无法获取目标坐标 | OCR: WS2 Recipe Empty, No Coords | 2
183 | PROC_DET_MOT_007 | 10182 | 流程异常/运动 | OCR检测-移动到工位2轴运动触发失败 | OCR: WS2 Axis Trigger Fail | 2
184 | PROC_DET_CAM_003 | 10183 | 流程异常/相机 | OCR检测-切换到工位2的OCR配方失败 | OCR: Switch to WS2 Recipe Fail | 2
185 | PROC_DET_MOT_008 | 10184 | 流程异常/运动 | OCR检测-移动到工位2 XYZ轴运动超时 | OCR: WS2 XYZ Axis Motion Timeout | 2
186 | PROC_DET_CAM_004 | 10185 | 流程异常/相机 | OCR检测-相机拍照触发失败或通讯异常 | OCR: Cam Trigger Fail / Comm Error | 2
187 | PROC_DET_MOT_009 | 10186 | 流程异常/初始化 | OCR检测-初始化Z轴回零失败 | OCR: Z-Axis Homing Fail (Init) | 2
188 | PROC_DET_MOT_010 | 10187 | 流程异常/初始化 | OCR检测-初始化X轴回零失败 | OCR: X-Axis Homing Fail (Init) | 2
189 | PROC_DET_MOT_011 | 10188 | 流程异常/初始化 | OCR检测-初始化Y轴回零失败 | OCR: Y-Axis Homing Fail (Init) | 2
190 | PROC_DET_INIT_001 | 10189 | 流程异常/初始化 | OCR检测-初始化异常 | OCR: Initialization Abnormal | 2
191 | PROC_DATA_MES_001 | 10190 | 流程异常/数据 | 数据模组-MES查询失败 | Data: MES Query Failed | 2
192 | PROC_DATA_REC_001 | 10191 | 流程异常/数据 | 数据模组-配方更新失败 | Data: Recipe Update Failed | 2
193 | PROC_DATA_OCR_001 | 10192 | 流程异常/数据 | 数据模组-OCR校验失败 | Data: OCR Verification Failed | 2
194 | PROC_DATA_DB_001 | 10193 | 流程异常/数据 | 数据模组-数据持久化失败 | Data: Persistence Failed | 2
195 | PROC_DATA_BAT_001 | 10194 | 流程异常/数据 | 数据模组-批次数据不完整 | Data: Batch Data Incomplete | 2
196 | PROC_DATA_MES_002 | 10195 | 流程异常/数据 | 数据模组-MES批次信息更新失败 | Data: MES Batch Update Failed | 2
197 | PROC_DATA_CODE_001 | 10196 | 流程异常/数据 | 数据模组-条码校验失败 | Data: Barcode Verify Failed | 2
198 | PROC_SECS_INIT_001 | 10197 | 流程异常/通讯 | SECS/GEM模组-初始化失败 | SECS/GEM: Init Failed | 2
199 | PROC_SECS_PROT_001 | 10198 | 流程异常/通讯 | SECS/GEM模组-协议处理失败 | SECS/GEM: Protocol Proc Failed | 2
200 | PROC_SECS_SEND_001 | 10199 | 流程异常/通讯 | SECS/GEM模组-消息发送失败 | SECS/GEM: Message Send Failed | 2
201 | PROC_SECS_RECV_001 | 10200 | 流程异常/通讯 | SECS/GEM模组-消息接收超时 | SECS/GEM: Message Receive Timeout | 2
202 | PROC_SECS_CONN_001 | 10201 | 流程异常/通讯 | SECS/GEM模组-连接断开 | SECS/GEM: Connection Lost | 2