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
            // 超时 ≤0 时 CancellationTokenSource(0) 立即取消，所有指令必然"接收响应超时"——兜底为默认值
            this.TimeOutMs = timeoutms > 0 ? timeoutms : DefaultTimeOutMs;
        }

        private const int DefaultTimeOutMs = 5000;

        /// <summary>
        /// 触发客户端（外部注入）
        /// </summary>
        private readonly IClient _triggerClient;

        /// <summary>
        /// 指令互斥：IClient 的发送锁只覆盖"发"，不覆盖"收"。调试页触发与工站流程并发时，
        /// 两次 ReadAsync 会互相抢对方的响应，必须把"发+收"整体串行化。
        /// </summary>
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);

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
        /// <para>
        /// 通讯失败（超时 / IO 异常 / 对端关闭）时主动断开 TCP：超时后相机的迟到响应会滞留在接收缓冲区，
        /// 若继续复用该连接，下一条指令读到的是上一条的响应（PR 读到 OF 的回包等），全部错位。
        /// 断开后下一条指令会自动重连，拿到干净的流。
        /// </para>
        /// <para>
        /// 底层 IClient 不支持取消，取消时底层 ReadAsync 仍挂在流上；必须等它结束后才释放指令锁，
        /// 否则下一条指令会和这个"孤儿读"并发读流，响应被孤儿读吃掉。
        /// </para>
        /// </summary>
        private async Task<string> SendCommandAsync(string command, CancellationToken token)
        {
            await _commandLock.WaitAsync(token).ConfigureAwait(false);
            Task<byte[]>? inner = null;
            try
            {
                await EnsureClientConnectedAsync().ConfigureAwait(false);

                // 按 \r 结束符收完整报文：单次读取可能只拿到半包（OCR 结果较长或网络分段时）
                inner = _triggerClient.WaitSentReceiveUntilAsync(Encoding.ASCII.GetBytes(command), (byte)'\r', TimeOutMs);
                byte[] rec;
                try
                {
                    rec = await inner.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;   // 交给 finally 等待孤儿读结束
                }
                catch (Exception ex)
                {
                    HardwareLogger.Warn($"[{DeviceName}] 指令 {command.Trim()} 通讯失败，断开连接以丢弃滞留数据: {ex.Message}");
                    await DropConnectionAsync().ConfigureAwait(false);
                    throw;
                }

                if (rec is null || rec.Length == 0)
                {
                    await DropConnectionAsync().ConfigureAwait(false);
                    throw new InvalidOperationException($"基恩士智能相机指令 {command.Trim()} 无响应");
                }

                var resp = Encoding.ASCII.GetString(rec).Trim();
                if (resp.StartsWith("ER,", StringComparison.OrdinalIgnoreCase))
                    throw new KeyenceCommandErrorException($"基恩士智能相机指令 {command.Trim()} 返回错误: {resp}");
                return resp;
            }
            finally
            {
                if (inner != null && !inner.IsCompleted)
                {
                    // 被取消：等底层读结束（最长 TimeOutMs）再放锁；结束后流状态未知，丢弃连接
                    _ = inner.ContinueWith(async _ =>
                    {
                        try { await DropConnectionAsync().ConfigureAwait(false); }
                        finally { _commandLock.Release(); }
                    }, TaskScheduler.Default).Unwrap();
                }
                else
                {
                    _commandLock.Release();
                }
            }
        }

        /// <summary>
        /// 相机明确回复 "ER,..."：通讯本身正常，是指令被相机拒绝（如程序号不存在）。
        /// 与超时/断线等通讯故障区分开，调用方据此判断"不存在"还是"查不了"。
        /// </summary>
        private sealed class KeyenceCommandErrorException : InvalidOperationException
        {
            public KeyenceCommandErrorException(string message) : base(message) { }
        }

        /// <summary>
        /// 确保 TCP 处于"已连接 + 同步收发模式"：
        /// 未连接时重连；若被别处（如 TCP 调试页）以异步接收模式连上，同步收发会一直抛异常，
        /// 相机作为该通道的使用方，断开后按同步模式重连接管。连接前先清理残留的 Error 状态。
        /// </summary>
        private async Task EnsureClientConnectedAsync()
        {
            if (_triggerClient.Status == ClientStatus.Connected)
            {
                if (!_triggerClient.IsAsyncReceiveMode) return;
                HardwareLogger.Warn($"[{DeviceName}] 触发通道被以异步接收模式连接（可能来自 TCP 调试页），断开后按同步模式重连");
            }

            await DropConnectionAsync().ConfigureAwait(false);
            if (!await _triggerClient.ConnectAsync(IPAddress, TriggerPort, false).ConfigureAwait(false))
                throw new InvalidOperationException($"相机[{DeviceName}]TCP 连接失败（{IPAddress}:{TriggerPort}）");
        }

        /// <summary>断开 TCP（吞掉异常）。TCPClient.DisconnectAsync 对 Error/Connected 都会清理 socket，对未连接状态是空操作。</summary>
        private async Task DropConnectionAsync()
        {
            try { await _triggerClient.DisconnectAsync().ConfigureAwait(false); }
            catch (Exception ex) { HardwareLogger.Warn($"[{DeviceName}] 断开 TCP 时异常: {ex.Message}"); }
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

        /// <summary>
        /// 发送 PW 切换程序号（不含 OF 输出格式设置）。
        /// 成功返回 true；相机明确回复 ER（程序号不存在等）返回 false；超时/断线等通讯故障抛异常。
        /// 把"不存在"和"通讯失败"分开，程式探测才不会把网络抖动误判为程式不存在。
        /// </summary>
        private async Task<bool> SendProgramSwitchAsync(int programId, CancellationToken token)
        {
            string resp;
            try
            {
                resp = await SendCommandAsync($"PW,{programId}\r", token);
            }
            catch (KeyenceCommandErrorException ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 相机拒绝切换到程序号 {programId}: {ex.Message}");
                return false;
            }

            if (!resp.StartsWith("PW", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"基恩士智能相机接收切换程式指令返回内容不匹配: {resp}");
            return true;
        }

        private static int ParseProgramId(string programid)
        {
            if (!int.TryParse(programid, out int id) || id < 0 || id > 9999)
                throw new ArgumentException($"切换程序编号错误: {programid}");
            return id;
        }

        /// <summary>切换程序号并恢复 OF 输出格式。任何失败返回 false（取消除外）。</summary>
        private async Task<bool> ChangeProgramID(string programid, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated)
                {
                    _currentProgram = programid;
                    return true;
                }

                int id = ParseProgramId(programid);
                if (!await SendProgramSwitchAsync(id, token))
                    return false;

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
        /// <list type="bullet">
        /// <item>返回 true：程式存在，且已切回原程式</item>
        /// <item>返回 false：相机明确回复 ER（程式不存在），相机未切换</item>
        /// <item>抛 InvalidOperationException：通讯失败，或探测后无法切回原程式（相机停在了别的程式上，
        ///       这种情况不能用 bool 表达，必须让调用方知道相机状态已被改变）</item>
        /// </list>
        /// 只要发出过 PW（含超时这种"不知道切没切"的情况），无论成功、失败还是被取消，
        /// 都会尝试切回原程式；切回不使用调用方的 token，避免取消后相机停在目标程式上。
        /// </summary>
        public async override Task<bool> DetermineProgramExists(object programName, CancellationToken token = default)
        {
            if (IsSimulated) { return true; }

            var name = programName?.ToString() ?? string.Empty;
            if (!name.Contains('_'))
            {
                throw new ArgumentException($"传入的程序名称错误: {name}");
            }
            int targetId = ParseProgramId(name.Split('_')[0]);

            string currentId;
            try
            {
                currentId = await GetCurrentProgramIdAsync(token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"相机[{DeviceName}]探测程式前查询当前程式失败: {ex.Message}", ex);
            }

            // 拿不到原程式号就无法保证能切回，宁可不探测
            if (!int.TryParse(currentId, out int originalId))
                throw new InvalidOperationException($"相机[{DeviceName}]当前程式号无法解析（{currentId}），为避免无法切回，放弃探测");

            if (originalId == targetId) return true;

            bool needRestore = true;   // 发出 PW 之前就置位：超时时相机可能已经切过去了
            Exception? probeError = null;
            try
            {
                if (!await SendProgramSwitchAsync(targetId, token))
                {
                    needRestore = false;   // 相机明确拒绝，未切换
                    return false;
                }

                // 等待相机完成程式加载后再切回（切换需要时间，立刻切回会失败）
                await Task.Delay(3000, token);
                return true;
            }
            catch (Exception ex)
            {
                probeError = ex;
                if (ex is OperationCanceledException) throw;
                throw new InvalidOperationException($"相机[{DeviceName}]探测程式 {targetId} 时通讯失败: {ex.Message}", ex);
            }
            finally
            {
                if (needRestore)
                    await RestoreProgramAsync(currentId, targetId, probeError);
            }
        }

        /// <summary>
        /// 探测后切回原程式：相机加载程式期间会拒绝切换，失败时间隔 1s 重试，共 3 次。
        /// 全部失败抛异常（带上探测阶段的原始异常信息，避免被覆盖后丢失）。
        /// </summary>
        private async Task RestoreProgramAsync(string originalId, int probedId, Exception? probeError)
        {
            const int maxAttempts = 3;
            for (int i = 1; i <= maxAttempts; i++)
            {
                if (await ChangeProgramID(originalId, CancellationToken.None))
                    return;
                if (i < maxAttempts)
                    await Task.Delay(1000);
            }

            var msg = $"相机[{DeviceName}]程式探测后恢复原程式 {originalId} 失败（已重试 {maxAttempts} 次），相机可能停留在 {probedId}";
            if (probeError != null) msg += $"；探测阶段异常: {probeError.Message}";
            HardwareLogger.Error(msg);
            throw new InvalidOperationException(msg, probeError);
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

                // 取最后一段为识别结果，并去掉首尾空白（空格 / \r / \n / \t）：
                // 相机输出格式里字段间可能带空格，结果末尾带回车，不去掉和期望字符串比对会失败
                var res = resp.Split(',');
                return res[res.Length - 1].Trim();
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

            // 关键修复：上一次连接若 TCP 已通但握手（PR/OF）失败，TCPClient 仍停在 Connected，
            // 再调 ConnectAsync 会抛 "Client is already connected"——此后每次重试、每次手动连接都失败，
            // 只能重启软件。连接前先把底层连接清干净。
            await DropConnectionAsync();

            if (!await _triggerClient.ConnectAsync(IPAddress, TriggerPort, false))
            {
                return false;
            }

            try
            {
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

                if (!await ChangeResultFormat(token))
                {
                    HardwareLogger.Warn($"[{DeviceName}] 切换结果输出格式失败");
                    await DropConnectionAsync();
                    return false;
                }
                return true;
            }
            catch
            {
                // 握手阶段任何异常（含取消）都不能把 TCP 留在 Connected 状态
                await DropConnectionAsync();
                throw;
            }
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
        /// 内部复位实现：连接断开时重连并恢复输出格式，再刷新当前程序号。
        /// 注意不能用 IClient.ReconnectAsync：它只在 AutoReconnect=true 时才重连（默认 false），
        /// 否则只做断开——原实现复位后相机反而一直是断的。
        /// </summary>
        protected async override Task InternalResetAsync(CancellationToken token)
        {
            if (IsSimulated) { return; }

            if (_triggerClient.Status != ClientStatus.Connected || _triggerClient.IsAsyncReceiveMode)
            {
                await EnsureClientConnectedAsync().WaitAsync(token);
                if (!await ChangeResultFormat(token))
                    throw new InvalidOperationException($"相机[{DeviceName}]复位重连后切换结果输出格式失败");
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
                    ISheet sheet = wk.GetSheet("IV")
                        ?? throw new InvalidOperationException($"程式备份文件中缺少名为 IV 的工作表: {CamProgramBackUpFilePath}");

                    for (int i = 1; i < sheet.LastRowNum + 1; i++)
                    {
                        // 空行 / 空单元格在 NPOI 中返回 null，原实现遇到一行空行整个列表就变空
                        var row = sheet.GetRow(i);
                        string kk = row?.GetCell(0)?.ToString() ?? "";
                        if (int.TryParse(kk, out int count))
                        {
                            if (ls.FindIndex(x => x.Item1 == count) == -1)
                            {
                                ls.Add((count, row!.GetCell(1)?.ToString() ?? ""));
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
                    throw new InvalidOperationException($"基恩士智能相机切换输出格式指令返回内容不匹配: {resp.Trim()}");
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // 签名是 Task<bool>，调用方都按返回值判断——原实现失败时抛异常，"if (!await ...)" 分支永远走不到
                HardwareLogger.Error($"[{DeviceName}] 切换结果输出格式失败", ex);
                return false;
            }

        }
    }
}
