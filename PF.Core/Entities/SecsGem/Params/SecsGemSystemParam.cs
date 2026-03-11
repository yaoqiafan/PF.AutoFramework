using PF.Core.Constants;

namespace PF.Core.Entities.SecsGem.Params
{

    public class SecsGemSystemParam
    {
        private static string filepath = $"{ConstGlobalParam.ConfigPath}\\SecsGemSysytem.json";

        public string ServiceName { get; set; } = "SecsGemService";

        /// <summary>
        /// 自动启动SecsGem服务
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// SecsGem服务启动延时（单位：毫秒）
        /// </summary>
        public int StartupDelayMs { get; set; } = 1000;

        #region 超时时间设置（单位：毫秒）

        /// <summary>
        /// 发送请求后等待回复的最大时间
        /// </summary>
        public int T3 { get; set; } = 45_000;


        public int T4 { get; set; } = 10_000;
        /// <summary>
        /// 两次连接尝试之间的最小时间间隔
        /// </summary>
        public int T5 { get; set; } = 10_000;
        /// <summary>
        /// 限制一个控制会话（如设备选择过程）的最长时间
        /// </summary>
        public int T6 { get; set; } = 5_000;
        /// <summary>
        /// 指建立TCP连接后，完成设备选择（Select操作）的最大时间
        /// </summary>
        public int T7 { get; set; } = 10_000;
        /// <summary>
        /// 规定接收消息时字符间最大间隔时间
        /// </summary>
        public int T8 { get; set; } = 5_000;

        /// <summary>
        /// 心跳间隔时间
        /// </summary>
        public int BeatInterval { get; set; } = 15_000;


        #endregion 超时时间设置（单位：毫秒）




        /// <summary>
        /// 服务器dns名称或ip地址
        /// </summary>
        public string IPAddress { get; set; } = "127.0.0.1";


        /// <summary>
        /// 服务器端口号
        /// </summary>
        public int Port { get; set; } = 5000;



        /// <summary>
        /// 机台编号
        /// </summary>
        public string DeviceID { get; set; } = "0";


        /// <summary>
        /// 设备类型
        /// </summary>
        public string MDLN { get; set; }

        /// <summary>
        /// 软件版本号
        /// </summary>
        public string SOFTREV { get; set; } = "V1.0.2";



        public void Reset()
        {
            this.AutoStart = true;
            this.BeatInterval = 15_000;
            this.DeviceID = "1";
            this.IPAddress = "127.0.0.1";
            this.MDLN = "";
            this.Port = 5000;
            this.ServiceName = "SecsGemService";
            this.SOFTREV = "V1.0.2";
            this.StartupDelayMs = 1000;
            this.T3 = 45_000;
            this.T4 = 10_000;
            this.T5 = 10_000;
            this.T6 = 5_000;
            this.T7 = 10_000;
            this.T8 = 5_000;
            this.Save();
        }

        public async Task<bool> Load(string path = "", CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = filepath;
            }
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var param = System.Text.Json.JsonSerializer.Deserialize<SecsGemSystemParam>(json);
                    if (param != null)
                    {
                        this.AutoStart = param.AutoStart;
                        this.BeatInterval = param.BeatInterval;
                        this.DeviceID = param.DeviceID;
                        this.IPAddress = param.IPAddress;
                        this.MDLN = param.MDLN;
                        this.Port = param.Port;
                        this.ServiceName = param.ServiceName;
                        this.SOFTREV = param.SOFTREV;
                        this.StartupDelayMs = param.StartupDelayMs;
                        this.T3 = param.T3;
                        this.T4 = param.T4;
                        this.T5 = param.T5;
                        this.T6 = param.T6;
                        this.T7 = param.T7;
                        this.T8 = param.T8;
                        return true;
                    }
                }
                else
                {
                    this.Reset();
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }

        }


        public void Save(CancellationToken token = default)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filepath, json);
        }
    }
}
