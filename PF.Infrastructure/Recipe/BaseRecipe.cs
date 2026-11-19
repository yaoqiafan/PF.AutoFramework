using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using PF.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PF.Infrastructure.Recipe
{
    /// <summary>
    /// 配方服务基类：每个配方存为 <see cref="RecipeDirPath"/> 下的一个 {配方名}.json，
    /// 同时在备份目录留一份副本。
    /// </summary>
    /// <remarks>
    /// 配方名会直接拼成文件名，而它可能来自 Host 下发的 PPID（S7F3/S7F5/S7F17），
    /// 所以所有读写删入口都先经 <see cref="IsValidRecipeName"/> 校验：拒绝空名、路径分隔符、
    /// 非法文件名字符和 "."/".."——否则 "..\xxx" 可以读出或删掉配方目录以外的任意 .json。
    /// </remarks>
    public abstract class BaseRecipe<T> : IRecipeService<T> where T : RecipeParamBase
    {
        /// <summary>
        /// 配方文件目录路径
        /// </summary>
        public string RecipeDirPath => Path.Combine(PF.Core.Constants.ConstGlobalParam.ConfigPath, "Recipe");

        /// <summary>
        /// 配方备份目录。放在配置目录之外（误删/迁移配置目录时备份仍在），并按项目名分子目录，
        /// 避免同机多项目的同名配方互相覆盖备份。
        /// </summary>
        private string BackUpRecipeDirPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BackUpRecipe",
            PF.Core.Constants.ConstGlobalParam.ProjectName);

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
            try
            {
                var root = Path.GetDirectoryName(BackUpRecipeDirPath)!;
                if (!Directory.Exists(root))
                {
                    var di = Directory.CreateDirectory(root);
                    di.Attributes |= FileAttributes.Hidden;
                }
                Directory.CreateDirectory(BackUpRecipeDirPath);
            }
            catch (Exception ex)
            {
                // 备份目录建不出来不影响配方本身的读写，只是没有备份
                RecipeLogger.Warn($"配方备份目录创建失败：{BackUpRecipeDirPath}，{ex.Message}");
            }
        }

        /// <summary>
        /// 判断配方名能否安全地作为文件名使用。
        /// </summary>
        public static bool IsValidRecipeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name == "." || name == "..") return false;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            // Windows 会静默去掉文件名末尾的点和空格，"A." 与 "A" 会落到同一个文件
            if (name.EndsWith('.') || name.EndsWith(' ')) return false;
            return true;
        }

        private string RecipeFilePath(string name) => Path.Combine(RecipeDirPath, name + ".json");

        private string BackupFilePath(string name) => Path.Combine(BackUpRecipeDirPath, name + ".json");

        /// <summary>
        /// 获取所有配方名称列表
        /// </summary>
        public virtual List<string> RecipeNames => GetAllRecipe();

        private List<string> GetAllRecipe()
        {
            try
            {
                if (!Directory.Exists(RecipeDirPath))
                {
                    Directory.CreateDirectory(RecipeDirPath);
                    return new List<string>();
                }

                // 只列顶层：读取按 {目录}\{名称}.json 拼路径，子目录里的文件列出来也读不到
                return Directory.EnumerateFiles(RecipeDirPath, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .ToList();
            }
            catch (Exception ex)
            {
                RecipeLogger.Error($"枚举配方目录失败：{RecipeDirPath}，{ex.Message}", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// 配方变更通知
        /// </summary>
        public abstract Task<bool> RecipeChangedAsync(T RecipeParam, CancellationToken token = default);

        /// <summary>
        /// 写入配方参数到文件（原子写入，断电不会留下半截文件）
        /// </summary>
        public virtual Task<bool> RecipeParamWriteAsync(T RecipeParam, bool IsCover = false, CancellationToken token = default)
        {
            try
            {
                if (RecipeParam == null)
                {
                    RecipeLogger.Warn("写入配方失败：配方对象为空");
                    return Task.FromResult(false);
                }
                if (!IsValidRecipeName(RecipeParam.RecipeName))
                {
                    RecipeLogger.Warn($"写入配方失败：配方名 [{RecipeParam.RecipeName}] 不能作为文件名（为空或含非法字符）");
                    return Task.FromResult(false);
                }

                bool exists = File.Exists(RecipeFilePath(RecipeParam.RecipeName));
                if (exists && !IsCover)
                {
                    RecipeLogger.Warn($"写入配方失败：配方 [{RecipeParam.RecipeName}] 已存在且未允许覆盖");
                    return Task.FromResult(false);
                }

                RecipeParam.UpdateTime = DateTime.Now;
                if (!exists) RecipeParam.CreateTime = DateTime.Now;

                Directory.CreateDirectory(RecipeDirPath);
                string str = System.Text.Json.JsonSerializer.Serialize(RecipeParam);
                AtomicFile.WriteAllText(RecipeFilePath(RecipeParam.RecipeName), str);

                try
                {
                    Directory.CreateDirectory(BackUpRecipeDirPath);
                    AtomicFile.WriteAllText(BackupFilePath(RecipeParam.RecipeName), str);
                }
                catch (Exception ex)
                {
                    // 主文件已写成功，备份失败只告警，不让保存整体失败
                    RecipeLogger.Warn($"配方 [{RecipeParam.RecipeName}] 已保存，但备份失败：{ex.Message}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                RecipeLogger.Error($"写入配方 [{RecipeParam?.RecipeName}] 失败：{ex.Message}", ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 删除指定配方
        /// </summary>
        public Task<bool> RecipeDeleteAsync(T RecipeParam, CancellationToken token = default)
        {
            if (RecipeParam == null) return Task.FromResult(false);
            return RecipeDeleteAsync(RecipeParam.RecipeName, token);
        }

        /// <summary>
        /// 获取所有配方对象列表。单个配方读取失败（文件损坏等）只跳过该配方并记日志，不影响其余配方。
        /// </summary>
        public async Task<List<T>> GetAllRecipes(CancellationToken token = default)
        {
            var list = new List<T>();
            foreach (var name in RecipeNames)
            {
                token.ThrowIfCancellationRequested();
                var recipe = await RecipeParam(name, token).ConfigureAwait(false);
                if (recipe != null)
                {
                    list.Add(recipe);
                }
            }
            return list;
        }

        /// <summary>
        /// 按名称删除配方。配方文件不存在时返回 false。
        /// </summary>
        public virtual Task<bool> RecipeDeleteAsync(string RecipeName, CancellationToken token = default)
        {
            try
            {
                if (!IsValidRecipeName(RecipeName))
                {
                    RecipeLogger.Warn($"删除配方被拒绝：配方名 [{RecipeName}] 非法");
                    return Task.FromResult(false);
                }

                string recipepath = RecipeFilePath(RecipeName);
                if (!File.Exists(recipepath))
                {
                    RecipeLogger.Warn($"删除配方失败：配方 [{RecipeName}] 不存在");
                    return Task.FromResult(false);
                }

                File.Delete(recipepath);
                try { File.Delete(BackupFilePath(RecipeName)); }
                catch (Exception ex) { RecipeLogger.Warn($"配方 [{RecipeName}] 已删除，但删除备份失败：{ex.Message}"); }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                RecipeLogger.Error($"删除配方 [{RecipeName}] 失败：{ex.Message}", ex);
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
        /// 复制配方。新名称非法、已存在或写入失败时返回 null。
        /// </summary>
        public virtual async Task<T> CopyRecipeAsync(string RecipeName, T RecipeParam, CancellationToken token = default)
        {
            if (!IsValidRecipeName(RecipeName))
            {
                RecipeLogger.Warn($"复制配方失败：新配方名 [{RecipeName}] 非法");
                return null;
            }
            if (RecipeNames.Contains(RecipeName))
            {
                RecipeLogger.Warn($"复制配方失败：配方 [{RecipeName}] 已存在");
                return null;
            }
            if (RecipeParam?.DeepClone() is not T newrecipe)
            {
                return null;
            }
            newrecipe.RecipeName = RecipeName;
            bool written = await RecipeParamWriteAsync(newrecipe, false, token).ConfigureAwait(false);
            return written ? newrecipe : null;
        }

        /// <summary>
        /// 修改配方名称：先写新文件、确认成功后再删旧文件，写新失败时旧文件原封不动。
        /// 旧文件删除失败时返回 false（此时新旧两份并存，日志会说明）。
        /// </summary>
        public virtual async Task<bool> ChangeRecipeNameAsync(T RecipeParam, string NewRecipeName, CancellationToken token = default)
        {
            if (RecipeParam == null || !IsValidRecipeName(NewRecipeName))
            {
                RecipeLogger.Warn($"重命名配方失败：新配方名 [{NewRecipeName}] 非法");
                return false;
            }
            if (RecipeNames.Contains(NewRecipeName))
            {
                RecipeLogger.Warn($"重命名配方失败：配方 [{NewRecipeName}] 已存在");
                return false;
            }
            if (RecipeParam.DeepClone() is not T newrecipe)
            {
                return false;
            }
            newrecipe.RecipeName = NewRecipeName;
            if (!await RecipeParamWriteAsync(newrecipe, false, token).ConfigureAwait(false))
            {
                return false;
            }
            if (!await RecipeDeleteAsync(RecipeParam.RecipeName, token).ConfigureAwait(false))
            {
                RecipeLogger.Warn($"配方已另存为 [{NewRecipeName}]，但旧配方 [{RecipeParam.RecipeName}] 删除失败，两份并存");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 按名称读取配方参数。配方不存在、名称非法或文件损坏时返回结果为 null 的 Task（不会返回 null Task）。
        /// </summary>
        public virtual Task<T> RecipeParam(string RecipeName, CancellationToken token = default)
            => Task.FromResult(ReadRecipe(RecipeName));

        /// <summary>
        /// 按名称（PPID）读取配方参数，语义同 <see cref="RecipeParam(string, CancellationToken)"/>。
        /// </summary>
        public virtual Task<T> RecipeParam(string? requestedPpid)
            => Task.FromResult(ReadRecipe(requestedPpid));

        private T? ReadRecipe(string? name)
        {
            if (!IsValidRecipeName(name))
            {
                RecipeLogger.Warn($"读取配方被拒绝：配方名 [{name}] 非法");
                return null;
            }

            string recipefilepath = RecipeFilePath(name!);
            try
            {
                if (!File.Exists(recipefilepath))
                {
                    RecipeLogger.Warn($"配方 [{name}] 不存在：{recipefilepath}");
                    return null;
                }
                string str = File.ReadAllText(recipefilepath);
                return System.Text.Json.JsonSerializer.Deserialize<T>(str);
            }
            catch (Exception ex)
            {
                RecipeLogger.Error($"读取配方 [{name}] 失败（文件可能已损坏）：{ex.Message}", ex);
                return null;
            }
        }
    }
}
