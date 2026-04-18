using PF.Core.Entities.SecsGem.Command;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PF.Core.Entities.SecsGem.Params.FormulaParam
{
    /// <summary>
    /// 公式配置，包含主动命令和响应命令字典
    /// </summary>
    public class FormulaConfiguration
    {

        /// <summary>主动命令字典</summary>
        public ConcurrentDictionary<string, SFCommand> IncentiveCommandDictionary { get; set; }=new ConcurrentDictionary<string, SFCommand>();

        /// <summary>响应命令字典</summary>
        public ConcurrentDictionary<string, SFCommand> ResponseCommandDictionary { get; set; } = new ConcurrentDictionary<string, SFCommand>();

    }
}
