using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam.Base
{
    public abstract class IDBase
    {
        public IDBase(uint _ID, string _Description)
        {
            ID = _ID;
            Description = _Description;
        }

        public uint ID { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}
