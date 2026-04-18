using PF.Core.Entities.SecsGem.Params;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.System
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class SecsGemSystemEntity : BasicEntity
    {
        /// <summary>
        /// 初始化实例
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ServiceName 服务
        /// </summary>
        public string ServiceName { get; set; } = "SecsGemService";

        /// <summary>自动启动 SecsGem 服务</summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>SecsGem 服务启动延时（毫秒）</summary>
        public int StartupDelayMs { get; set; } = 1000;

        #region 超时时间（毫秒）

        /// <summary>发送请求后等待回复的最大时间</summary>
        public int T3 { get; set; } = 45_000;

        /// <summary>
        /// T4
        /// </summary>
        public int T4 { get; set; } = 10_000;

        /// <summary>两次连接尝试之间的最小时间间隔</summary>
        public int T5 { get; set; } = 10_000;

        /// <summary>控制会话最长时间</summary>
        public int T6 { get; set; } = 5_000;

        /// <summary>TCP 建立后完成设备选择的最大时间</summary>
        public int T7 { get; set; } = 10_000;

        /// <summary>接收消息时字符间最大间隔时间</summary>
        public int T8 { get; set; } = 5_000;

        /// <summary>心跳间隔时间</summary>
        public int BeatInterval { get; set; } = 15_000;

        #endregion

        /// <summary>
        /// AddressIP地址
        /// </summary>
        public string IPAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// Device标识
        /// </summary>
        public string DeviceID { get; set; } = "0";

        /// <summary>
        /// MDLN
        /// </summary>
        public string MDLN { get; set; }

        /// <summary>
        /// SOFTREV
        /// </summary>
        public string SOFTREV { get; set; } = "V1.0.2";
    }

    /// <summary>
    /// SecsGemSystemExtend
    /// </summary>
    public static class SecsGemSystemExtend
    {
        /// <summary>
        /// ToEntity 实体
        /// </summary>
        public static SecsGemSystemEntity ToEntity(this SecsGemSystemParam param)
        {
            return new SecsGemSystemEntity
            {
                AutoStart = param.AutoStart,
                BeatInterval = param.BeatInterval,
                DeviceID = param.DeviceID,
                IPAddress = param.IPAddress,
                MDLN = param.MDLN,
                Port = param.Port,
                ServiceName = param.ServiceName,
                SOFTREV = param.SOFTREV,
                StartupDelayMs = param.StartupDelayMs,
                T3 = param.T3,
                T4 = param.T4,
                T5 = param.T5,
                T6 = param.T6,
                T7 = param.T7,
                T8 = param.T8
            };
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static SecsGemSystemParam ToParam(this SecsGemSystemEntity entity)
        {
            return new SecsGemSystemParam
            {
                AutoStart = entity.AutoStart,
                BeatInterval = entity.BeatInterval,
                DeviceID = entity.DeviceID,
                IPAddress = entity.IPAddress,
                MDLN = entity.MDLN,
                Port = entity.Port,
                ServiceName = entity.ServiceName,
                SOFTREV = entity.SOFTREV,
                StartupDelayMs = entity.StartupDelayMs,
                T3 = entity.T3,
                T4 = entity.T4,
                T5 = entity.T5,
                T6 = entity.T6,
                T7 = entity.T7,
                T8 = entity.T8
            };
        }

        // 保持向后兼容的旧方法名
        /// <summary>
        /// GetSecsGemSystemEntityFormSecsGemSystem 实体
        /// </summary>
        [Obsolete("Use ToEntity() instead")]
        public static SecsGemSystemEntity GetSecsGemSystemEntityFormSecsGemSystem(this SecsGemSystemParam param)
            => param.ToEntity();

        /// <summary>
        /// GetSecsGemSystemFormSecsGemSystemEntity 实体
        /// </summary>
        [Obsolete("Use ToParam() instead")]
        public static SecsGemSystemParam GetSecsGemSystemFormSecsGemSystemEntity(this SecsGemSystemEntity entity)
            => entity.ToParam();
    }
}
