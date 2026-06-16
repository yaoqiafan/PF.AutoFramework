# PF.Modules.SecsGem

PF.AutoFramework SECS/GEM 通信 Prism 模块，提供 SECS/GEM 报文实时监控、手动收发及连接状态管理界面。以插件 DLL 方式加载。

## 功能

- **连接状态监控**：显示与主机（Host）的 HSMS 连接状态（Connected / Selected / Not Connected）
- **报文监控**：实时显示收发的 S/F 报文（十六进制 + 解析文本双栏显示）
- **手动发送**：支持工程师手动构造并发送 S/F 报文
- **变量浏览**：查看当前 SVID / DVVAL / ECID 值
- **事件日志**：记录 Collection Event 触发历史

## 架构说明

本模块仅提供 **UI 层**，通信逻辑由独立 Windows 后台服务 `PF.SecsGem.Service` 实现。主程序与服务通过以下方式交互：

```
主程序（WPF）
    ↓ TCP Socket（IinternalClient）
PF.SecsGem.Service（Windows 服务）
    ↓ SECS/GEM（Secs4Net）
半导体设备主机（Host）
```

## 接入步骤

### 1. 注册 SECS/GEM 服务（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<ISecsGemManager, SecsGemManager>();
containerRegistry.RegisterSingleton<ICommandManager, CommandManager>();
containerRegistry.RegisterSingleton<IinternalClient, InternalClient>();
```

### 2. 定义 SF 命令处理器

```csharp
[SFCommand(Stream = 2, Function = 41)]  // S2F41：Host Command Send
public class HostCommandHandler : ISFCommand
{
    public async Task<SecsMessage> ExecuteAsync(SecsMessage message, CancellationToken token)
    {
        var command = message.SecsItem.Items[0].GetValue<string>();
        // 处理主机命令
        return new SecsMessage(2, 42, "S2F42", SecsItem.L(SecsItem.A("CMDA"), SecsItem.B(0)));
    }
}
```

### 3. 发送事件通知（CE — Collection Event）

```csharp
// 在工站中注入 ISecsGemMessageUpdater
await _secsGemUpdater.TriggerCollectionEventAsync(ceId: 201);  // 批次开始
await _secsGemUpdater.UpdateVariableAsync("SVID_BatchNo", _currentBatch);
```

### 4. 启动 / 停止 Windows 服务

```csharp
// 通过 ServiceControlHelper（PF.CommonTools 提供）
await ServiceControlHelper.StartAsync("PF.SecsGem.Service");
```

## 日志路径

SECS/GEM 通信报文日志：

```
D:\SWLog\SecsGemService\
    ├── 2025\06\16\
    │   ├── SecsGem_08.log   ← 按小时滚动，含完整十六进制报文
```
