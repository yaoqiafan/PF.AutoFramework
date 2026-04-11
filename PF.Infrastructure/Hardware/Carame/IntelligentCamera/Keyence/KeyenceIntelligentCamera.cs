using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Carame.IntelligentCamera.Keyence
{
    public class KeyenceIntelligentCamera : BaseIntelligentCamera
    {
        public KeyenceIntelligentCamera(string IP, int port, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            this.IPAdress = IP;
            this.TiggerPort = port;
            this.TimeOutMs = timeoutms;
        }

        /// <summary>
        /// 触发客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient tiggerclient = new Communication.TCP.TCPClient();

        private ManualResetEventSlim TiggerEvent = new ManualResetEventSlim(false);

        private string TiggerRec = string.Empty;
        public override string IPAdress { get; }

        public override int TiggerPort { get; }

        public override int TimeOutMs { get; }


        private async Task<bool> ChangeProgramID(string programid, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }

                if (!int.TryParse(programid.ToString(), out int ProgramID) || ProgramID < 0 || ProgramID > 9999)
                {
                    throw new Exception($"切换程序编号错误");
                }
                TiggerEvent.Reset();
                string TiggerStr = "RUN";
                TiggerRec = string.Empty;
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机发送切换运行模式指令失败");
                }
                Task a = Task.Run(() => TiggerEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                Task result = await Task.WhenAny(a, b);
                if (!result.Equals(a))
                {
                    throw new Exception($"基恩士智能相机接收切换运行模式指令返回超时");
                }
                if (!TiggerRec.Equals(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机接收切换运行模式指令返回内容不匹配");
                }

                TiggerEvent.Reset();
                TiggerStr = $"PL,1,{ProgramID.ToString("X4")}";
                TiggerRec = string.Empty;
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机发送切换程式指令失败");
                }
                a = Task.Run(() => TiggerEvent.Wait(), token);
                b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                result = await Task.WhenAny(a, b);
                if (!result.Equals(a))
                {
                    throw new Exception($"基恩士智能相机接收切换程式指令返回超时");
                }
                if (!TiggerRec.Trim().Equals("PL"))
                {
                    throw new Exception($"基恩士智能相机接收切换程式指令返回内容不匹配");
                }
                return true;

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }



        public async override Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }

                if (!ProgramNumber.ToString().Contains("_"))
                {
                    throw new Exception("输入的程式名称错误");
                }
                string id = ProgramNumber.ToString().Split('_')[0];

                return await ChangeProgramID(id, token);

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }

        public async override Task<string> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return "当前设备模拟模式中，触发测试！"; }

                TiggerEvent.Reset();
                string TiggerStr = "TRG";
                TiggerRec = string.Empty;
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机发送触发指令失败");
                }
                Task a = Task.Run(() => TiggerEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                Task result = await Task.WhenAny(a, b);
                if (result.Equals(a))
                {
                    return TiggerRec;
                }
                else
                {
                    throw new Exception($"基恩士智能相机接收触发返回超时");
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        protected async override Task<bool> InternalConnectAsync(CancellationToken token = default)
        {
            if (IsSimulated) { return true; }

            if (!await tiggerclient.ConnectAsync(IPAdress, TiggerPort))
            {
                return false;
            }
            tiggerclient.DataReceived += Tiggerclient_DataReceived;
            return true;
        }

        private void Tiggerclient_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            TiggerRec = Encoding.UTF8.GetString(e.Data);
            TiggerEvent.Set();
        }

        protected async override Task InternalDisconnectAsync()
        {
            if (IsSimulated) { return; }
            await tiggerclient.DisconnectAsync();
        }

        protected async override Task InternalResetAsync(CancellationToken token)
        {
            if (IsSimulated) { return; }
            await tiggerclient.ReconnectAsync();
        }

        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (tiggerclient.Status != ClientStatus.Connected && !HasAlarm)
                RaiseAlarm(AlarmCodes.Hardware.CameraHeartbeatTimeout,
                    $"相机[{DeviceName}]触发端口 TCP 连接中断（{IPAdress}:{TiggerPort}）");
            return Task.CompletedTask;
        }



        public override List<string> CameraProgram => GetCamProgramFromFile();







        private string CamProgramBackUpFilePath => $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments))}\\KEYENCE\\VS";


        /// <summary>
        /// 从文件内获取所有程式列表名称
        /// </summary>
        /// <returns></returns>
        private List<string> GetCamProgramFromFile()
        {
            try
            {
                if (IsSimulated) { return new List<string>() { "0000_Test" }; }
                var dirinfo = new DirectoryInfo(CamProgramBackUpFilePath);
                if (!dirinfo.Exists)
                {
                    throw new Exception($"未找到指定路径的文件夹,文件路径{CamProgramBackUpFilePath}");
                }

                var programdir = new DirectoryInfo($"{dirinfo.GetDirectories()[0].FullName}\\EM\\VS\\Camera\\BackupFiles").GetDirectories();
                var validFolders = programdir.Where(folder =>
                {
                    // 处理文件夹名称不足4位的异常情况
                    if (folder.Name.Length < 4)
                    {
                        return false;
                    }
                    // 提取前四位并校验是否为纯数字
                    string firstFourChars = folder.Name.Substring(0, 4);
                    bool isFourDigits = int.TryParse(firstFourChars, out _);
                    if (!isFourDigits)
                    {
                        return false;
                    }
                    return true;
                }).ToList();
                // 3. 按前四位数字分组，每组取最新修改的文件夹
                var result = validFolders
                    .GroupBy(folder => folder.Name.Substring(0, 4)) // 按前四位数字分组
                    .Select(group => group.OrderByDescending(f => f.LastWriteTime).First()) // 取最新
                    .ToList();
                return result.Select(x => x.Name).ToList();
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }




        private async Task<string> GetProgramName(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return "0000-Test"; }
                TiggerEvent.Reset();
                string TiggerStr = "PR";
                TiggerRec = string.Empty;
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机发送触发指令失败");
                }
                Task a = Task.Run(() => TiggerEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                Task result = await Task.WhenAny(a, b);
                if (!result.Equals(a))
                {
                    throw new Exception("基恩士智能相机接收数据超时");
                }
                string rec = TiggerRec;
                if (rec.Contains("PR,"))
                {
                    return rec.Split(',')[2];
                }
                else
                {
                    throw new Exception($"基恩士智能相机返回数据格式错误：返回数据{rec}");
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }



        public async override Task<bool> DetermineProgramExits(object programName, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }
                if (!programName.ToString().Contains("_"))
                {
                    throw new Exception("传入的程序名称错误");
                }
                string changecamid = programName.ToString().Split('_')[0];
                TiggerEvent.Reset();
                string TiggerStr = "TRG";
                TiggerRec = string.Empty;
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机发送触发指令失败");
                }
                string nowid = await this.GetProgramName(token);
                if (string.IsNullOrEmpty(nowid))
                {
                    throw new Exception($"获取基恩士智能相机当前程序号失败");
                }
                if (!await ChangeProgramID(changecamid))
                {
                    throw new Exception($"设置基恩士智能相机程序号错误");
                }
                await Task.Delay(3000);
                await ChangeProgramID(nowid);
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }
    }
}
