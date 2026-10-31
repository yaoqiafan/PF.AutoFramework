# 海康 HCNetSDK 摄像头集成方案（登录 + 图像采集）

> 目标：在 **PF.AutoFramework** 中接入海康网络摄像机（IPC/NVR，HCNetSDK 私有协议），先实现**登录**与**图像采集**两项功能。
> 本文是调研结论 + 实施计划。**已于 2026-10-31 按第七节清单实施完成**（仿真验证待真机回归）。

---

## 一、结论先行

1. **不要复用现有的 `IIntelligentCamera` 通道**。该接口语义是"触发一次 → 相机内部跑完算法 → 返回结果字符串"（基恩士 OCR 相机），返回类型是 `Task<string>`，装不下一张图。
2. 正确落点是**在 `PF.Core` 新增一个 `INetworkCamera` 接口 + `PF.Infrastructure` 新增 `HikNetworkCamera` 实现**，与 `IIntelligentCamera` / `ILineScanCamera` 并列。海康线阵相机（`HikLineScanCamera`）已经确立了"同一厂商、不同 SDK、不同语义就单开一条接口"的先例。
3. 拍照结果**返回 `byte[]` JPEG 字节 + 元信息**，参考现有的 `LineScanFrame`（设备层只交原样数据、不做算法、不依赖视觉库）。落盘由调用方决定。
4. **真正的坑不在 API 调用，在原生库部署**：`HCNetSDK.dll` 会按固定目录结构去找 `HCNetSDKCom\` 下的十几个旁路 DLL，少一个就返回含糊错误码。本仓库 `DLL\Infrastructure\` 目前**没有**任何 HCNetSDK 文件，需要新增。

---

## 二、现状调研

### 2.1 仓库里已有的海康资产

| 位置 | 内容 | 是否可复用 |
|------|------|-----------|
| `DLL/Infrastructure/MvCameraControl.Net.dll` | 海康**工业相机** MVS SDK 托管封装（V2 面向对象） | ❌ 另一套 SDK，不能拍 IPC |
| `DLL/Infrastructure/MvCodeReaderSDK.Net.dll` | 海康**读码器** SDK | ❌ 另一套 |
| `HCNetSDK*.dll` | — | ❌ **完全不存在，需新增** |

三套海康 SDK 彼此独立：MVS（工业相机，GenICam）、MvCodeReader（读码器）、HCNetSDK（安防监控，私有协议）。本方案要接的是**第三套**。

### 2.2 现有设备层的接入契约

所有硬件设备实现 `IHardwareDevice`（`PF.Core/Interfaces/Device/Hardware/IHardwareDevice.cs`），继承 `BaseDevice`（`PF.Infrastructure/Hardware/BaseDevice.cs`）后自动获得：

- 3 次重试连接（间隔 2s）、连接/断开互斥（`_lifecycleLock` 防止 SDK 句柄踩踏）
- 健康监控后台循环（默认 1000ms，仿真模式 ×5）、`InternalCheckHealthAsync` 钩子
- 仿真模式拦截（`InternalConnectSimulatedAsync`）
- 报警级联（`RaiseAlarm` / `ClearAlarm` / `UpdateAutoClearableHealth` 带防抖）
- `ConnectionChanged` / `AlarmTriggered` / `HardwareAlarmAutoCleared` 三个事件

子类只需实现 3 个抽象钩子：`InternalConnectAsync` / `InternalDisconnectAsync` / `InternalResetAsync`。

**关键约束（CLAUDE.md 明确）**：禁止在构造函数中解析设备，延迟到 `InternalInitializeAsync`。本方案是单设备自包含，无父子挂载，`IAttachedDevice` 不涉及。

### 2.3 SDK 进程级生命周期：必须做引用计数

`NET_DVR_Init()` / `NET_DVR_Cleanup()` 是**进程级**的。如果每台相机各自在连接时 Init、断开时 Cleanup，先断开的那台会把整个 SDK 关掉，其余设备句柄集体失效。

仓库里已有现成范式：`PF.Infrastructure/Hardware/Vision/Hikvision/MvSdkLifetime.cs` —— 用 `lock` + 引用计数，首台设备 Acquire 时初始化，末台 Release 时反初始化。**照抄这个结构，新建一个 `HCNetSdkLifetime`**。

---

## 三、SDK 侧要点（已从 Demo 与头文件核实）

代码依据：`F:\海康DLL\HCNetSDKV6.1.11.5_build20251204_Win64_ZH\Demo示例\3- C# 开发示例\2-实时预览示例代码二\NVRCsharpDemo\`

### 3.1 需要封装的原生函数（仅 8 个）

| 函数 | 用途 | 备注 |
|------|------|------|
| `NET_DVR_Init()` | 初始化 SDK | 进程级，全局一次 |
| `NET_DVR_Cleanup()` | 反初始化 | 进程级，退出时 |
| `NET_DVR_SetLogToFile(int,string,bool)` | SDK 内部日志 | 便于排查 |
| `NET_DVR_SetConnectTime(uint,uint)` | 连接超时/重试次数 | 建议显式设置 |
| `NET_DVR_SetReconnect(uint,int)` | 断线重连 | 建议显式设置 |
| `NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO, ref NET_DVR_DEVICEINFO_V40)` | **登录** | 推荐，替代 V30 |
| `NET_DVR_Logout(int)` | 注销 | |
| `NET_DVR_CaptureJPEGPicture_NEW(int,int,ref NET_DVR_JPEGPARA,byte[],uint,ref uint)` | **抓图到内存** | 首选；另有 `..._CaptureJPEGPicture` 落盘版 |
| `NET_DVR_GetLastError()` | 错误码 | 必须封装成中文描述 |

Demo 用的是 `NET_DVR_Login_V30`，但 V40 才是当前推荐版本（支持异步登录、TLS、`byVerifyMode` 双因子等）。本方案用 V40。

### 3.2 关键结构体尺寸（**照抄，不要猜**）

```csharp
NET_DVR_DEV_ADDRESS_MAX_LEN   = 129      // sDeviceAddress
NET_DVR_LOGIN_USERNAME_MAX_LEN = 64      // sUserName
NET_DVR_LOGIN_PASSWD_MAX_LEN   = 64      // sPassword
SERIALNO_LEN                   = 48      // sSerialNumber
NET_DVR_DEVICEINFO_V40.byRes2  = 243
NET_DVR_PREVIEWINFO.byStreamID = STREAM_ID_LEN
NET_DVR_PREVIEWINFO.byRes      = 215     // ← 见下，踩过坑
```

`NET_DVR_JPEGPARA`：
```csharp
wPicSize    = 0xFF   // 0xFF = Auto，用当前码流分辨率
wPicQuality = 0      // 0=最好 1=较好 2=一般
```

`NET_DVR_DEVICEINFO_V30` 里本方案需要的字段：
- `byChanNum` / `byStartChan` — 模拟通道数与起始通道号
- `byIPChanNum` + `byHighDChanNum * 256` — 数字通道总数（**高低位要拼**，只取 `byIPChanNum` 会少算 256 的倍数）
- `byStartDChan` — 起始数字通道号（为 0 时回退 `byStartChan + byChanNum`）
- `sSerialNumber` — 序列号（可用于设备唯一标识）

> Demo 里的通道拼接写法：`dwDChanTotalNum = byIPChanNum + 256 * byHighDChanNum`。这是老项目的既有做法。

### 3.3 `DllImport` 写法注意

Demo 的 `CHCNetSDK.cs` 用了**三种**路径写法混着来：

```csharp
[DllImport(@".\HCNetSDK.dll")]                          // 相对当前工作目录
[DllImport(@"HCNetSDK.dll")]                            // 纯文件名，走系统 DLL 搜索路径
[DllImport(@"..\..\..\HCNetSDK\HCNetSDK.dll")]          // ← 绝对不要用这种
```

**本方案统一用 `[DllImport("HCNetSDK.dll")]`（纯文件名）**。理由：

