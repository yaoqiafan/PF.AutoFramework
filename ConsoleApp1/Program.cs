using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MergeCodebase
{
    class Program
    {
        // ==========================================
        // 配置区
        // ==========================================

        static readonly HashSet<string> IgnoreDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "bin", "obj", ".vs", ".idea",
            "packages", "dist", "build", "out", "target"
        };

        static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xaml", ".cshtml", ".js", ".ts", ".py", ".java", ".c", ".cpp", ".h",
            ".json", ".md", ".html", ".css", ".sql", ".xml", ".yaml", ".yml", ".sln"
        };

        static void Main(string[] args)
        {
            // ⚠️ 替换为你要转换的项目的本地完整路径
            string targetRepoPath = @"C:\Users\12434\source\repos\PF.AutoFramework";

            // 输出目录
            string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MergedCodebaseOutput");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            Console.WriteLine("开始按 'PF.xxxxx' 前缀分类合并代码...\n");

            // 1. 扫描并对顶层文件夹进行分组
            // Key: 分类名 (如 "PF.Modules"), Value: 属于该分类的实际文件夹路径列表
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in Directory.GetDirectories(targetRepoPath))
            {
                string dirName = new DirectoryInfo(dir).Name;

                // 跳过黑名单目录
                if (IgnoreDirs.Contains(dirName)) continue;

                // 提取分类前缀
                string groupKey = dirName; // 默认情况下，自身就是一个分类

                if (dirName.StartsWith("PF.", StringComparison.OrdinalIgnoreCase))
                {
                    // 按照小数点进行分割，例如 "PF.Modules.Identity" -> ["PF", "Modules", "Identity"]
                    string[] parts = dirName.Split('.');
                    if (parts.Length >= 2)
                    {
                        // 提取 PF.xxxxx 作为我们的核心分类 Key
                        groupKey = $"PF.{parts[1]}";
                    }
                }

                if (!groups.ContainsKey(groupKey))
                {
                    groups[groupKey] = new List<string>();
                }
                groups[groupKey].Add(dir);
            }

            // 2. 按分组生成 Markdown 文件
            foreach (var kvp in groups)
            {
                string groupKey = kvp.Key;                  // 例如: "PF.Modules"
                List<string> dirsInGroup = kvp.Value;       // 例如: [ "...\PF.Modules.Identity", "...\PF.Modules.Parameter" ]

                string outputFileName = Path.Combine(outputDirectory, $"{groupKey}.md");
                Console.WriteLine($"📦 正在合并分类: {groupKey} (包含 {dirsInGroup.Count} 个项目/模块) ...");

                int totalFiles = 0;
                try
                {
                    // 为该分类创建一个写入流
                    using (StreamWriter writer = new StreamWriter(outputFileName, false, Encoding.UTF8))
                    {
                        // 遍历属于该分类的所有文件夹
                        foreach (string dir in dirsInGroup)
                        {
                            MergeDirectoryToWriter(dir, targetRepoPath, writer, ref totalFiles);
                        }
                    }

                    // 空文件清理
                    if (totalFiles == 0)
                    {
                        File.Delete(outputFileName);
                        Console.WriteLine($"   -> ⚠️ 分类 '{groupKey}' 中没有提取到任何代码，已跳过。");
                    }
                    else
                    {
                        Console.WriteLine($"   -> ✅ 写入了 {totalFiles} 个代码文件。");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 处理分类 {groupKey} 时发生错误: {ex.Message}");
                }
            }

            // 3. 处理根目录下的孤立文件 (例如 .sln 等)
            string rootOutputFileName = Path.Combine(outputDirectory, "_RootFiles.md");
            Console.WriteLine($"\n📄 正在处理根目录文件 ...");
            MergeRootFilesToText(targetRepoPath, rootOutputFileName);

            Console.WriteLine($"\n🎉 全部处理完成！");
            Console.WriteLine($"📁 完美分类生成的所有代码文件已存放在: \n{outputDirectory}");
        }

        /// <summary>
        /// 递归遍历指定文件夹，并将代码内容持续追加到当前打开的 StreamWriter 中
        /// </summary>
        static void MergeDirectoryToWriter(string processPath, string baseRepoPath, StreamWriter writer, ref int totalFiles)
        {
            Queue<string> dirsQueue = new Queue<string>();
            dirsQueue.Enqueue(processPath);

            while (dirsQueue.Count > 0)
            {
                string currentDir = dirsQueue.Dequeue();

                try
                {
                    // 子目录入队
                    foreach (string dir in Directory.GetDirectories(currentDir))
                    {
                        string dirName = new DirectoryInfo(dir).Name;
                        if (!IgnoreDirs.Contains(dirName))
                        {
                            dirsQueue.Enqueue(dir);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { /* 忽略没权限的目录 */ }

                try
                {
                    // 文件写入
                    foreach (string file in Directory.GetFiles(currentDir))
                    {
                        ProcessSingleFile(file, baseRepoPath, writer, ref totalFiles);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>
        /// 仅仅处理根目录下的代码文件，不进行深入递归
        /// </summary>
        static void MergeRootFilesToText(string rootPath, string outputFile)
        {
            int totalFiles = 0;
            try
            {
                using (StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    foreach (string file in Directory.GetFiles(rootPath))
                    {
                        ProcessSingleFile(file, rootPath, writer, ref totalFiles);
                    }
                }

                if (totalFiles == 0) File.Delete(outputFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理根目录时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 核心读取方法：读取单个文件并写入到对应的 md 结构中
        /// </summary>
        static void ProcessSingleFile(string file, string baseRepoPath, StreamWriter writer, ref int totalFiles)
        {
            string ext = Path.GetExtension(file);

            if (AllowedExtensions.Contains(ext))
            {
                string relativePath = Path.GetRelativePath(baseRepoPath, file);
                string codeLang = ext.TrimStart('.').ToLower();

                // 格式校准
                if (codeLang == "xaml") codeLang = "xml";
                if (codeLang == "sln") codeLang = "text";

                // 写入文件头信息
                writer.WriteLine($"\n### File: `{relativePath.Replace("\\", "/")}`\n");

                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    writer.WriteLine($"```{codeLang}");
                    writer.WriteLine(content);
                    writer.WriteLine("```\n");
                    totalFiles++;
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"// Error reading file content: {ex.Message}");
                }
            }
        }
    }
}