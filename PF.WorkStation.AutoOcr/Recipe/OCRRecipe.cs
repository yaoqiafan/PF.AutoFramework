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
    /// <summary>
    /// OCR配方管理类
    /// </summary>
    public class OCRRecipe<T> : BaseRecipe<T> where T : OCRRecipeParam
    {

        private readonly IHardwareManagerService _hardwareservice;
        /// <summary>
        /// 初始化OCR配方
        /// </summary>
        public OCRRecipe(ILogService logger, IHardwareManagerService hardwareservice) : base(logger: logger)
        {
            _hardwareservice = hardwareservice;
        }

        /// <summary>下载配方</summary>
        public override Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>配方变更时切换OCR相机程序</summary>
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

        /// <summary>更新配方</summary>
        public override Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }
    }
}