- `.\HCNetSDK.dll` 依赖进程当前工作目录。WPF 用文件对话框选过文件、或用 `Directory.SetCurrentDirectory` 后，工作目录会漂移，`.\` 就找不到文件了 —— 这是极隐蔽的偶发崩溃。
- 纯文件名的解析顺序是：已加载模块 → 应用目录（`AppContext.BaseDirectory`，.NET Core 之后**默认不再包含当前工作目录**）→ 系统目录。稳定。
- `..\..\..\` 是 Demo 作者本机路径，纯属垃圾。

### 3.4 `HCNetSDKCom` 子目录是硬要求

`HCNetSDK.dll` 运行时会去 `<SDK目录>/HCNetSDKCom/` 加载组件 DLL（`HCPreview.dll`、`HCPlayBack.dll`、`HCGeneralCfgMgr.dll` 等）。这个**目录名和相对位置写死在 SDK 内部**，不能挪、不能改。

**体积实测**（`F:\...\库文件\` 下，逐个文件累加）：

| 集合 | 文件数 | 体积 |
|------|--------|------|
| 根目录 DLL | 19 | **35.0 MB** |
| `HCNetSDKCom\` DLL | 16 | **18.8 MB** |
| **运行时合计** | **35** | **53.8 MB** |

> ⚠️ 注意别被目录总大小误导：`库文件` 整个目录是 83 MB，但其中 29 MB 是**不需要部署**的 —— `ClientDemo.exe`（20.4 MB，海康的演示程序）、`ClientDemoDll\`（6.4 MB）、`LocalXml.zip`（1.3 MB）、`.lib` 导入库（0.9 MB，仅 C/C++ 编译期用）。Demo 的 `bin\` 目录显示 98 MB，还多一个 `test.bmp`（14.1 MB，Demo 跑出来的测试图）。

文件清单与逐个体积（KB）：

```
根目录 19 个 —— 35.0 MB
   8070  PlayCtrl.dll          5455  libcrypto-3-x64.dll   3173  libmmd.dll
   3158  HCCore.dll            2203  HXVA.dll              2116  GdiPlus.dll
   2111  NPQos.dll             2093  SuperRender.dll       1309  OpenAL32.dll
   1238  hlog.dll              1230  libssl-3-x64.dll      1188  HCNetSDK.dll
    696  AudioProcess.dll       678  YUVProcess.dll         397  MP_Render.dll
    377  HmMerge.dll            156  AudioRender.dll        148  hpr.dll
     84  zlib1.dll

HCNetSDKCom\ 16 个 —— 18.8 MB
   2770  AudioIntercom.dll     2053  HCGeneralCfgMgr.dll   1621  HCIndustry.dll
   1335  HCVoiceTalk.dll       1309  OpenAL32.dll          1282  HCCoreDevCfg.dll
   1271  HCDisplay.dll         1196  HCAlarm.dll           1107  SystemTransform.dll
   1045  HCPlayBack.dll         965  libiconv2.dll          952  HCPreview.dll
    913  StreamTransClient.dll   711  AnalyzeData.dll        559  AudioProcess.dll
    156  AudioRender.dll
