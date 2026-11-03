# 海康网络摄像机最小 Demo + 原生库精简结论

只调用框架 `HikNetworkCamera` 的五个功能：**连接 → 设备信息 → 抓图到内存 → 抓图落盘 → 断开**。
用于回答两个问题：

1. 最少需要哪些代码？（答案：~180 行，含一个空实现 `ILogService`）
2. 最少需要哪些 HCNetSDK 原生 DLL？（答案见下表，**35 个文件 → 4 个，53.8MB → 10.8MB**）

## 用法

```powershell
dotnet run --project HikCameraMinDemo -- --sim          # 仿真：无硬件跑通全链路（合成 JPEG）
dotnet run --project HikCameraMinDemo --                # 真机连 127.0.0.1:8000（预期登录失败 [7]）
dotnet run --project HikCameraMinDemo -- --ip 192.168.1.64 --user admin --pwd xxx [--ch 0] [--buf 8]
```

> 「预期登录失败」本身就是验证手段：原生库缺失时看到的是 `DllNotFoundException` / Init 失败 /
> 进程崩溃；库齐了才会走到干净的 `[7] 连接设备失败（设备不在线或网络超时）`。

## 现场实测（Run-FieldTest.bat）

`Run-FieldTest.bat` 已挂进 csproj，随 `dotnet publish` 自动带出：

```powershell
dotnet publish HikCameraMinDemo -c Release -r win-x64 --self-contained true -o HikCameraField
```

把 `HikCameraField\` 整目录拷到测试机（无需 .NET/VS/MVS 客户端），双击 `Run-FieldTest.bat`
（或 `Run-FieldTest.bat --ip <IP> --user admin --pwd <密码> [--ch 33]`），脚本自动：

1. ping + 8000 端口连通性检查；
2. 正常抓图测试（前台跑、退出码保真），并行轮询抓**运行期模块快照**——精简验证的核心证据；
3. 缓冲区压力测试（`--buf 1`，观察翻倍重试链路）；
4. 收集环境信息（4 个原生库在场核对）、两份日志、demo 抓图、框架落盘图，打成一个
   `FieldTest_Results_*.zip` —— **把这个 zip 发回即可**。

注意：密码含特殊字符（`^ & ! ' "`）可能破坏 cmd 解析，建议现场临时用纯字母数字密码；
脚本刻意不含"错误密码"用例（海康连错锁号 30 分钟）。脚本必须为 CRLF 行尾（已按此保存）。

## 最小 DLL 集合（登录 + JPEG 抓图路径）

| 文件 | 大小 | 必需性 |
|---|---|---|
| `HCNetSDK.dll` | 1.16 MB | **必须**。框架 P/Invoke 的 8 个函数全部由它导出（dumpbin 已核实），静态导入 HCCore |
| `HCCore.dll` | 3.08 MB | **必须**。HCNetSDK.dll 的静态导入依赖 |
| `libcrypto-3-x64.dll` | 5.33 MB | **建议**。SDK 在 Init/Login 路径主动加载（模块快照实测）；设备开了视频加密时抓图解密要用 |
| `libssl-3-x64.dll` | 1.20 MB | **建议**。同上，静态导入 libcrypto |

**可以删掉的 31 个**：`HCNetSDKCom\` 整目录 16 个 + 根目录 `PlayCtrl/SuperRender/OpenAL32/MP_Render/HXVA/HmMerge/YUVProcess/GdiPlus/AudioProcess/AudioRender/NPQos/hpr/hlog/zlib1/libmmd`——
全部是实时预览(RealPlay)、回放、语音对讲、报警布防、透明通道等**本框架没用的功能**的按需懒加载组件。
模块快照证明：全套在场时它们一个都不会被加载。

## 实验方法（可复现）

1. **基线**：ProjectReference → 框架自动铺全套 35 文件。真机模式连未监听端口 → `[7]` 干净失败 = SDK 加载完整。
2. **减法**：把输出目录的 SDK 文件移走，只留候选集 → 行为仍为 `[7]`。
3. **模块快照**：进程重试期间用 `(Get-Process -Id $pid).Modules` 枚举实际加载模块——
   全套与精简集加载的 SDK 模块**完全一致**（就是上表 4 个）。
4. **导出表**：`dumpbin /exports HCNetSDK.dll` 确认 8 个 P/Invoke 函数都在 HCNetSDK.dll 自身，无转发。
5. **静态导入**：`dumpbin /dependents` 确认 4 文件集闭包只落系统 DLL（WS2_32/ADVAPI32/bcrypt/KERNEL32 等，系统自带）。

## 边界与残留风险

- **真机抓图未实测**（手头无设备）：`NET_DVR_CaptureJPEGPicture_NEW` 是登录会话上的请求-应答，
  不涉预览组件；唯一不确定项是「设备开了视频加密」的场景——4 文件集里的 OpenSSL 就是为此保留的。
- 若未来框架加**实时预览**（NET_DVR_RealPlay），需把 `PlayCtrl.dll`、`SuperRender.dll`
  与 `HCNetSDKCom\` 相关组件加回来（SDK 目录 `DLL\Infrastructure\HCNetSDK\` 仍保留全套 35 个文件）。
- Demo 目录未被框架 slnx 收录，属独立验证工具，可随时删除。
