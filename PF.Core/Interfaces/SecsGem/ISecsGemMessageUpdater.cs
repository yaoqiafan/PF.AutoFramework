using PF.Core.Entities.SecsGem.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem
{
    /// <summary>
    /// SecsGem 消息更新器接口
    /// </summary>
    public interface ISecsGemMessageUpdater
    {
        /// <summary>
        /// 更新消息中所有 IsVariableNode 为 true 的节点的值
        /// </summary>
        /// <param name="message">要更新的 SecsGem 消息对象</param>
        void UpdateVariableNodesWithVIDValues(SecsGemMessage message);
    }
}
