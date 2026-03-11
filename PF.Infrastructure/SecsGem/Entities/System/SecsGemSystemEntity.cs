
using PF.Infrastructure.SecsGem.Entities.Basic;
using PF.Infrastructure.SecsGem.Entities.Variable;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PF.Core.Entities.SecsGem.Params;

namespace PF.Infrastructure.SecsGem.Entities.System
{
    public class SecsGemSystemEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

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

    }

    public static class SecsGemSystemExtend
    {
        public static SecsGemSystemEntity GetSecsGemSystemEntityFormSecsGemSystem(this SecsGemSystemParam param)
        {
            SecsGemSystemEntity secsGemSystemEntity = new SecsGemSystemEntity();

            secsGemSystemEntity.AutoStart = param.AutoStart;
            secsGemSystemEntity.BeatInterval = param.BeatInterval;
            secsGemSystemEntity.DeviceID = param.DeviceID;
            secsGemSystemEntity.IPAddress = param.IPAddress;
            secsGemSystemEntity.MDLN = param.MDLN;
            secsGemSystemEntity.Port = param.Port;
            secsGemSystemEntity.ServiceName = param.ServiceName;
            secsGemSystemEntity.SOFTREV = param.SOFTREV;
            secsGemSystemEntity.StartupDelayMs = param.StartupDelayMs;
            secsGemSystemEntity.T3 = param.T3;
            secsGemSystemEntity.T4 = param.T4;
            secsGemSystemEntity.T5 = param.T5;
            secsGemSystemEntity.T6 = param.T6;
            secsGemSystemEntity.T7 = param.T7;
            secsGemSystemEntity.T8 = param.T8;
            return secsGemSystemEntity;
        }


        public static SecsGemSystemParam GetSecsGemSystemFormSecsGemSystemEntity(this SecsGemSystemEntity secsGemSystemEntity)
        {
            SecsGemSystemParam param = new SecsGemSystemParam();
            param.AutoStart = secsGemSystemEntity.AutoStart;
            param.BeatInterval = secsGemSystemEntity.BeatInterval;
            param.DeviceID = secsGemSystemEntity.DeviceID;
            param.IPAddress = secsGemSystemEntity.IPAddress;
            param.MDLN = secsGemSystemEntity.MDLN;
            param.Port = secsGemSystemEntity.Port;
            param.ServiceName = secsGemSystemEntity.ServiceName;
            param.SOFTREV = secsGemSystemEntity.SOFTREV;
            param.StartupDelayMs = secsGemSystemEntity.StartupDelayMs;
            param.T3 = secsGemSystemEntity.T3;
            param.T4 = secsGemSystemEntity.T4;
            param.T5 = secsGemSystemEntity.T5;
            param.T6 = secsGemSystemEntity.T6;
            param.T7 = secsGemSystemEntity.T7;
            param.T8 = secsGemSystemEntity.T8;
            return param;
        }

    }

}
