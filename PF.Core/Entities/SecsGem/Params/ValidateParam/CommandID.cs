using PF.Core.Entities.SecsGem.Params.ValidateParam.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    /// <summary>
    /// 命令标识
    /// </summary>
    public class CommandID : IDBase
    {
        /// <summary>
        /// 初始化CommandID
        /// </summary>
        public CommandID(uint _ID, string _Description, uint[] _LinkVID, string RCMD,string _Key) : base(_ID, _Description)
        {
            this.LinkVID = _LinkVID;
            this.RCMD = RCMD;
            this.Key = _Key;
        }

        /// <summary>关联的VID列表</summary>
        public uint[] LinkVID { get; set; } = Array.Empty<uint>();

        /// <summary>远程命令名称</summary>
        public string RCMD { get; set; } = string.Empty;

        /// <summary>键值</summary>
        public string Key { get; set; } = string.Empty;
    }
}
