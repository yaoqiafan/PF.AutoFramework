using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Recipe
{
    /// <summary>
    /// 配方服务接口，提供配方的增删改查、导入导出及应用等基础操作
    /// </summary>
    /// <typeparam name="T">配方参数的具体类型，必须继承自 <see cref="RecipeParamBase"/></typeparam>
    public interface IRecipeService<T> where T : RecipeParamBase
    {
        /// <summary>
        /// 根据配方名称获取指定的配方参数实体
        /// </summary>
        /// <param name="RecipeName">目标配方名称</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>返回匹配的配方参数实例；若未找到则返回 null 或默认值</returns>
        Task<T> RecipeParam(string RecipeName, CancellationToken token = default);

        /// <summary>
        /// 根据 PPID (Process Program ID) 获取指定的配方参数实体
        /// </summary>
        /// <param name="requestedPpid">请求的配方 ID 或名称</param>
        /// <returns>返回匹配的配方参数实例</returns>
        Task<T> RecipeParam(string? requestedPpid);

        /// <summary>
        /// 获取系统中所有的配方参数列表
        /// </summary>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>包含所有配方参数的列表</returns>
        Task<List<T>> GetAllRecipes(CancellationToken token = default);

        /// <summary>
        /// 获取存储配方文件的物理或相对目录路径
        /// </summary>
        string RecipeDirPath { get; }

        /// <summary>
        /// 获取当前系统中所有可用配方的名称集合
        /// </summary>
        List<string> RecipeNames { get; }

        /// <summary>
        /// 将配方参数写入/保存到存储介质（如文件或数据库）中
        /// </summary>
        /// <param name="RecipeParam">要写入的配方参数实例</param>
        /// <param name="IsCover">若存在同名配方，是否允许覆盖（true：覆盖；false：不覆盖并可能抛出异常或返回 false）</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>保存成功返回 true，否则返回 false</returns>
        Task<bool> RecipeParamWriteAsync(T RecipeParam, bool IsCover = false, CancellationToken token = default);

        /// <summary>
        /// 切换当前正在生产/使用的生效配方
        /// </summary>
        /// <param name="RecipeParam">要切换至的目标配方参数实例</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>切换成功返回 true，否则返回 false</returns>
        Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 更新已存在的配方信息（通常用于配方内容的修改保存）
        /// </summary>
        /// <param name="RecipeParam">包含最新数据的配方参数实例</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>更新成功返回 true，否则返回 false</returns>
        Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 删除指定的配方参数记录
        /// </summary>
        /// <param name="RecipeParam">需要删除的配方参数实例</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>删除成功返回 true，否则返回 false</returns>
        Task<bool> RecipeDeleteAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 根据配方名称删除指定的配方记录
        /// </summary>
        /// <param name="RecipeName">需要删除的配方名称</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>删除成功返回 true，否则返回 false</returns>
        Task<bool> RecipeDeleteAsync(string RecipeName, CancellationToken token = default);

        /// <summary>
        /// 复制现有配方，并以新名称另存为一个独立的配方
        /// </summary>
        /// <param name="RecipeName">新配方的名称</param>
        /// <param name="RecipeParam">要被复制的源配方参数实例</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>返回复制生成的新配方参数实例</returns>
        Task<T> CopyRecipeAsync(string RecipeName, T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 修改现有配方的名称
        /// </summary>
        /// <param name="RecipeParam">要修改名称的配方实例</param>
        /// <param name="NewRecipeName">目标新名称</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>修改成功返回 true，否则返回 false</returns>
        Task<bool> ChangeRecipeNameAsync(T RecipeParam, string NewRecipeName, CancellationToken token = default);

        /// <summary>
        /// 将远程或内存中的指定配方下载/保存到本地路径
        /// </summary>
        /// <param name="RecipeParam">需要下载的配方参数实例</param>
        /// <param name="token">取消令牌，用于支持异步操作的取消</param>
        /// <returns>下载/保存成功返回 true，否则返回 false</returns>
        Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default);
    }

    /// <summary>
    /// 配方参数基础抽象类，提供配方的基本属性标识、校验逻辑以及序列化支持
    /// </summary>
    public abstract class RecipeParamBase
    {
        /// <summary>
        /// 获取或设置配方的名称（通常作为配方的唯一标识）
        /// </summary>
        [Required(ErrorMessage = "程式名称不能为空")]
        [MinLength(1, ErrorMessage = "程式名称长度不能小于1")]
        public string RecipeName { get; set; }

        /// <summary>
        /// 获取或设置该配方的创建时间，默认为实例创建时的系统时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 获取或设置该配方的最后修改时间，默认为实例创建时的系统时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 基础参数校验（利用 DataAnnotations 验证必填项、长度等规则）
        /// </summary>
        /// <param name="err">输出参数：如果校验失败，返回具体的错误信息拼接字符串；成功则为空</param>
        /// <returns>校验结果（true=全部规则通过，false=存在验证失败的规则）</returns>
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
                    err += ($"配方[{RecipeName}]校验失败：{error.ErrorMessage}\n");
                }
            }
            return isValid;
        }

        /// <summary>
        /// 将当前配方对象序列化为包含缩进格式的 JSON 字符串（常用于文件存储或网络传输）
        /// </summary>
        /// <returns>格式化后的 JSON 字符串</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 将符合配方结构的 JSON 字符串反序列化为具体的配方对象
        /// </summary>
        /// <typeparam name="T">配方子类的目标类型</typeparam>
        /// <param name="json">待反序列化的 JSON 格式字符串</param>
        /// <returns>反序列化生成的配方对象实例</returns>
        public static T FromJson<T>(string json) where T : RecipeParamBase
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// 数据深拷贝方法。
        /// 需在派生类中具体实现，用于创建一个新的配方对象，其所有层级的属性值均与当前对象保持一致（切断引用关联）。
        /// </summary>
        /// <returns>克隆生成的新配方参数实例</returns>
        public abstract RecipeParamBase DeepClone();
    }
}