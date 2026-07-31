using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Camera.IntelligentCamera.Keyence
{
    ///基恩士智能相机SDK
    public class KeyenceIntelligent_IVCamera : BaseIntelligentCamera
    {
        /// <summary>
        /// 构造基恩士IV系列智能相机。IP/端口不再单独传入——直接读取注入客户端的 TargetServerIp/TargetServerPort，
        /// 避免和通讯实例配置出现两份数据源不一致的问题。
        /// </summary>
        /// <param name="triggerClient">触发通道 TCP 客户端（外部注入，未连接状态，已配置好 TargetServerIp/TargetServerPort）</param>
        /// <param name="timeoutms">应用协议超时时间（毫秒）——等待相机响应的业务超时，与底层TCP连接本身无关，因此仍需显式传入</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备显示名称</param>
        /// <param name="isSimulated">是否为模拟模式</param>
        /// <param name="logger">日志服务</param>
        public KeyenceIntelligent_IVCamera(IClient triggerClient, int timeoutms,
            string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            _triggerClient = triggerClient;
            this.TimeOutMs = timeoutms;
        }

        /// <summary>
        /// 触发客户端（外部注入）
        /// </summary>
        private readonly IClient _triggerClient;

        /// <summary>
        /// IP地址（直接读取触发通道客户端的目标地址）
        /// </summary>
        public override string IPAddress => _triggerClient.TargetServerIp;

        /// <summary>
        /// 触发端口（直接读取触发通道客户端的目标端口）
        /// </summary>
        public override int TriggerPort => _triggerClient.TargetServerPort;

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public override int TimeOutMs { get; }

        /// <inheritdoc/>
        public override string CurrentProgram => _currentProgram;

        private string _currentProgram = string.Empty;

        /// <summary>
        /// 相机程序列表（从本地备份目录扫描；失败返回空列表并记录日志，不返回 null）
        /// </summary>
        public override List<string> CameraProgram => GetCamProgramFromFile();

        private static string CamProgramBackUpFilePath =>
            Path.Combine(PF.Core.Constants.ConstGlobalParam.ConfigPath, "IV4Program.xlsx");

        // ── 协议通讯 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 发送 ASCII 指令并返回响应字符串。响应为空或以 "ER," 开头（基恩士错误格式）时抛出异常。
        /// 底层 IClient 不支持取消，通过 WaitAsync(token) 让等待可被取消。
        /// </summary>
        private async Task<string> SendCommandAsync(string command, CancellationToken token)
        {
            var rec = await _triggerClient
                .WaitSentReceiveDataAsync(Encoding.ASCII.GetBytes(command), TimeOutMs)
                .WaitAsync(token);

            if (rec is null || rec.Length == 0)
                throw new InvalidOperationException($"基恩士智能相机指令 {command.Trim()} 无响应");

            var resp = Encoding.ASCII.GetString(rec);
            if (resp.TrimStart().StartsWith("ER,", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"基恩士智能相机指令 {command.Trim()} 返回错误: {resp.Trim()}");
            return resp;
        }



        /// <summary>
        /// 查询当前程序号（PR 指令，响应格式 "PR,{状态},{程序号}"）。
        /// 失败抛出异常。原实现存在两个近似副本（GetProgramID/GetProgramName），
        /// 且校验条件写反（&amp;&amp; 应为 ||），格式错误的响应不会被拒绝——已合并修正。
        /// </summary>
        private async Task<string> GetCurrentProgramIdAsync(CancellationToken token)
        {
            if (IsSimulated) { return "0000"; }

            var resp = await SendCommandAsync("PR\r", token);
            var trimmed = resp.Trim();
            var parts = trimmed.Split(',');
            if (!trimmed.Contains("PR,") || parts.Length < 2)
                throw new InvalidOperationException($"基恩士智能相机 PR 指令返回数据格式错误: {trimmed}");
            return parts[1].Trim();
        }

        // ── 程序管理 ──────────────────────────────────────────────────────────

        private async Task<bool> ChangeProgramID(string programid, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                {
                    _currentProgram = programid;
                    return true;
                }

                if (!int.TryParse(programid, out int ProgramID) || ProgramID < 0 || ProgramID > 9999)
                {
                    throw new ArgumentException($"切换程序编号错误: {programid}");
                }
                var cmd = $"PW,{ProgramID}\r";
                var resp = await SendCommandAsync(cmd, token);
                if (!resp.Trim().Contains("PW"))
                {
                    throw new InvalidOperationException($"基恩士智能相机接收切换程式指令返回内容不匹配: {resp.Trim()}");
                }
                _currentProgram = programid;
                if (!await ChangeResultFormat(token))
                {
                    throw new InvalidOperationException($"基恩士智能相机切换结果输出格式失败");
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 切换程序号 {programid} 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 切换程序。程序名格式 "{程序号}_{名称}"；当前程序号一致时直接返回成功。
        /// </summary>
        public async override Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }

                var name = ProgramNumber?.ToString() ?? string.Empty;
                if (!name.Contains('_'))
                {
                    throw new ArgumentException($"输入的程式名称错误: {name}");
                }
                string id = name.Split('_')[0];

                _currentProgram = await GetCurrentProgramIdAsync(token);

                if (int.TryParse(id, out int target) &&
                    int.TryParse(_currentProgram, out int current) &&
                    target == current)
                {
                    return true;
                }
                return await ChangeProgramID(id, token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 切换程序 {ProgramNumber} 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 判断程序是否存在：尝试切换到目标程序号，等待加载完成后切回原程序。
        /// </summary>
        public async override Task<bool> DetermineProgramExists(object programName, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }

                var name = programName?.ToString() ?? string.Empty;
                if (!name.Contains('_'))
                {
                    throw new ArgumentException($"传入的程序名称错误: {name}");
                }
                string targetId = name.Split('_')[0];
                string currentId = await GetCurrentProgramIdAsync(token);

                if (!await ChangeProgramID(targetId, token))
                {
                    // 切换失败 → 判定程式不存在
                    return false;
                }

                // 等待相机完成程式加载后再切回（切换需要时间，立刻切回会失败）
                await Task.Delay(3000, token);

                if (!await ChangeProgramID(currentId, token))
                {
                    // 探测成功但恢复原程式失败：相机停留在目标程式上，必须显式暴露而非静默成功
                    HardwareLogger.Error($"[{DeviceName}] 程式探测后恢复原程式 {currentId} 失败，相机当前停留在 {targetId}");
                    return false;
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 判断程序 {programName} 是否存在失败", ex);
                return false;
            }
        }

        // ── 触发 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 触发拍照并返回识别结果。失败抛出 InvalidOperationException（原实现返回 null，
        /// 调用方容易漏判空）；取消抛出 OperationCanceledException。
        /// </summary>
        public async override Task<string> Trigger(CancellationToken token = default)
        {
            if (IsSimulated) { return "当前设备模拟模式中，触发测试！"; }

            try
            {
                var resp = await SendCommandAsync("T2\r", token);
                if (!resp.Contains("RT"))
                {
                    throw new InvalidOperationException($"基恩士智能相机触发指令返回内容不匹配: {resp.Trim()}");
                }

                // 触发后在时间窗口内收取识别结果

                var res = resp.Split(',');
                return res[res.Length - 1];
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 触发拍照失败", ex);
                throw new InvalidOperationException($"相机[{DeviceName}]触发失败: {ex.Message}", ex);
            }
        }

        // ── 连接生命周期 ──────────────────────────────────────────────────────

        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected async override Task<bool> InternalConnectAsync(CancellationToken token = default)
        {
            if (IsSimulated) { return true; }

            if (!await _triggerClient.ConnectAsync(IPAddress, TriggerPort, false))
            {
                return false;
            }

            try
            {
                _currentProgram = await GetCurrentProgramIdAsync(token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // TCP 已连通，程序号查询失败不阻断连接流程，仅记录并留空
                HardwareLogger.Warn($"[{DeviceName}] 连接后查询当前程序号失败", ex);
                _currentProgram = string.Empty;
            }
            return true;
        }

        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected async override Task InternalDisconnectAsync()
        {
            if (IsSimulated) { return; }
            await _triggerClient.DisconnectAsync();
        }

        /// <summary>
        /// 内部复位实现：连接断开时重连，并刷新当前程序号。
        /// （原实现为空方法，复位对相机不产生任何效果。）
        /// </summary>
        protected async override Task InternalResetAsync(CancellationToken token)
        {
            if (IsSimulated) { return; }

            if (_triggerClient.Status != ClientStatus.Connected)
            {
                await _triggerClient.ReconnectAsync().WaitAsync(token);
            }

            try
            {
                _currentProgram = await GetCurrentProgramIdAsync(token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 复位后查询当前程序号失败", ex);
            }
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                // 连接类报警：触发端口断连→报警，重连→防抖后自动消除
                bool faulted = _triggerClient.Status != ClientStatus.Connected;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.CameraHeartbeatTimeout,
                    $"相机[{DeviceName}]触发端口 TCP 连接中断（{IPAddress}:{TriggerPort}）");
            }
            return Task.CompletedTask;
        }

        // ── 程式列表扫描 ──────────────────────────────────────────────────────

        /// <summary>
        /// 从基恩士备份目录扫描所有程式名称。
        /// 失败返回空列表（原实现返回 null，UI 直接绑定会抛空引用）。
        /// </summary>
        private List<string> GetCamProgramFromFile()
        {
            try
            {
                if (IsSimulated) { return new List<string>() { "0000_Test" }; }

                var dirinfo = new FileInfo(CamProgramBackUpFilePath);
                if (!dirinfo.Exists)
                {
                    throw new DirectoryNotFoundException($"未找到指定路径的文件夹,文件路径{CamProgramBackUpFilePath}");
                }
                List<(int, string)> ls = new List<(int, string)>();
                using (FileStream fs = new FileStream(CamProgramBackUpFilePath, FileMode.Open, FileAccess.Read))
                {
                    XSSFWorkbook wk = new XSSFWorkbook(fs);
                    ISheet sheet = wk.GetSheet("IV");

                    for (int i = 1; i < sheet.LastRowNum + 1; i++)
                    {
                        string kk = sheet.GetRow(i).GetCell(0).ToString() ?? "";
                        if (int.TryParse(kk, out int count))
                        {
                            if (ls.FindIndex(x => x.Item1 == count) == -1)
                            {
                                ls.Add((count, sheet.GetRow(i).GetCell(1).ToString() ?? ""));
                            }
                        }
                    }
                }
                return ls.Select(x => $"{x.Item1.ToString("D3")}_{x.Item2}").ToList();
            }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 扫描相机程式列表失败", ex);
                return new List<string>();
            }
        }


        /// <summary>
        /// 切换结果输出格式(切换配方后需要调用)
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>

        private async Task<bool> ChangeResultFormat(CancellationToken token = default)
        {

            try
            {
                var resp = await SendCommandAsync($"OF,01\r", token);
                if (!resp.Contains("OF"))
                {
                    throw new InvalidOperationException($"基恩士智能相机触发指令返回内容不匹配: {resp.Trim()}");
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                HardwareLogger.Error($"[{DeviceName}] 切换结果输出格式", ex);
                throw new InvalidOperationException($"相机[{DeviceName}]切换结果输出格式: {ex.Message}", ex);
            }

        }
    }
}
