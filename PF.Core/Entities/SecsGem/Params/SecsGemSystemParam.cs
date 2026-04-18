using PF.Core.Constants;

namespace PF.Core.Entities.SecsGem.Params
{

    /// <summary>
    /// SecsGem系统参数配置
    /// </summary>
    public class SecsGemSystemParam
    {
        private static string filepath = $"{ConstGlobalParam.ConfigPath}\\SecsGemSysytem.json";

        /// <summary>服务名称</summary>
        public string ServiceName { get; set; } = "SecsGemService";

        /// <summary>
        /// �Զ�����SecsGem����
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// SecsGem����������ʱ����λ�����룩
        /// </summary>
        public int StartupDelayMs { get; set; } = 1000;

        #region ��ʱʱ�����ã���λ�����룩

        /// <summary>
        /// ���������ȴ��ظ������ʱ��
        /// </summary>
        public int T3 { get; set; } = 45_000;


        /// <summary>T4超时时间</summary>
        public int T4 { get; set; } = 10_000;
        /// <summary>
        /// �������ӳ���֮�����Сʱ����
        /// </summary>
        public int T5 { get; set; } = 10_000;
        /// <summary>
        /// ����һ�����ƻỰ�����豸ѡ����̣����ʱ��
        /// </summary>
        public int T6 { get; set; } = 5_000;
        /// <summary>
        /// ָ����TCP���Ӻ�����豸ѡ��Select�����������ʱ��
        /// </summary>
        public int T7 { get; set; } = 10_000;
        /// <summary>
        /// �涨������Ϣʱ�ַ��������ʱ��
        /// </summary>
        public int T8 { get; set; } = 5_000;

        /// <summary>
        /// �������ʱ��
        /// </summary>
        public int BeatInterval { get; set; } = 15_000;


        #endregion ��ʱʱ�����ã���λ�����룩




        /// <summary>
        /// ������dns���ƻ�ip��ַ
        /// </summary>
        public string IPAddress { get; set; } = "127.0.0.1";


        /// <summary>
        /// �������˿ں�
        /// </summary>
        public int Port { get; set; } = 5000;



        /// <summary>
        /// ��̨���
        /// </summary>
        public string DeviceID { get; set; } = "0";


        /// <summary>
        /// �豸����
        /// </summary>
        public string MDLN { get; set; }

        /// <summary>
        /// �����汾��
        /// </summary>
        public string SOFTREV { get; set; } = "V1.0.2";



        /// <summary>
        /// 重置所有参数为默认值
        /// </summary>
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

        /// <summary>
        /// 从文件加载参数配置
        /// </summary>
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


        /// <summary>
        /// 保存参数配置到文件
        /// </summary>
        public void Save(CancellationToken token = default)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filepath, json);
        }
    }
}
