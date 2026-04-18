using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam.Base
{
    /// <summary>
    /// ID基类，提供ID、描述和注释属性
    /// </summary>
    public abstract class IDBase
    {
        /// <summary>
        /// 初始化IDBase
        /// </summary>
        public IDBase(uint _ID, string _Description)
        {
            ID = _ID;
            Description = _Description;
        }

        /// <summary>ID编号</summary>
        public uint ID { get; set; }
        /// <summary>描述</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>注释</summary>
        public string Comment { get; set; } = string.Empty;
    }
}