```

> `HCNetSDKCom\` 下另有 `HCAlarm.lib` / `HCGeneralCfgMgr.lib` / `HCPreview.lib` 三个导入库，**不需要部署**（那是给 C/C++ 链接用的）。

### 3.5 能不能精简？——可以，但首版建议不全带

已用 PE 导入表解析出真实的静态依赖链（不是猜的）：

- `HCNetSDK.dll` 静态导入：`HCCore.dll` + 系统库（`WS2_32` / `ADVAPI32` / `RPCRT4` / `WINMM` / `MSWSOCK`）
- `HCNetSDK.dll` 内部字符串引用到的动态加载模块：`HCAlarm` / `HCDisplay` / `HCGeneralCfgMgr` / `HCIndustry` / `HCPlayBack` / `HCPreview` / `HCVoiceTalk` / `StreamTransClient` / `SystemTransform`
- 那几个模块又共同依赖 `HCCore.dll` + `HCCoreDevCfg.dll`

**最小集（登录 + 抓图，10 个 / 16.4 MB）**：

```
HCNetSDK.dll  HCCore.dll  libcrypto-3-x64.dll  libssl-3-x64.dll  zlib1.dll
hlog.dll  hpr.dll
HCNetSDKCom\  HCCoreDevCfg.dll  HCGeneralCfgMgr.dll  StreamTransClient.dll
```

**明确可排除的（22.8 MB，全是预览/播放/音频/渲染/解码链路）**：

```
PlayCtrl.dll(7.9)  libmmd.dll(3.1)  HXVA.dll(2.2)  GdiPlus.dll(2.1)  NPQos.dll(2.1)
SuperRender.dll(2.0)  OpenAL32.dll(1.3)  AudioProcess.dll(0.7)  YUVProcess.dll(0.7)
HmMerge.dll(0.4)  MP_Render.dll(0.4)  AudioRender.dll(0.2)
```

`HCNetSDKCom\` 里非必需 13 个 / 14.6 MB：`AnalyzeData` `AudioIntercom` `AudioProcess` `AudioRender` `HCDisplay` `HCIndustry` `HCPlayBack` `HCPreview` `HCVoiceTalk` `OpenAL32` `libiconv2` `SystemTransform` `HCAlarm`。

> **建议首版带全套 53.8 MB**。理由：`HCNetSDK.dll` 是按需延迟加载，字符串里出现的模块名不等于"只有对应功能才加载"——SDK 初始化时可能就探测这些组件是否存在。精简省下 37 MB，换来的是一类"登录成功但某个操作返回含糊错误码"的难查问题。真要精简，单独跑一轮完整回归（登录、抓图、错误场景、长时间运行）再定。

**架构**：已用 PE 头核实 `HCNetSDK.dll` / `HCCore.dll` 的 Machine = `0x8664`，即 **x64**。而本仓库 `Common.props` 是 `net8.0` + `AnyCPU`（`Common.Desktop.props` 是 `net8.0-windows` + AnyCPU），全库没有任何 `PlatformTarget` / `RuntimeIdentifier` 设置。

> ⚠️ **这是最大的落地风险**：AnyCPU 程序在 64 位 Windows 上默认以 x64 运行，加载 x64 的 `HCNetSDK.dll` 没问题。但如果哪天有人给项目加了 `Prefer32Bit=true` 或改成 x86，会立刻 `BadImageFormatException`。同理，SDK 只有 Win64 版本（本目录名就写着 `_Win64_`），**没有 x86 回退方案**。
>
> 建议在 `PF.Infrastructure.csproj` 或使用方项目的注释里显式记一条部署要求，跟前面对 MVS SDK 的处理方式一致。

---

## 四、老项目的踩坑经验（来自 `AutomaticWeighingSystem`）

该项目 `D:\VSprojects\Work\绿鑫资源\AutomaticWeighingSystem` 已经跑通了 HCNetSDK 的登录、预览、抓图。有价值的几点：

### 坑 1：`NET_DVR_PREVIEWINFO.byRes` 长度必须 = 215

提交 `ef066e7` 的说明明确写了：**"修复实时预览封送崩溃：NET_DVR_PREVIEWINFO.byRes 长度按 SizeConst=215"**。

老项目最初把 `byRes` 写短了，`NET_DVR_RealPlay_V40` 封送时越界，进程直接崩。原因是 `NET_DVR_PREVIEWINFO` 里除了末位的 `byRes[215]`，前面还有 `byStreamID[STREAM_ID_LEN]` 和几个 `byte`/`uint`，总长度算错一点就错位。

**本方案要不要用预览？** 你的需求只有登录 + 抓图，**抓图走 `NET_DVR_CaptureJPEGPicture_NEW`，完全不需要预览、不需要 HWND、不需要 PlayCtrl 解码**。这样可以绕开这个坑，也省掉整个视频渲染链路。

> 这是本方案相对老项目的一个**重要简化**。老项目要实时预览所以必须碰 `PREVIEWINFO`；纯抓图场景不需要。

### 坑 2：错误码必须解码成中文

老项目最初只在日志里打 `GetLastError()` 的数字，现场完全无法定位。后来加了 `AWS.Services/HCNetSDK/HCNetSDKError.cs`（175 行静态字典，覆盖 0~常见错误码），日志变成 `[7] 连接设备失败（设备不在线或网络超时）`。

**本方案直接移植这份字典**（它是独立无依赖的静态类，复制过来即可）。

常用错误码：

| 码 | 含义 | 现场对应动作 |
|----|------|-------------|
| 1 | 用户名或密码错误 | 核对配置 |
| 3 | SDK 未初始化 | `NET_DVR_Init` 没调 / 失败 |
| 4 | 通道号错误 | 通道号算错（高低位拼接问题） |
| 7 | 连接设备失败 | 网络不通 / IP 错 / 相机离线 |
| 43 | 缓冲区太小 | 抓图 buffer 不够（改了分辨率后） |
| 64 | 载入播放库失败 | `PlayCtrl.dll` 缺失（纯抓图不涉及） |
| 89 | 登录密码错误次数过多 | 等锁定期 |
| 96 | IP 地址不匹配 | 相机做了 IP 白名单 |
| 218 | 需要升级设备 | 版本太低 |

完整列表见老项目 `HCNetSDKError.cs`。

### 坑 3：登录失败但仍继续用句柄

老项目 `HikvisionCameraService.Login()` 里先 `if (_userId >= 0) Logout();` 再登录，是对的。但 `CaptureJpegAsync` 里 `if (!IsLoggedIn) return null;` —— **静默返回 null，调用方无法区分"没登录"和"抓图失败"**。

本方案遵循仓库既有约定（`IIntelligentCamera.Trigger` 的注释明确写着"失败抛出异常（不再返回 null）"）：**失败一律抛 `InvalidOperationException`，取消抛 `OperationCanceledException`**。

### 坑 4：抓图缓冲区写死 400KB

老项目 `var buf = new byte[400_000];`。这在 1080P / 4K 摄像头上会直接 `[43] 缓冲区太小`。

`NET_DVR_CaptureJPEGPicture_NEW` 需要调用方预分配缓冲区，**没有"先查询大小"的接口**。正确做法：
- 按配置的分辨率估算（1080P JPEG 高质量约 500KB~1MB，4K 约 2~4MB）
- 或者：先分配较大值（如 8MB），失败且错误码为 43 时自动翻倍重试，上限设 64MB
- **缓冲区大小做成配置项**，默认 8MB

### 坑 5：原生 DLL 靠 `Content CopyToOutputDirectory` 手工逐个列

老项目 `AWS.Shell.csproj` 里手写了 19 + 16 = 35 行 `<Content Include=... Link=...>`（根目录 19 个 + `HCNetSDKCom\` 16 个），用 `Link="HCNetSDKCom\xxx.dll"` 保持子目录层级 —— 这个写法是对的，层级丢了 SDK 就找不到组件。

⚠️ 老项目把 `HCNetSDK/` 目录加进了 `.gitignore`（提交 `45540fe` "将 HCNetSDK/ DLL 目录加入 .gitignore"），所以那份 DLL 是**本地放了但没入库**的 —— 换机器/CI 就会构建失败。

**本仓库的做法应该更规范**：按现有 `PF.Vendor.Infrastructure` 的模式（`None Include` + `Pack` + `CopyToOutputDirectory="PreserveNewest"`），把 HCNetSDK 也纳入供应商资产包统一管理。但这会让包体积增加 53.8 MB，需要决策 —— 见第六节。

### 坑 6：`NET_DVR_Init` 在 Splash 阶段同步调，不检查返回值

老项目 `App.xaml.cs` 里：
```csharp
HCNetSDKApi.NET_DVR_Init();
HCNetSDKApi.NET_DVR_SetLogToFile(3, @"D:\SWLog\HCNetSDK\", true);
```
**没有检查 `NET_DVR_Init` 的 bool 返回值**。初始化失败（比如 DLL 没找到）时，后面每一次登录都会返回 `[-1]` + 错误码 3，但现场看到的是"登录失败"，排查方向完全错。

本方案：`HCNetSdkLifetime.Acquire()` 里检查返回值，失败即抛异常，让 `BaseDevice` 的重试机制接住并报报警。

---

## 五、实施计划

### Phase 0：原生资产与研究（前置）

| 步骤 | 内容 | 产出 |
|------|------|------|
| 0.1 | 从 `F:\海康DLL\HCNetSDKV6.1.11.5_build20251204_Win64_ZH\库文件\` 复制根目录 **19 个** + `HCNetSDKCom\` **16 个** DLL（合计 53.8 MB）到本仓库 `DLL\Infrastructure\HCNetSDK\`。**不要**复制 `ClientDemo.exe` / `ClientDemoDll\` / `LocalXml.zip` / `*.lib` | 目录结构就位 |
| 0.2 | **决策点**：这 53.8 MB 是否入库 / 是否打进 `PF.Vendor.Infrastructure` 包（见第六节） | 决策记录 |
| 0.3 | 决定是否只带抓图必需的最小集合（10 个 / 16.4 MB，见 3.5 节） | 首版建议全带 |

### Phase 1：`PF.Core` — 接口与实体

**新增** `PF.Core/Interfaces/Device/Hardware/Camera/NetworkCamera/INetworkCamera.cs`

```csharp
/// <summary>
/// 网络摄像机（海康 HCNetSDK 私有协议，IPC / NVR）。
/// <para>与 IIntelligentCamera 的区别：智能相机是"触发一次→相机内部跑算法→返回结果字符串"；
/// 网络摄像机吐的是**图像**，算法在上位机侧做。</para>
/// <para>与 ILineScanCamera 的区别：线阵相机吐原始像素流、配合运动轴扫描；
/// 网络摄像机是面阵、单次抓取整幅 JPEG/图片，不感知运动。</para>
/// </summary>
public interface INetworkCamera : IHardwareDevice
{
    /// <summary>设备 IP（不含端口）</summary>
    string IPAddress { get; }

    /// <summary>SDK 私有协议端口，海康默认 8000</summary>
    int Port { get; }

    /// <summary>登录用户名</summary>
    string UserName { get; }

    /// <summary>设备序列号（登录成功后由 NET_DVR_DEVICEINFO_V30.sSerialNumber 填充）</summary>
    string SerialNumber { get; }

    /// <summary>可用通道列表（登录成功后填充；未登录时为空集合）</summary>
    IReadOnlyList<NetworkCameraChannel> Channels { get; }

    /// <summary>当前抓图使用的通道号</summary>
    int ActiveChannel { get; }

    /// <summary>
    /// 抓取一帧 JPEG 图片。
    /// <para>成功返回图片字节（含完整 JPEG 头尾）；失败抛 InvalidOperationException；取消抛 OperationCanceledException。</para>
    /// </summary>
    /// <param name="channel">通道号；null 表示用 <see cref="ActiveChannel"/></param>
    /// <param name="token">取消令牌</param>
    Task<NetworkCameraFrame> CaptureJpegAsync(int? channel = null, CancellationToken token = default);
}
```

**新增** `PF.Core/Entities/Hardware/Vision/NetworkCameraFrame.cs`（对齐 `LineScanFrame` 的设计原则）

```csharp
/// <summary>
/// 网络摄像机抓取的一帧图片。
/// <para>设计约定与 LineScanFrame 一致：设备层只负责把图像原样交出来，
/// 不做算法处理、不依赖任何视觉库。转 Bitmap / 存盘 / 送 HALCON 全由调用方决定。</para>
/// </summary>
public sealed class NetworkCameraFrame
{
    /// <summary>图片字节（JPEG 编码，含完整文件头尾，可直接写盘或送 Bitmap 解码）</summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>实际采集用的通道号</summary>
    public int Channel { get; init; }

    /// <summary>所在设备的序列号</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>字节数</summary>
    public long SizeBytes => Data.LongLength;
}
```

**新增** `PF.Core/Entities/Hardware/Vision/NetworkCameraChannel.cs`

```csharp
/// <summary>网络摄像机的一个通道</summary>
public sealed class NetworkCameraChannel
{
    /// <summary>通道号（SDK 用）</summary>
    public int ChannelNo { get; init; }
    /// <summary>显示名称</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>是否数字（IP）通道</summary>
    public bool IsIpChannel { get; init; }
}
```

> **不放到 `PF.Core/Models`**：仓库里视觉相关实体统一在 `PF.Core/Entities/Hardware/Vision/`，跟 `LineScanFrame` 同位。

**新增报警码** `PF.Core/Constants/AlarmCodes.cs` → `Hardware` 类 → `相机 (CAM)` region 追加：

```csharp
/// <summary>网络摄像机登录失败</summary>
[AlarmInfo("硬件异常", "网络摄像机登录失败", "Network camera login failed", AlarmSeverity.Error,
    "1. 检查相机 IP 与端口(默认8000)是否可达;\n" +
    "2. 核对用户名密码（默认 admin，出厂密码见相机标签）;\n" +
    "3. 查看错误码：1=密码错 7=网络不通 89=密码错误次数过多被锁定 96=IP白名单;\n" +
    "4. 用浏览器访问 http://{IP} 验证相机是否在线;\n" +
    "5. 修正后点击【复位】重新登录;",
    50027, "Network camera login failed",
    "/PF.UI.Infrastructure;component/HardwareImage/相机.png")]
public const string NetworkCameraLoginFailed = "HW_CAM_004";

/// <summary>网络摄像机抓图失败</summary>
[AlarmInfo("硬件异常", "网络摄像机抓图失败", "Network camera capture failed", AlarmSeverity.Error,
    "1. 确认相机已登录（未登录时先复位重新连接）;\n" +
    "2. 检查通道号是否正确（NVR 下通道号不等于 1）;\n" +
    "3. 错误码 43=缓冲区太小，调大抓图缓冲区配置;\n" +
    "4. 错误码 4=通道号错误，检查通道高低位拼接;\n" +
    "5. 确认相机码流正常（用浏览器页面能看到画面）;",
    50028, "Network camera capture failed",
    "/PF.UI.Infrastructure;component/HardwareImage/相机.png")]
public const string NetworkCameraCaptureFailed = "HW_CAM_005";
```

> 已核对：现有报警 ID 最大到 `50026`，`50027` / `50028` 未被占用。

### Phase 2：`PF.Infrastructure` — 实现

目录结构（对齐现有 `Hardware/Camera/` 下的组织方式）：

```
PF.Infrastructure/Hardware/Camera/Network/
├── BaseNetworkCamera.cs                      抽象基类，实现 INetworkCamera
└── Hikvision/
    ├── HikNetworkCamera.cs                   海康实现
    ├── HCNetSdkApi.cs                        原生 P/Invoke 声明（只声明用到的）
    ├── HCNetSdkStructs.cs                    结构体定义（照抄 Demo，勿改尺寸）
    ├── HCNetSdkLifetime.cs                   SDK 引用计数（照 MvSdkLifetime）
    └── HCNetSdkError.cs                      错误码中文映射（移植老项目）
```

**`HCNetSdkLifetime.cs`** — 直接对标 `MvSdkLifetime`：

```csharp
internal static class HCNetSdkLifetime
{
    private static readonly object _gate = new();
    private static int _refCount;

    /// <summary>获取一次 SDK 引用（首次调用时 NET_DVR_Init，失败抛异常）</summary>
    public static void Acquire()
    {
        lock (_gate)
        {
            if (_refCount == 0)
            {
                if (!HCNetSdkApi.NET_DVR_Init())
                    throw new InvalidOperationException(
                        $"HCNetSDK 初始化失败: {HCNetSdkError.GetLastError()}");
            }
            _refCount++;
        }
    }

    /// <summary>释放一次引用（计数归零时 NET_DVR_Cleanup）；允许重复调用</summary>
    public static void Release()
    {
        lock (_gate)
        {
            if (_refCount == 0) return;
            if (--_refCount == 0) HCNetSdkApi.NET_DVR_Cleanup();
        }
    }
}
```

**`HikNetworkCamera.cs`** 核心逻辑：

```csharp
protected override async Task<bool> InternalConnectAsync(CancellationToken token)
{
    if (IsSimulated) return true;

    return await Task.Run(() =>
    {
        HCNetSdkLifetime.Acquire();
        try
        {
            var loginInfo = BuildLoginInfo();      // sDeviceAddress/sUserName/sPassword 按固定长度填 byte[]
            var deviceInfo = new NET_DVR_DEVICEINFO_V40
            {
                byRes2 = new byte[243]             // ← 必须分配，否则封送崩溃
            };

            int userId = HCNetSdkApi.NET_DVR_Login_V40(ref loginInfo, ref deviceInfo);
            if (userId < 0)
            {
                HardwareLogger.Error($"[{DeviceName}] 登录失败 {IPAddress}:{Port} " +
                    HCNetSdkError.GetLastError());
                RaiseAlarm(AlarmCodes.Hardware.NetworkCameraLoginFailed, "网络摄像机登录失败");
                HCNetSdkLifetime.Release();
                return false;
            }

            _userId = userId;
            _serialNumber = ReadFixedString(deviceInfo.struDeviceV30.sSerialNumber);
            _channels = BuildChannelList(deviceInfo.struDeviceV30);
            _activeChannel = _channels.FirstOrDefault()?.ChannelNo ?? 1;
            return true;
        }
        catch
        {
            HCNetSdkLifetime.Release();   // 任何异常都要还引用，否则计数泄漏、SDK 永不 Cleanup
            throw;
        }
    }, token).ConfigureAwait(false);
}
```

**`CaptureJpegAsync` 核心**：

```csharp
public async Task<NetworkCameraFrame> CaptureJpegAsync(int? channel, CancellationToken token)
{
    if (IsSimulated) return BuildSimulatedFrame();

    if (_userId < 0)
        throw new InvalidOperationException($"相机[{DeviceName}]未登录，无法抓图");

    int ch = channel ?? _activeChannel;

    return await Task.Run(() =>
    {
        var para = new NET_DVR_JPEGPARA { wPicSize = 0xFF, wPicQuality = 0 };

        // 缓冲区按配置起始大小分配；错误码 43（缓冲区太小）时翻倍重试
        int size = CaptureBufferBytes;
        for (int attempt = 0; attempt < MaxCaptureRetries; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var buf = new byte[size];
            uint returned = 0;

            if (HCNetSdkApi.NET_DVR_CaptureJPEGPicture_NEW(
                    _userId, ch, ref para, buf, (uint)buf.Length, ref returned))
            {
                if (returned == 0)
                    throw new InvalidOperationException($"相机[{DeviceName}]抓图返回空数据");
                return new NetworkCameraFrame
                {
                    Data = buf[..(int)returned],     // 截断到实际长度
                    Channel = ch,
                    SerialNumber = _serialNumber
                };
            }

            uint err = HCNetSdkApi.NET_DVR_GetLastError();
            if (err != 43)   // 43 = 缓冲区太小
                throw new InvalidOperationException(
                    $"相机[{DeviceName}]抓图失败(通道{ch}): {HCNetSdkError.GetMessage(err)}");

            size *= 2;
            if (size > MaxCaptureBufferBytes)
                throw new InvalidOperationException(
                    $"相机[{DeviceName}]抓图失败: 缓冲区已达上限 {MaxCaptureBufferBytes / 1024 / 1024}MB");
        }
        throw new InvalidOperationException($"相机[{DeviceName}]抓图失败: 重试次数用尽");
    }, token).ConfigureAwait(false);
}
```

**`InternalCheckHealthAsync`** — 复用现有的 `UpdateAutoClearableHealth`：

```csharp
protected override Task InternalCheckHealthAsync(CancellationToken token)
{
    if (!IsSimulated)
    {
        // 未登录 = 连接已断（相机重启/网络中断），触发连接类报警
        bool faulted = _userId < 0;
        UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.CameraHeartbeatTimeout,
            $"相机[{DeviceName}]未登录（{IPAddress}:{Port}），连接可能已断开");
    }
    return Task.CompletedTask;
}
```

> `BaseDevice` 的 `UpdateAutoClearableHealth` 自带 3 次防抖，信号恢复后自动撤销报警，不需要额外处理。

**`InternalResetAsync`** — 先登出再重登：

```csharp
protected override async Task InternalResetAsync(CancellationToken token)
{
    if (IsSimulated) return;
    LogoutInternal();                       // 内部方法，只调 NET_DVR_Logout，不动 SDK 引用计数
    await InternalConnectAsync(token);      // 但这里会 Acquire 一次……
}
```

⚠️ **注意引用计数陷阱**：`InternalResetAsync` 里不能直接复用 `InternalConnectAsync`，否则会多 Acquire 一次。需要在类内拆出 `LoginCoreAsync()`（只管登录，不管 Acquire/Release），由 `InternalConnectAsync` 和 `InternalResetAsync` 各自调用。

**`InternalDisconnectAsync`**：

```csharp
protected override Task InternalDisconnectAsync()
{
    LogoutInternal();
    HCNetSdkLifetime.Release();
    return Task.CompletedTask;
}
```

### Phase 3：DI 注册

**`PF.Application.Shell/App.xaml.cs`** → `RegisterHardwareFactories()` 中，紧跟在 `KeyenceIntelligentCamera` 之后加：

```csharp
// 海康网络摄像机（HCNetSDK 私有协议，IPC/NVR）
// ConnectionParameters：IP / Port(默认8000) / UserName / Password /
//                       Channel(可选，留空用设备枚举的第一个通道) /
//                       CaptureBufferMB(抓图缓冲区起始大小，默认8)
hwManager.RegisterFactory("HikNetworkCamera", cfg =>
{
    cfg.ConnectionParameters.TryGetValue("IP", out var ip);
    int port = cfg.ConnectionParameters.TryGetValue("Port", out var p) ? int.Parse(p) : 8000;
    cfg.ConnectionParameters.TryGetValue("UserName", out var user);
    cfg.ConnectionParameters.TryGetValue("Password", out var pwd);
    int channel = cfg.ConnectionParameters.TryGetValue("Channel", out var ch) ? int.Parse(ch) : 0;
    int bufferMb = cfg.ConnectionParameters.TryGetValue("CaptureBufferMB", out var bm) ? int.Parse(bm) : 8;

    return new Infrastructure.Hardware.Camera.Network.Hikvision.HikNetworkCamera(
        ip, port, user, pwd, channel, bufferMb,
        cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
});
```

**`PF.Application.Shell/CustomConfiguration/Param/DefaultParameters.cs`** → `GetHardwareDefaults()` 里加一条默认（仿真态，无硬件也能在设备树里点开）：

```csharp
HardwareConfig networkCamera = new HardwareConfig
{
    DeviceId = "NetworkCamera_0",
    DeviceName = "海康网络摄像机",
    Category = "Camera",
    ImplementationClassName = "HikNetworkCamera",
    IsSimulated = true,
    IsEnabled = true,
    ParentDeviceId = string.Empty,
    ConnectionParameters = new Dictionary<string, string>
    {
        ["IP"] = "192.168.1.64",
        ["Port"] = "8000",
        ["UserName"] = "admin",
        ["Password"] = "",
        ["Channel"] = "0",
        ["CaptureBufferMB"] = "8"
    },
    Remarks = "海康网络摄像机，IP/端口/账号改到现场实际值后关闭仿真"
};
```

### Phase 4：调试界面

**`PF.Core/Constants/NavigationConstants.cs`** → `Views` 类追加：

```csharp
/// <summary>网络摄像机调试视图</summary>
public const string NetworkCameraDebugView = nameof(NetworkCameraDebugView);
```

**设计要点（已定稿）**：网络摄像机在设备树中作为**独立分组节点**存在，与板卡、采集卡同级；其调试页是"多台摄像机的集合页"——左侧列出本组所有摄像机，右侧为选中那台的参数配置 + 登录/注销 + 预览/采图。

#### 4.1 树结构

```
硬件调试
├─ [卡0] 雷赛控制卡
│   ├─ X轴伺服
│   └─ Y轴伺服
├─ [采集卡] 海康图像采集卡
│   └─ 线阵相机
├─ [网络摄像机]            ← 新增分组，Payload = NetworkCameraGroup
│   ├─ 工位1摄像机          ← Payload = HikNetworkCamera
│   └─ 工位2摄像机
└─ 独立设备
    ├─ 基恩士OCR智能相机
    └─ 康视达光源控制器
```

**新增** `PF.Modules.Debug/Models/NetworkCameraGroup.cs`

> 放在 Debug 模块的 `Models/` 而非 `PF.Core` —— 它只是调试界面的分组载体，不是硬件契约，没有理由污染 Core。

```csharp
/// <summary>
/// 硬件调试树中的「网络摄像机」分组节点载体。
/// <para>分组节点必须带 Payload：HardwareDebugView.xaml.cs 的 TreeView_SelectedItemChanged
/// 只对 Payload != null 的节点发起导航，纯视觉分组点了没反应。</para>
/// </summary>
public sealed class NetworkCameraGroup
{
    public NetworkCameraGroup(IReadOnlyList<INetworkCamera> cameras) => Cameras = cameras;

    /// <summary>本组包含的摄像机（按设备名排序）</summary>
    public IReadOnlyList<INetworkCamera> Cameras { get; }
}
```

#### 4.2 `BuildTree()` 改动

现有第一层只认 `IMotionCard` / `IFrameGrabberCard` 两类宿主，其余进「独立设备」。加一段网络摄像机组，并把它们从 orphans 里排除：

```csharp
// 网络摄像机独立成组：与板卡/采集卡同级，不在「独立设备」里
var networkCameras = allDevices.OfType<INetworkCamera>()
    .OrderBy(c => c.DeviceName)
    .ToList();

if (networkCameras.Count > 0)
{
    var groupNode = new DebugTreeNode
    {
        NodeName = "[网络摄像机]",
        Payload  = new NetworkCameraGroup(networkCameras)
    };
    foreach (var cam in networkCameras)
    {
        groupNode.Children.Add(new DebugTreeNode { NodeName = cam.DeviceName, Payload = cam });
    }
    TreeNodes.Add(groupNode);
}

// ↓ 已有的 orphans 计算里追加排除
var orphans = allDevices
    .Where(d => !hostIds.Contains(d.DeviceId)
             && !attachedIds.Contains(d.DeviceId)
             && d is not INetworkCamera)        // ← 新增
    .OrderBy(d => d.Category.ToString())
    .ThenBy(d => d.DeviceName)
    .ToList();
```

#### 4.3 导航分发

`ExecuteNavigateToDebug` 里加两个分支 —— 分组节点和单台摄像机都进同一个视图，区别只是预选哪台：

```csharp
else if (payload is NetworkCameraGroup group)
{
    parameters.Add("Cameras", group.Cameras);
    RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion,
        NavigationConstants.Views.NetworkCameraDebugView, parameters);
}
else if (payload is INetworkCamera networkCamera)
{
    parameters.Add("Device", networkCamera);
    RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion,
        NavigationConstants.Views.NetworkCameraDebugView, parameters);
}
```

**`NetworkCameraDebugViewModel.OnNavigatedTo`** 两种参数都接：

```csharp
public override void OnNavigatedTo(NavigationContext ctx)
{
    base.OnNavigatedTo(ctx);

    if (ctx.Parameters.ContainsKey("Cameras"))
    {
        // 从分组节点进入 → 展示全部摄像机，默认选第一台
        var all = ctx.Parameters.GetValue<IReadOnlyList<INetworkCamera>>("Cameras");
        Cameras.Clear();
        foreach (var c in all) Cameras.Add(new NetworkCameraItemViewModel(c));
        SelectedCamera = Cameras.FirstOrDefault();
    }
    else if (ctx.Parameters.ContainsKey("Device"))
    {
        // 从单台摄像机节点进入 → 只有这一台
        var cam = ctx.Parameters.GetValue<INetworkCamera>("Device");
        Cameras.Clear();
        var item = new NetworkCameraItemViewModel(cam);
        Cameras.Add(item);
        SelectedCamera = item;
    }
}
```

> `HikNetworkCamera` **不要**加 `[HardwareUI]` 特性。加了会走特性分支，而特性分支在硬编码分发**之前**且直接 `return`，反而绕开上面这段。现有内置设备都走硬编码分发。

#### 4.4 预览：定时抓图轮询（已定方案 P1）

**为什么不用 RealPlay**（三条路的对比）：

| 方案 | 做法 | 结论 |
|------|------|------|
| **P1 定时抓图轮询** ✅ | 每 N ms 调一次 `CaptureJpegPicture_NEW` 喂 `pf:ImageViewer` | **采用**。零新增原生依赖、无 HWND 互操作、风格天然一致。实测约 2~5 fps（取决于相机抓图耗时） |
| P2 回调取流 + PlayCtrl 解码 | `NET_DVR_RealPlay_V40` 传 `REALDATACALLBACK` → `PlayM4_OpenStream`/`InputData` → `PlayM4_GetJPEG` | 真视频，但要接 `PlayCtrl.dll`(7.9 MB)、处理 PS 流解析、管解码端口生命周期，工作量数倍 |
| P3 RealPlay + HWND | `NET_DVR_RealPlay_V40` 传窗口句柄，SDK 直接画 | **不采用**。必须 `WindowsFormsHost` / `HwndHost`——全库从未引过 WinForms；老项目的 `byRes=215` 封送崩溃就出在这条路；原生窗口无法统一风格 |

**轮询实现要点**：

```csharp
private DispatcherTimer? _previewTimer;   // 间隔 = 配置项 PreviewIntervalMs，默认 300
private bool _previewBusy;                // 防重入：上一帧没回来就跳过本轮

