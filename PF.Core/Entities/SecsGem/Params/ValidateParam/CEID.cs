using PF.Core.Entities.SecsGem.Params.ValidateParam.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    /// <summary>
    /// 事件标识（Collection Event ID）
    /// </summary>
    public class CEID : IDBase
    {
        /// <summary>
        /// 初始化CEID
        /// </summary>
        public CEID(uint _ID, string _Description, uint[] _LinkReportID,string _Key = "")
            : base(_ID, _Description)
        {
            LinkReportID = _LinkReportID;
            Key = _Key;
        }

        /// <summary>关联的报告ID列表</summary>
        public uint[] LinkReportID { get; set; } = Array.Empty<uint>();

        /// <summary>键值</summary>
        public string Key { get; set; } = string.Empty;
    }
}
