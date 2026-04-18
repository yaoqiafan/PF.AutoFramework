using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Recipe
{
    /// <summary>
    /// 配方服务基类
    /// </summary>
    public abstract class BaseRecipe<T> : IRecipeService<T> where T : RecipeParamBase
    {
        /// <summary>
        /// 配方文件目录路径
        /// </summary>
        public string RecipeDirPath => $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\Recipe";



        private readonly string BackUpRecipeDirPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\BackUpRecipe";



        /// <summary>
        /// 配方专用日志记录器
        /// </summary>
        public readonly Logging.CategoryLogger RecipeLogger;


        /// <summary>
        /// 构造配方服务
        /// </summary>
        public BaseRecipe(ILogService logger)
        {
            RecipeLogger = Logging.CategoryLoggerFactory.Recipe(logger);
            if (!Directory.Exists(BackUpRecipeDirPath))
            {
                DirectoryInfo di = Directory.CreateDirectory(BackUpRecipeDirPath);
                di.Attributes |= FileAttributes.Hidden;
            }
        }




        /// <summary>
        /// 获取所有配方名称列表
        /// </summary>
        public List<string> RecipeNames
        {
            get
            {
                return GetAllRecipe();
            }
        }



        private List<string> GetAllRecipe()
        {
            // 1. 先判断文件夹是否存在
            if (!System.IO.Directory.Exists(this.RecipeDirPath))
            {
                // 如果不存在，直接返回空列表 
                System.IO.Directory.CreateDirectory(this.RecipeDirPath);
                return new List<string>();
            }

            // 2. 获取文件夹下所有的 json 文件路径
            IEnumerable<string> jsonFiles = System.IO.Directory.EnumerateFiles(this.RecipeDirPath, "*.json", System.IO.SearchOption.AllDirectories);

            // 3. 提取纯文件名（不含路径和扩展名），避免下游方法拼接路径时出错
            return jsonFiles.Select(file => System.IO.Path.GetFileNameWithoutExtension(file)).ToList();
        }


        /// <summary>
        /// 配方变更通知
        /// </summary>
        public abstract Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default);



     


        /// <summary>
        /// 写入配方参数到文件
        /// </summary>
        public Task<bool> RecipeParamWriteAsync(T RecipeParam, bool IsCover = false, CancellationToken token = default)
        {
            try
            {
                if (this.RecipeNames.Contains(RecipeParam.RecipeName))
                {
                    RecipeParam.UpdateTime = DateTime.Now;
                    if (!IsCover)
                    {
                        throw new Exception($"{RecipeParam.RecipeName}配方已存在");
                    }
                    else
                    {
                        string recipefilepath = $"{this.RecipeDirPath}\\{RecipeParam.RecipeName}.json";
                        string str = System.Text.Json.JsonSerializer.Serialize(RecipeParam);
                        File.WriteAllText(recipefilepath, str);
                        File.WriteAllText($"{this.BackUpRecipeDirPath}\\{RecipeParam.RecipeName}.json", str);
                    }
                }
                else
                {
                    RecipeParam.UpdateTime = DateTime.Now;
                    RecipeParam.CreateTime = DateTime.Now;
                    string recipefilepath = $"{this.RecipeDirPath}\\{RecipeParam.RecipeName}.json";
                    string str = System.Text.Json.JsonSerializer.Serialize(RecipeParam);
                    File.WriteAllText(recipefilepath, str);
                    File.WriteAllText($"{this.BackUpRecipeDirPath}\\{RecipeParam.RecipeName}.json", str);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                RecipeLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 删除指定配方
        /// </summary>
        public Task<bool> RecipeDeleteAsync(T RecipeParam, CancellationToken token = default)
        {
            try
            {
                return RecipeDeleteAsync(RecipeParam.RecipeName, token);
            }
            catch (Exception ex)
            {
                RecipeLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }




        /// <summary>
        /// 获取所有配方对象列表
        /// </summary>
        public async Task<List<T>> GetAllRecipes(CancellationToken token = default)
        {
            List<T> list = new List<T>();
            for (int i = 0; i < this.RecipeNames?.Count; i++)
            {
                await Task.Delay(1, token);
                var recipe = await RecipeParam(RecipeNames[i]);
                if (recipe != null)
                {
                    list.Add(recipe);
                }
            }
            return list;
        }




        /// <summary>
        /// 按名称删除配方
        /// </summary>
        public Task<bool> RecipeDeleteAsync(string RecipeName, CancellationToken token = default)
        {
            try
            {
                string recipepath = $"{this.RecipeDirPath}\\{RecipeName}.json";
                string backupPath = $"{this.BackUpRecipeDirPath}\\{RecipeName}.json";
                File.Delete(recipepath);
                File.Delete(backupPath);
                return Task.FromResult(true);

            }
            catch (Exception ex)
            {
                RecipeLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }



        /// <summary>
        /// 更新配方
        /// </summary>
        public abstract Task<bool> RecipeUpdateAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 下载配方
        /// </summary>
        public abstract Task<bool> DownLoadRecipe(T RecipeParam, CancellationToken token = default);







        /// <summary>
        /// 复制配方
        /// </summary>
        public Task<T> CopyRecipeAsync(string RecipeName, T RecipeParam, CancellationToken token = default)
        {
            if (this.RecipeNames.FindIndex(x => x == RecipeName) != -1)
            {
                return Task.FromResult<T>(null);
            }
            var newrecipe = RecipeParam.DeepClone() as T;
            if (newrecipe != null)
            {
                newrecipe.RecipeName = RecipeName;
                this.RecipeParamWriteAsync(newrecipe, false, token);
                return Task.FromResult(newrecipe);
            }
            else
            {
                return Task.FromResult<T>(null);
            }
        }

        /// <summary>
        /// 修改配方名称
        /// </summary>
        public Task<bool> ChangeRecipeNameAsync(T RecipeParam, string NewRecipeName, CancellationToken token = default)
        {
            if (this.RecipeNames.FindIndex(x => x == NewRecipeName) != -1)
            {
                return Task.FromResult(false);
            }
            var newrecipe = RecipeParam.DeepClone() as T;
            if (newrecipe != null)
            {
                newrecipe.RecipeName = NewRecipeName;
                this.RecipeDeleteAsync(RecipeParam.RecipeName, token);
                return this.RecipeParamWriteAsync(newrecipe, false, token);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        Task<T> IRecipeService<T>.RecipeParam(string RecipeName, CancellationToken token)
        {
            try
            {
                string recipefilepath = $"{this.RecipeDirPath}\\{RecipeName}.json";
                if (!File.Exists(recipefilepath))
                {
                    throw new FileNotFoundException($"配方{RecipeName}不存在");
                }
                string str = File.ReadAllText(recipefilepath);
                return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(str));
            }
            catch (Exception ex)
            {
                RecipeLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 按名称读取配方参数
        /// </summary>
        public Task<T> RecipeParam(string? requestedPpid)
        {
            try
            {
                string recipefilepath = $"{this.RecipeDirPath}\\{requestedPpid}.json";
                if (!File.Exists(recipefilepath))
                {
                    throw new FileNotFoundException($"配方{requestedPpid}不存在");
                }
                string str = File.ReadAllText(recipefilepath);
                return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(str));
            }
            catch (Exception ex)
            {
                RecipeLogger.Debug(ex.Message, ex);
                return null;
            }
        }
    }
}
