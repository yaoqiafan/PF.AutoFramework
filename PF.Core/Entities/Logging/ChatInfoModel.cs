using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Logging
{
    /// <summary>
    /// 聊天信息模型
    /// </summary>
    public class ChatInfoModel
    {
        /// <summary>发送时间</summary>
        public DateTime Time { get; set; }
        /// <summary>消息内容</summary>
        public object Message { get; set; }

        /// <summary>发送者ID</summary>
        public string SenderId { get; set; }

        /// <summary>角色</summary>
        public string Role { get; set; }

        /// <summary>附件</summary>
        public object Enclosure { get; set; }
    }
}
