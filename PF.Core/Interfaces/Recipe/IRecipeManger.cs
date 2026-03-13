using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Recipe
{
    public interface IRecipeManger<T> where T : Recipe.RecipeParamBase
    {


        /// <summary>
        /// 工位配方字典
        /// </summary>
        Dictionary <string ,T> StationRecipeDic { get; }


        /// <summary>
        /// 切换工位配方
        /// </summary>
        /// <param name="StationName">工位名称</param>
        /// <param name="RecipeParam">工位配方参数</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>

        Task<bool> ChangedStationRecipe(string StationName, T RecipeParam, CancellationToken token = default);



        /// <summary>
        /// 获取工位配方
        /// </summary>
        /// <param name="StationName">工位名称</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<T> GetStationRecipe(string StationName, CancellationToken token = default);


        /// <summary>
        /// 序列化配方
        /// </summary>
        /// <returns></returns>
        Task<bool> WriteRecipeManger();





    }
}
