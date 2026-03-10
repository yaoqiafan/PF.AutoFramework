using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Recipe;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Recipe
{
    public class OCRRecipeManger<T> : BaseRecipeManger<T> where T : OCRRecipeParam
    {
        public OCRRecipeManger(ILogService logger) : base(logger: logger)
        {


        }
    }
}
