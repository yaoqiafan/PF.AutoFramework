using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Recipe
{
    public interface IRecipeService<T> where T : RecipeParamBase
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
        /// 复制配方
        /// </summary>
        /// <param name="RecipeName"></param>
        /// <param name="RecipeParam"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<T> CopyRecipeAsync(string RecipeName, T RecipeParam, CancellationToken token = default);


        /// <summary>
        /// 修改配方名称
        /// </summary>
        /// <param name="RecipeParam"></param>
        /// <param name="NewRecipeName"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> ChangeRecipeNameAsync(T RecipeParam, string NewRecipeName, CancellationToken token = default);



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
    /// <summary>
    /// 配方名称（唯一标识）
    /// </summary>
    public abstract class RecipeParamBase 
    {
        /// <summary>
        /// 配方参数
        /// </summary>
        [Required(ErrorMessage = "程式名称不能为空")] 
        [MinLength(1, ErrorMessage = "程式名称长度不能小于1")]
        public string RecipeName { get; set; }


        /// <summary>
        /// 配方创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 配方修改时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 基础参数校验（验证必填项）
        /// </summary>
        /// <returns>校验结果（true=通过，false=失败）</returns>
        public virtual bool Validate(out string err)
        {
            err = string.Empty;
            // 使用数据注解进行校验
            var validationContext = new ValidationContext(this);
            var validationResults = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
            bool isValid = Validator.TryValidateObject(this, validationContext, validationResults, true);

            if (!isValid)
            {
                foreach (var error in validationResults)
                {
                    err+=($"配方[{RecipeName}]校验失败：{error.ErrorMessage}");
                }
            }
            return isValid;
        }

        /// <summary>
        /// 转换为JSON字符串（用于存储/传输）
        /// </summary>
        /// <returns>JSON格式字符串</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 从JSON字符串加载配方
        /// </summary>
        /// <typeparam name="T">配方子类类型</typeparam>
        /// <param name="json">JSON字符串</param>
        /// <returns>配方对象</returns>
        public static T FromJson<T>(string json) where T : RecipeParamBase
        {
            return JsonSerializer.Deserialize<T>(json);
        }



        /// <summary>
        /// 数据深拷贝（创建一个新的配方对象，属性值与当前对象相同，但引用类型属性会被复制）
        /// </summary>
        /// <returns></returns>
        public abstract RecipeParamBase DeepClone();
        
    }
}
