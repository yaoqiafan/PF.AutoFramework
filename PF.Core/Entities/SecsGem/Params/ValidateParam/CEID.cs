using PF.Core.Entities.SecsGem.Params.ValidateParam.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    public class CEID : IDBase
    {
        public CEID(uint _ID, string _Description, uint[] _LinkReportID,string _Key = "")
            : base(_ID, _Description)
        {
            LinkReportID = _LinkReportID;
            Key = _Key;
        }


        public uint[] LinkReportID { get; set; } = Array.Empty<uint>();

        public string Key { get; set; } = string.Empty;
    }
}
