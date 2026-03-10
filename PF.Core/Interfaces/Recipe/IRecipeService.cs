using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Recipe
{
    public interface IRecipeService<T> where T : ReceipeParamBase
    {
        /// <summary>
        /// 获取配方参数
        /// </summary>
        /// <param name="RecipeName">配方名称</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<T> RecipeParam(string RecipeName, CancellationToken token = default);

        /// <summary>
        /// 获取所有配方
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<List<T>> GetAllRecipes(CancellationToken token = default);

        /// <summary>
        /// 配方文件夹路径
        /// </summary>
        string RecipeDirPath { get; }

        /// <summary>
        /// 获取所有配方
        /// </summary>
        List<string> RecipeNames { get; }

        /// <summary>
        /// 写入配方参数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="RecipeName">配方名称</param>
        /// <param name="RecipeParam">配方参数</param>
        ///  <param name="IsCover">是否覆盖</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> RecipeParamWriteAsync(T RecipeParam, bool IsCover = false, CancellationToken token = default);

        /// <summary>
        /// 切换配方
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="RecipeParam">配方参数</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default);


        /// <summary>
        /// 上传配方
        /// </summary>
        /// <param name="RecipeParam">配方参数</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 删除指定配方
        /// </summary>
        /// <param name="RecipeParam">配方参数</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> RecipeDeleteAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 删除指定配方
        /// </summary>
        /// <param name="RecipeName">配方名称</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> RecipeDeleteAsync(string RecipeName, CancellationToken token = default);


        /// <summary>
        /// 下载指定配方到本地
        /// </summary>
        /// <param name="RecipeParam">配方参数</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default);

    }




    /// <summary>
    /// 配方参数基础参数
    /// </summary>
    public  class ReceipeParamBase
    {
        /// <summary>
        /// 配方参数
        /// </summary>
        public string RecipeName { get; set; }

    }
}