private void OnPreviewTick(object? s, EventArgs e)
{
    if (_previewBusy || SelectedCamera?.Camera is not { IsConnected: true } cam) return;

    _previewBusy = true;
    _ = Task.Run(async () =>
    {
        try
        {
            var frame = await cam.CaptureJpegAsync();
            var bmp   = NetworkCameraPreview.TryBuild(frame.Data, out var hint);   // byte[] → BitmapFrame
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PreviewImage = bmp;
                PreviewHint  = bmp == null ? hint : string.Empty;
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => PreviewHint = $"预览中断: {ex.Message}");
        }
        finally { _previewBusy = false; }
    });
}
```

**必须做的三件事**：

1. **`OnNavigatedFrom` 里停掉 timer** —— 不停止会在离开页面后持续向相机发抓图请求，把相机打满，且绑定属性更新到已释放的 VM 上
2. **防重入** —— 相机抓图耗时可能超过轮询间隔（1080P 约 40~100 ms，网络差时更久），不加 `_previewBusy` 会堆积请求
3. **间隔不要太短** —— 200~500 ms 之间取值。低于 150 ms 对多数 IPC 就是持续打码流接口，且帧率收益为零

**新增** `PF.Modules.Debug/Models/NetworkCameraPreview.cs` —— 对标现有的 `LineScanPreview`：

```csharp
/// <summary>
/// 网络摄像机 JPEG 字节 → 预览位图的转换。
/// <para>返回值必须是 BitmapFrame：pf:ImageViewer 的 ImageSource 依赖属性即为该类型，
/// 给普通 BitmapSource 会因类型不匹配被静默丢弃（见 LineScanCameraDebugViewModel 的注释）。</para>
/// </summary>
internal static class NetworkCameraPreview
{
    public static BitmapFrame? TryBuild(byte[] jpeg, out string hint)
    {
        hint = string.Empty;
        if (jpeg is null || jpeg.Length == 0) { hint = "尚未收到图像"; return null; }

        try
        {
            using var ms = new MemoryStream(jpeg);
            var decoder = BitmapDecoder.Create(ms,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = BitmapFrame.Create(decoder.Frames[0]);
            frame.Freeze();   // 冻结后可跨线程安全使用
            return frame;
        }
        catch (Exception ex)
        {
            hint = $"图像解码失败: {ex.Message}";
            return null;
        }
    }
}
```

> `BitmapCacheOption.OnLoad` 是关键：不写的话解码器会懒加载，`MemoryStream` 被 `using` 释放后再渲染就是一片空白。

#### 4.5 界面布局

```
┌─────────────────────────────────────────────────────────────────────────┐
│ [📷] 网络摄像机                                    [刷新] [全部连接]      │
│      设备类别: Camera | 模拟状态: 否 | 共 2 台                            │
├──────────────────┬──────────────────────────────────────────────────────┤
│ 摄像机列表        │  工位1摄像机                                          │
│ ● 工位1摄像机     │ ┌── 连接参数 ───────────────────────────────────────┐ │
│ ○ 工位2摄像机     │ │ IP 地址   [192.168.1.64                    ]      │ │
│                  │ │ 端口      [8000                            ]      │ │
│                  │ │ 用户名    [admin                           ]      │ │
│                  │ │ 密码      [********                        ]      │ │
│                  │ │ 通道号    [0   (0 = 自动选第一个)          ]      │ │
│                  │ │ 抓图缓冲  [8   MB                          ]      │ │
│                  │ │            [保存配置]  ⚠ 需重载后生效             │ │
│                  │ └──────────────────────────────────────────────────┘ │
│                  │ ┌── 状态 ──────────────────────────────────────────┐ │
│                  │ │ ● 已登录   序列号 DS-xxxx   通道 1   1920×1080   │ │
│                  │ │ [登录] [注销] [抓图] [开始预览] [停止预览]        │ │
│                  │ └──────────────────────────────────────────────────┘ │
│                  │ ┌────────────────────────────────────────────────┐ │
│                  │ │                                                │ │
│                  │ │           pf:ImageViewer                       │ │
│                  │ │        （缩放 / 平移 / 适应窗口）                │ │
│                  │ │                                                │ │
│                  │ └────────────────────────────────────────────────┘ │
│                  │   预览中 · 3.2 fps · 抓图 268.4 KB / 42 ms           │
└──────────────────┴──────────────────────────────────────────────────────┘
```

**风格一致性**：沿用 `LineScanCameraDebugView.xaml` 里那套本地资源键 —— `GroupBorder` / `GroupTitle` / `SubTitle` / `FieldLabel` / `FieldBox` / `FieldCombo` / `Hint` / `StatePill`，外壳用 `pf:TransitioningContentControl`（`TransitionMode="Right2LeftWithFade"`），与其余调试页一致。这些样式目前是各页面各自定义的（`LineScanCameraDebugView` 有，`CameraDebugView` 没有），新页面照抄一份同样放在 `<UserControl.Resources>` 里。

**操作日志**：按 `PF.Modules.Debug` 既有约定，`OperationLogKeysDebug` 新增 `NetworkCameraDebugView` 键组，页面根挂 `OperationLog.PageName`，可交互控件挂 `OperationLog.Key`。关键操作（登录/注销/抓图/开始预览/保存配置）默认启用记录，其余默认关闭。

#### 4.6 配置编辑：可改但**不自动重载**

调试页里的 IP / 端口 / 用户名 / 密码 / 通道号 / 抓图缓冲都可以改，点「保存配置」后：

```csharp
SaveConfigCommand = new DelegateCommand(async () =>
{
    if (SelectedCamera?.Camera is not { } cam) return;

    var config = _hardwareManager.GetConfig(cam.DeviceId);
    if (config is null) { SaveStatus = "未找到该设备的配置记录"; return; }

    config.ConnectionParameters["IP"]       = EditIp;
    config.ConnectionParameters["Port"]     = EditPort;
    config.ConnectionParameters["UserName"] = EditUser;
    config.ConnectionParameters["Password"] = EditPassword;
    config.ConnectionParameters["Channel"]  = EditChannel;
    config.ConnectionParameters["CaptureBufferMB"] = EditBufferMb;

    await _hardwareManager.SaveConfigAsync(config);   // 只写库，不重载

    SaveStatus = "已保存。新配置需重新加载硬件后生效（当前连接仍使用旧参数）。";
});
```

**为什么不做自动重载**：`ReloadAllAsync()` 是**全量**的——它会把**所有**设备断开再重新实例化（见 `HardwareManagerService.cs:323`），不只是这一台摄像机。产线运行中触发会打断运动轴、工站、其他相机。串口调试页目前就是"保存即重载"，但那是通讯层，代价小得多；这里不能照抄。

界面上的呈现：
- 保存后按钮旁显示提示文字，**不是**一次性弹窗（用户可能不在屏幕前）
- 顶部加一条黄色提醒横幅：`配置已修改，当前连接仍使用旧参数 —— [重新加载全部硬件]`，那个按钮才调 `ReloadAllAsync()`，且点击前弹确认框说明影响范围
- 只在「有未生效改动」时显示这条横幅

> 对比：`PF.Modules.Parameter` 里的 `HikNetworkCameraParamView`（Phase 5）也编辑同一批字段，但它走的是参数管理页的保存流程（改完弹窗确认 → 存库 → 重载）。两处编辑的是同一份 `HardwareConfig`，不冲突，只是入口不同、重载时机不同。

#### 4.7 新增文件清单

```
PF.Modules.Debug/Models/NetworkCameraGroup.cs         分组节点载体
PF.Modules.Debug/Models/NetworkCameraPreview.cs       JPEG → BitmapFrame
PF.Modules.Debug/ViewModels/NetworkCameraDebugViewModel.cs
PF.Modules.Debug/ViewModels/NetworkCameraItemViewModel.cs   单台摄像机的列表项 + 编辑态
PF.Modules.Debug/Views/Hardware/NetworkCameraDebugView.xaml(.cs)
```

**`PF.Modules.Debug/DebugModule.cs`** → `RegisterTypes` 里加：

```csharp
containerRegistry.RegisterForNavigation<NetworkCameraDebugView, NetworkCameraDebugViewModel>(
    NavigationConstants.Views.NetworkCameraDebugView);
```

**`OperationLogKeysDebug.cs`** → 新增 `NetworkCameraDebugView` 嵌套类（键组）。

### Phase 5：参数配置界面

参照 `HikLineScanCameraParamView` / `Mapper` 的成对模式：

**新增** `PF.Modules.Parameter/ViewModels/Models/Hardware/HikNetworkCameraParamView.cs`

字段（全部按 `[Category]` + `[DisplayName]` 标注）：

| 分类 | 字段 | 对应 ConnectionParameters 键 |
|------|------|------------------------------|
| 设备基本信息 | 设备ID / 设备名称 / 启用 / 仿真模式 / 备注 | — |
| 连接参数 | IP 地址 | `IP` |
| 连接参数 | 端口（默认 8000） | `Port` |
| 连接参数 | 用户名 | `UserName` |
| 连接参数 | 密码 | `Password` |
| 连接参数 | 通道号（0=自动选第一个） | `Channel` |
| 采集参数 | 抓图缓冲区(MB) | `CaptureBufferMB` |

**新增** `PF.Modules.Parameter/Dialog/Mappers/Hardware/HikNetworkCameraParamViewMapper.cs`——结构与 `HikLineScanCameraParamViewMapper` 完全一致（`HasSpecificMapping` 读、`ExtractSpecificData` 写，`ImplementationClassName = "HikNetworkCamera"`，`Category = "Camera"`）。

**`PF.Modules.Parameter/ParameterModule.cs`** → 在 `RegisterHardwareConfigType` 那一串里加一行：

```csharp
ViewFactory.RegisterHardwareConfigType<HikNetworkCameraParamView, HikNetworkCameraParamViewMapper>("HikNetworkCamera");
```

### Phase 6：验证

**仿真模式（无硬件）**：
1. 启动 Shell → 硬件调试树出现「海康网络摄像机」
2. 点开 → 连接成功 → 点抓图 → 返回合成图，界面显示
3. 点「模拟报警」→ 报警级联到主控 → 报警列表出现 `HW_CAM_004`

**真机**：
1. 改 `DefaultParameters` 里的 IP / 账号 / 密码，`IsSimulated = false`
2. 或用参数页改（走 `RegisterHardwareConfigType` 那条路）
3. 连接 → 设备树状态变绿 → 抓图 → 图片正确
4. 拔网线 → 等健康监控（1000ms×3 防抖）→ `HW_CAM_002` 报警
5. 插回网线 → 报警自动清除（`UpdateAutoClearableHealth`）
6. 故意填错密码 → 连接失败 → 日志/报警显示 `[1] 用户名或密码错误`，不是裸数字

**边界**：
- NVR 下的 IP 通道（`byIPChanNum + byHighDChanNum * 256` 拼接是否正确）
- 1080P / 4K 摄像头抓图（验证缓冲区翻倍逻辑）
- 抓图时取消（`token` 是否能中断）

---

## 六、需要你决策的点

### 决策 1：53.8 MB 原生库怎么入库？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **A. 入库 `DLL\Infrastructure\HCNetSDK\`**（推荐） | 与现有 `DLL\Infrastructure\` 模式一致；换机器可复现；不重蹈老项目 `.gitignore` 的覆辙 | 仓库体积 +53.8 MB |
| B. 打进 `PF.Vendor.Infrastructure` 包 | 外部消费项目自动拿到 | 该包会明显变重（+53.8 MB）；且 `HCNetSDKCom\` 的目录层级在 `runtimes\win-x64\native\` 下**无法保持**（见下） |
| C. 不入库，构建脚本从安装目录复制 | 仓库干净 | 换机器/CI 直接构建失败（老项目就是这么翻的） |

> **方案 B 的致命问题**：`HCNetSDK.dll` 强制要求 `HCNetSDKCom\` 子目录存在于**它自己的同级**。而 NuGet 的 `runtimes\<rid>\native\` 资产落地时是**平铺到输出目录根部**的 —— `HCNetSDKCom\` 下的 16 个 DLL 会跟根目录那 19 个混在一起，目录层级丢失，SDK 找不到组件。NuGet 没有"保持子目录"的 native 资产机制。
>
> 而且 `PF.Vendor.Infrastructure` 的 `<None Include>` 是逐个文件列的，加 35 条可行但啰嗦。更麻烦的是 **它是所有框架项目的公共依赖** —— 塞 53.8 MB 进去会让所有下游项目都被迫承担，哪怕根本不用摄像机。

**建议：方案 A**。理由：这是"用到了才需要"的资产，跟 `DLL\HALCON\`（只有 Halcon 模块用）的处理一致；而且 `PF.Infrastructure` 目前的 `MvCameraControl.Net.dll` 也是走 HintPath 就近取用。

### 决策 2：只带必需的原生文件，还是全套 35 个？

- **全套（推荐首版）**：35 个 / **53.8 MB**，避免"某个功能间接触发了没带的组件"这种难查的问题
- **精简**：10 个 / **16.4 MB**（清单见 3.5 节，已按 PE 导入表 + 字符串引用推导）。理论上登录 + 抓图够用，但**需要实测验证**。若走精简，建议单独跑一轮完整回归（登录、抓图、错误场景、长时间运行），确认没有模块加载失败

### 决策 3：预览方式 —— 已定：定时抓图轮询（P1）

调试页需要「预览」功能，三条实现路线的对比见 4.4 节。**已确定采用 P1（定时抓图轮询）**：

- 每 `PreviewIntervalMs`（默认 300 ms）调一次 `CaptureJPEGPicture_NEW`，结果喂 `pf:ImageViewer`
- 帧率约 2~5 fps（取决于相机抓图耗时），用于确认视野/对焦/曝光足够
- **不引入** `WindowsFormsHost` / `HwndHost`、**不需要** HWND、**不需要** `PlayCtrl.dll` 解码链路
- 因此 `NET_DVR_PREVIEWINFO`（及其 `byRes=215` 封送崩溃坑）在本方案中完全不涉及

若后续要升级成真视频流（P2），需要补：`NET_DVR_RealPlay_V40` + `REALDATACALLBACK`、`PlayM4_OpenStream`/`InputData`/`GetJPEG`、解码端口生命周期管理。届时 `PlayCtrl.dll`(7.9 MB) 才需要进部署清单。

### 决策 4：原生库入库方式 —— 已定：本地化入库

已确认采用**方案 A**：全部 35 个原生 DLL（53.8 MB）放入 `DLL\Infrastructure\HCNetSDK\`，随代码库提交。

两个配套动作：

1. **`.gitattributes` 追加** `*.dll binary` —— 现有文件只有 `* text=auto`，虽然是全局规则但让 Git 明确按二进制处理这些 DLL 更稳妥，避免行尾归一化污染二进制内容。
2. **`.gitignore` 不要排除该目录** —— 现有规则只排除 `DLL/PF.UI.Resources.dll`，`DLL/` 下其余内容均正常入库，新增子目录直接生效，无需改动。老项目正是把 `HCNetSDK/` 加进了 `.gitignore` 导致换机器构建失败（提交 `45540fe`），不要重蹈。

---

## 七、给实施工具的操作清单

按顺序执行，每步可独立验证：

1. 从 `F:\海康DLL\HCNetSDKV6.1.11.5_build20251204_Win64_ZH\库文件\` 复制原生 DLL 到 `DLL\Infrastructure\HCNetSDK\`（根目录 **19 个** + `HCNetSDKCom\` **16 个**，保持 `HCNetSDKCom\` 子目录层级。**跳过** `ClientDemo.exe` / `ClientDemoDll\` / `LocalXml.zip` / `*.lib`）
2. `.gitattributes` 追加 `*.dll binary`；确认 `.gitignore` 未排除 `DLL/Infrastructure/HCNetSDK/`；`git add` 该目录
3. `PF.Infrastructure.csproj` 加 `<None Include>` 把上述文件 `CopyToOutputDirectory="PreserveNewest"` 复制到输出目录（**用 `Link="HCNetSDKCom\xxx.dll"` 保持子目录层级**）
4. 新建 `PF.Core/Interfaces/Device/Hardware/Camera/NetworkCamera/INetworkCamera.cs`
5. 新建 `PF.Core/Entities/Hardware/Vision/NetworkCameraFrame.cs` 与 `NetworkCameraChannel.cs`
6. `PF.Core/Constants/AlarmCodes.cs` → `相机 (CAM)` region 追加 `HW_CAM_004` / `HW_CAM_005`（ID 50027 / 50028）
7. 新建 `PF.Infrastructure/Hardware/Camera/Network/BaseNetworkCamera.cs`
8. 新建 `PF.Infrastructure/Hardware/Camera/Network/Hikvision/` 下 5 个文件（Api / Structs / Lifetime / Error / HikNetworkCamera）
9. `App.xaml.cs` → `RegisterHardwareFactories()` 加 `HikNetworkCamera` 工厂
10. `DefaultParameters.cs` → `GetHardwareDefaults()` 加默认配置（仿真态）
11. `NavigationConstants.cs` → `Views` 加 `NetworkCameraDebugView`
12. 新建 `PF.Modules.Debug/Models/NetworkCameraGroup.cs`（分组节点载体）
13. `HardwareDebugViewModel.cs` → `BuildTree()` 加网络摄像机组 + orphans 排除；分发链加 `NetworkCameraGroup` / `INetworkCamera` 两个分支
14. 新建 `PF.Modules.Debug/Models/NetworkCameraPreview.cs`（JPEG → BitmapFrame，注意 `BitmapCacheOption.OnLoad`）
15. 新建 `NetworkCameraDebugView.xaml(.cs)` + `NetworkCameraDebugViewModel.cs` + `NetworkCameraItemViewModel.cs`（左侧列表 + 右侧参数/登录/预览/采图；预览用 `DispatcherTimer` 轮询抓图，`OnNavigatedFrom` 必须停表）
16. `DebugModule.cs` → `RegisterTypes` 加导航注册；`OperationLogKeysDebug.cs` 加键组
17. 新建 `HikNetworkCameraParamView.cs` + `HikNetworkCameraParamViewMapper.cs`
18. `ParameterModule.cs` → 加 `RegisterHardwareConfigType`
19. 构建 → 仿真验证 → 真机验证

**版本号**（按现有约定递增）：
- `PF.Core` 1.0.32 → 1.0.33（新接口 + 新实体 + 新报警码）
- `PF.Infrastructure` 1.0.31 → 1.0.32（新设备实现）
- `PF.Modules.Debug` 1.0.22 → 1.0.23
- `PF.Modules.Parameter` 1.0.10 → 1.0.11
- `PF.AutoFramework.Meta` 1.0.47 → 1.0.48

---

## 八、风险清单

| 风险 | 影响 | 缓解 |
|------|------|------|
| **32/64 位不匹配** | `BadImageFormatException`，整个程序启动失败 | SDK 仅 Win64；确认项目 `PlatformTarget=AnyCPU` 且在 x64 上运行；在 csproj 注释里显式标注 |
| **`HCNetSDKCom\` 目录层级丢失** | 登录成功但抓图/其他功能返回含糊错误 | 用 `Link="HCNetSDKCom\xxx.dll"` 保持层级；构建后检查输出目录 |
| **通道号算错**（NVR） | `[4] 通道号错误` | `byIPChanNum + byHighDChanNum * 256` 高低位拼接；`byStartDChan` 为 0 时回退 |
| **抓图缓冲区太小** | `[43] 缓冲区太小` | 配置项 + 翻倍重试 + 上限；默认 8MB |
| **SDK 引用计数泄漏** | 退出时 `NET_DVR_Cleanup` 不执行，进程残留 | `Acquire` 后的所有异常路径都 `Release`；`InternalResetAsync` 内部拆 `LoginCoreAsync` 避免重复 Acquire |
| **多相机互相踩踏** | 后连的把前一个的 SDK 关了 | `HCNetSdkLifetime` 引用计数（对标 `MvSdkLifetime`） |
| **密码明文存配置** | 安全隐患 | 现状 `HardwareConfig.ConnectionParameters` 就是明文（`KeyenceIntelligentCamera` 的 IP 也是）；如需加密应作为独立议题，不在本方案范围 |
| **`NET_DVR_Init` 失败未检查** | 后续全是"登录失败"，排查方向错 | `Acquire()` 内检查返回值并抛异常 |
| **未做防抖的报警风暴** | 断网时每 1s 报一次 | 用 `UpdateAutoClearableHealth`（3 次防抖） |
| **`[HardwareUI]` 误加** | 调试页导航失败 | `HikNetworkCamera` 不加该特性，走硬编码分发 |
| **分组节点不带 Payload** | 点「网络摄像机」节点无反应（`TreeView_SelectedItemChanged` 只对 `Payload != null` 导航） | 分组挂 `NetworkCameraGroup` 实例作 Payload；分发链加对应分支 |
| **预览定时器未停** | 离开页面后持续抓图打满相机，且向已释放 VM 更新绑定 | `OnNavigatedFrom` 里 `_previewTimer?.Stop()`（光源调试页曾因"timer 从未 Start"踩过同类问题） |
| **预览请求堆积** | 相机抓图慢于轮询间隔时请求排队、界面卡顿 | `_previewBusy` 标志防重入，上一帧未回则跳过本轮 |
| **预览间隔过短** | 持续占满相机码流接口，帧率收益为零 | `PreviewIntervalMs` 默认 300，下限 150 |
| **`BitmapCacheOption` 漏写** | 预览图渲染为空白（MemoryStream 已释放） | `BitmapDecoder.Create(..., BitmapCacheOption.OnLoad)` |
| **调试页保存后全量重载** | `ReloadAllAsync()` 会断开**所有**设备，打断产线运行 | 调试页只 `SaveConfigAsync` 写库不重载；顶部黄条提示 + 手动「重新加载全部硬件」按钮（带确认弹窗） |
| **System32 中存在同名 DLL** | 实际加载了系统目录里的另一版本，行为诡异 | 现已确认本机 System32/SysWOW64 均无 `HCNetSDK.dll`；现场排查用厂商手法：程序运行时尝试删除部署目录的 `HCNetSDK.dll`，**能删掉说明加载的是 System32 那份**，删不掉才是加载了自己的（readme 第 3 条） |

---

## 九、与现有设计的对齐说明

| 框架约定 | 本方案如何遵守 |
|---------|---------------|
| 三层硬件抽象、模板方法 | `HikNetworkCamera : BaseNetworkCamera : BaseDevice` |
| 构造函数禁止解析设备 | 只存配置参数，登录在 `InternalConnectAsync` |
| 失败抛异常不返回 null | `CaptureJpegAsync` 抛 `InvalidOperationException` |
| 取消抛 `OperationCanceledException` | `Task.Run(..., token)` + `token.ThrowIfCancellationRequested()` |
| 仿真模式 | `IsSimulated` 时返回合成图；`BaseDevice` 自动延长健康监控间隔 ×5 |
| 设备层不依赖视觉库 | 只返回 `byte[]`，转 Bitmap 在 UI 层 |
| 报警码元数据反射扫描 | `[AlarmInfo]` 特性 + `AlarmDictionaryService` 自动发现 |
| 参数页通过 Mapper 注册 | `ViewFactory.RegisterHardwareConfigType<TView, TMapper>("key")` |
| 七层单向依赖 | `PF.Core` 只加接口/实体/常量；实现全在 `PF.Infrastructure`；UI 在 `PF.Modules.*` |
| 私有 NuGet 分发 | 原生资产随源码 `DLL\Infrastructure\` 走，与 `DLL\HALCON\` 同一模式 |
| 调试页自动发现 | 走 `HardwareDebugViewModel.ExecuteNavigateToDebug` 硬编码分发（内置设备惯例），**不用** `[HardwareUI]` |
| 图像显示控件 | `pf:ImageViewer`（`ImageSource` 类型为 `BitmapFrame`），与线扫相机/检测模组调试页一致 |
| 调试页样式 | 复用 `LineScanCameraDebugView.xaml` 里那套本地资源键（`GroupBorder`/`GroupTitle`/`FieldBox`/`StatePill` 等）+ `pf:TransitioningContentControl` 外壳 |
| 操作日志 | `OperationLogKeysDebug` 按键组登记，页面根挂 `OperationLog.PageName`、控件挂 `OperationLog.Key` |
| 配置变更粒度 | 结构性配置（IP/账号）走 `SaveConfigAsync` 只写库；重载由用户显式触发，避免 `ReloadAllAsync` 全量断开打断产线 |
| 分组节点（新增概念） | 树中的纯分组（如「独立设备」）本无 Payload；网络摄像机组需要导航到聚合页，故引入 `NetworkCameraGroup` 作 Payload——**仅限调试 UI 层**，不进 `PF.Core` |

---

## 十、参考位置索引

**本项目（PF.AutoFramework）**

| 用途 | 路径 |
|------|------|
| 设备生命周期基类 | `PF.Infrastructure/Hardware/BaseDevice.cs` |
| 硬件设备接口 | `PF.Core/Interfaces/Device/Hardware/IHardwareDevice.cs` |
| 智能相机基类/接口（不要复用） | `PF.Infrastructure/Hardware/Camera/IntelligentCamera/BaseIntelligentCamera.cs` |
| 线阵相机（参考组织方式） | `PF.Infrastructure/Hardware/Camera/LineScan/Hikvision/HikLineScanCamera.cs` |
| SDK 引用计数范式 | `PF.Infrastructure/Hardware/Vision/Hikvision/MvSdkLifetime.cs` |
| 帧实体设计范式 | `PF.Core/Entities/Hardware/Vision/LineScanFrame.cs` |
| 工厂注册 | `PF.Application.Shell/App.xaml.cs` → `RegisterHardwareFactories()` |
| 默认硬件配置 | `PF.Application.Shell/CustomConfiguration/Param/DefaultParameters.cs` |
| 调试页分发 | `PF.Modules.Debug/ViewModels/HardwareDebugViewModel.cs` → `ExecuteNavigateToDebug()` |
| 设备树构建 | 同上 → `BuildTree()`（`ReloadAllAsync` 在 `PF.Services/Hardware/HardwareManagerService.cs:323`） |
| 树点击处理（Payload 判空） | `PF.Modules.Debug/Views/HardwareDebugView.xaml.cs` → `TreeView_SelectedItemChanged` |
| 相机调试页参考 | `PF.Modules.Debug/ViewModels/CameraDebugViewModel.cs` |
| **调试页布局与样式范式** | `PF.Modules.Debug/Views/Hardware/LineScanCameraDebugView.xaml`（本地资源键 + `pf:ImageViewer` + `pf:TransitioningContentControl`） |
| **JPEG/帧 → BitmapFrame 范式** | `PF.Modules.Debug/Models/LineScanPreview.cs`（注意 `BitmapFrame` 类型与 `Freeze()`） |
| 预览定时器生命周期范式 | `PF.Modules.Debug/ViewModels/CameraDebugViewModel.cs`（`DispatcherTimer` + `OnNavigatedTo/From`） |
| 参数页参考 | `PF.Modules.Parameter/ViewModels/Models/Hardware/HikLineScanCameraParamView.cs` + 对应 Mapper |
| 参数页注册 | `PF.Modules.Parameter/ParameterModule.cs` |
| 报警码 | `PF.Core/Constants/AlarmCodes.cs` → `Hardware` → `相机 (CAM)` |
| 第三方资产包 | `PF.Vendor.Infrastructure/PF.Vendor.Infrastructure.csproj` |

**SDK 与 Demo**

| 用途 | 路径 |
|------|------|
| SDK 根 | `F:\海康DLL\HCNetSDKV6.1.11.5_build20251204_Win64_ZH\` |
| 原生库 | `...\库文件\`（根 **19 个** + `HCNetSDKCom\` **16 个**，合计 53.8 MB） |
| 头文件 | `...\头文件\HCNetSDK.h` |
| C# Demo | `...\Demo示例\3- C# 开发示例\2-实时预览示例代码二\NVRCsharpDemo\` |
| Demo 的 P/Invoke 全量声明 | 该目录下 `CHCNetSDK.cs`（1093 KB，2 万行） |
| Demo 的 SDK 调用示例 | 该目录下 `MainWindow.cs` |
| SDK 手册 | `...\开发文档\设备网络SDK使用手册.chm` |

**老项目（踩坑来源）**

| 用途 | 路径 |
|------|------|
| 相机服务实现 | `D:\VSprojects\Work\绿鑫资源\AutomaticWeighingSystem\AWS.Services\HikvisionCameraService.cs`（138 行） |
| P/Invoke 声明 | `...\AWS.Services\HCNetSDK\HCNetSDKApi.cs`（20334 行） |
| **错误码中文映射** | `...\AWS.Services\HCNetSDK\HCNetSDKError.cs`（175 行，**直接移植**） |
| 相机管理界面 | `...\AWS.UI\ViewModels\Camera\CameraManageViewModel.cs` |
| 原生 DLL 拷贝配置 | `...\AWS.Shell\AWS.Shell.csproj`（34 条 `<Content Include>`） |
| SDK 初始化位置 | `...\AWS.Shell\App.xaml.cs` 第 78~79 行 / 第 106 行 |
| 踩坑提交 | `ef066e7`（byRes 封送崩溃）、`dcc1e3b`（错误码中文） |
