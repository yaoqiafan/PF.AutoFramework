using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using PF.Infrastructure.Recipe;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Recipe
{
    public class OCRRecipe<T> : BaseRecipe<T>  where T : OCRRecipeParam
    {
        public OCRRecipe(ILogService logger) : base(logger: logger)
        {

        }

        public override Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public override Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }
    }
}
