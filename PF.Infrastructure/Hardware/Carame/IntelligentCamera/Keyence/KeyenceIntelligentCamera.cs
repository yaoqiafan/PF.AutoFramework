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
    /// <summary>
    /// 基恩士智能相机实现
    /// </summary>
    public class KeyenceIntelligentCamera : BaseIntelligentCamera
    {
        /// <summary>
        /// 构造基恩士智能相机
        /// </summary>
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

       

        private string TiggerRec = string.Empty;
        /// <summary>
        /// IP地址
        /// </summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        public override int TiggerPort { get; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
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
                string TiggerStr = "RUN\r\n";
                TiggerRec = string.Empty;
                var rec = await tiggerclient.WaitSentReceiveDataAsync(Encoding.ASCII.GetBytes(TiggerStr), TimeOutMs);

                TiggerRec = Encoding.ASCII.GetString(rec);
                if (!TiggerRec.Contains(TiggerStr))
                {
                    throw new Exception($"基恩士智能相机接收切换运行模式指令返回内容不匹配");
                }


                TiggerStr = $"PL,1,{ProgramID.ToString("X4")}\r\n";
                TiggerRec = string.Empty;
                TiggerRec = string.Empty;
                rec = await tiggerclient.WaitSentReceiveDataAsync(Encoding.ASCII.GetBytes(TiggerStr), TimeOutMs);

                TiggerRec = Encoding.ASCII.GetString(rec);
                if (!TiggerRec.Trim().Contains ("PL"))
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



        /// <summary>
        /// 切换程序
        /// </summary>
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

        /// <summary>
        /// 触发拍照
        /// </summary>
        public async override Task<string> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return "当前设备模拟模式中，触发测试！"; }


                //string TiggerStr = "TRG\r\n";
                string TiggerStr = "TRG\r\n";
                TiggerRec = await GetResult(TiggerStr, TimeOutMs, 1000);
                return TiggerRec;

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected async override Task<bool> InternalConnectAsync(CancellationToken token = default)
        {
            if (IsSimulated) { return true; }

            if (!await tiggerclient.ConnectAsync(IPAdress, TiggerPort, false))
            {
                return false;
            }
           
            return true;
        }

      

        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected async override Task InternalDisconnectAsync()
        {
            if (IsSimulated) { return; }
            await tiggerclient.DisconnectAsync();
        }

        /// <summary>
        /// 内部复位实现
        /// </summary>
        protected async override Task InternalResetAsync(CancellationToken token)
        {
            if (IsSimulated) { return; }
            //await tiggerclient.ReconnectAsync();
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                if (tiggerclient.Status != ClientStatus.Connected && !HasAlarm)
                    RaiseAlarm(AlarmCodes.Hardware.CameraHeartbeatTimeout,
                        $"相机[{DeviceName}]触发端口 TCP 连接中断（{IPAdress}:{TiggerPort}）");
            }
            return Task.CompletedTask;
        }



        /// <summary>
        /// 相机程序列表
        /// </summary>
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
                string TiggerStr = "PR\r\n";
                TiggerRec = string.Empty;
                var rec = await tiggerclient.WaitSentReceiveDataAsync(Encoding.ASCII.GetBytes(TiggerStr), TimeOutMs);

                TiggerRec = Encoding.ASCII.GetString(rec);
                if (!TiggerRec.Contains("PR,"))
                {
                    throw new Exception($"基恩士智能相机返回数据格式错误：返回数据{rec}");
                }
                return TiggerRec.Split(',')[2];
            }
            catch (Exception ex)
            {
                return null;
            }
        }



        /// <summary>
        /// 判断程序是否存在
        /// </summary>
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



        private async Task<string> GetResult(string Send, int TimeOutMs = 5000, int DelayTimeM = 500, CancellationToken token = default)
        {
            var rec = await tiggerclient.WaitSentReceiveDataAsync(Encoding.ASCII.GetBytes(Send), TimeOutMs);
            if (rec == null || !Encoding.ASCII.GetString(rec).Contains(Send.Trim()))
            {
                return null;
            }
            var ocr = await tiggerclient.ReceiveAllDataInTimeWindowAsync(DelayTimeM);
            if (ocr == null)
            {
                return null;
            }
            return Encoding.ASCII.GetString(ocr).Trim();
        }


    }
}
