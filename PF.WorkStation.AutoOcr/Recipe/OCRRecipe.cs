using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using PF.Infrastructure.Recipe;
using PF.Services.Hardware;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Recipe
{
    public class OCRRecipe<T> : BaseRecipe<T> where T : OCRRecipeParam
    {

        private readonly IHardwareManagerService _hardwareservice;
        public OCRRecipe(ILogService logger, IHardwareManagerService hardwareservice) : base(logger: logger)
        {
            _hardwareservice = hardwareservice;
        }

        public override Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public override async Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default)
        {
            /***切换OCR相机配方***/
            var cam = _hardwareservice.GetDevice(E_Camera.OCR相机.ToString()) as IIntelligentCamera;
            if (!await cam.ChangeProgram(RecipeParam.OCRRecipeName, token))
            {
                return false;
            }
            return true;

        }

        public override Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }
    }
}
