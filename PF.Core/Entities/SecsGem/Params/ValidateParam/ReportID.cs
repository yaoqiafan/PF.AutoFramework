using PF.Core.Entities.SecsGem.Params.ValidateParam.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    /// <summary>
    /// 报告标识
    /// </summary>
    public class ReportID : IDBase
    {
        /// <summary>
        /// 初始化ReportID
        /// </summary>
        public ReportID(uint _ID, string _Description, uint[] _LinkVID)
            : base(_ID, _Description)
        {
            LinkVID = _LinkVID;
        }

        /// <summary>关联的VID列表</summary>
        public uint[] LinkVID { get; set; } = Array.Empty<uint>();
    }
}
