using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Constants;
using System.IO;

namespace PF.UI.Infrastructure.Operation
{
    /// <summary>
    /// 操作日志的项目级配置存取：一个 Excel 工作簿，一个工作表对应一个页面（<see cref="OperationLog.PageNameProperty"/>），
    /// 每行是一个 Key 的 {Description, Enabled}。文件位于 <see cref="ConstGlobalParam.ConfigPath"/>，
    /// 由程序自动生成/追加行——工程师直接编辑 Excel 的 Enabled 列即可按项目定制，不用碰代码。
    /// </summary>
    public class OperationLogConfigStore
    {
        private const string FileName = "OperationLogConfig.xlsx";
        private static readonly string[] Header = { "Key", "Description", "Enabled" };

        private readonly string _filePath;
        private readonly Dictionary<string, Dictionary<string, OperationLogEntry>> _data = new();
        private bool _loaded;

        /// <summary>构造时立即从 <see cref="ConstGlobalParam.ConfigPath"/> 下的配置文件加载已有数据</summary>
        public OperationLogConfigStore()
        {
            _filePath = Path.Combine(ConstGlobalParam.ConfigPath, FileName);
            Load();
        }

        private void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (!File.Exists(_filePath)) return;

            try
            {
                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
                var workbook = new XSSFWorkbook(stream);
                for (var i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);
                    var pageEntries = new Dictionary<string, OperationLogEntry>();
                    for (var r = 1; r <= sheet.LastRowNum; r++)
                    {
                        var row = sheet.GetRow(r);
                        if (row == null) continue;

                        var key = row.GetCell(0)?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;

                        pageEntries[key] = new OperationLogEntry
                        {
                            Description = row.GetCell(1)?.ToString() ?? string.Empty,
                            Enabled = bool.TryParse(row.GetCell(2)?.ToString(), out var enabled) && enabled
                        };
                    }
                    _data[sheet.SheetName] = pageEntries;
                }
            }
            catch
            {
                // 文件损坏/被占用：退化为空配置，不阻塞主流程；后续新键仍会按默认值追加。
            }
        }

        /// <summary>
        /// 查询某个（页面, Key）的记录配置：已存在则返回 Excel 里记录的整行（Description/Enabled 都以
        /// 工程师改过的为准）；不存在则按传入的默认值新建一行、同步写回文件，并返回这个默认值。
        /// </summary>
        public OperationLogEntry GetOrCreate(string pageName, string key, string defaultDescription, bool defaultEnabled)
        {
            if (_data.TryGetValue(pageName, out var pageEntries) && pageEntries.TryGetValue(key, out var entry))
                return entry;

            if (!_data.TryGetValue(pageName, out pageEntries))
            {
                pageEntries = new Dictionary<string, OperationLogEntry>();
                _data[pageName] = pageEntries;
            }

            entry = new OperationLogEntry { Description = defaultDescription, Enabled = defaultEnabled };
            pageEntries[key] = entry;
            Save();
            return entry;
        }

        /// <summary>
        /// 整页批量补齐缺失的键（页面首次加载时预写用）：已存在的行原样保留（工程师改过的优先），
        /// 只有真的补进新行才写一次文件——预写一整页也只落一次盘。
        /// </summary>
        public void EnsurePage(string pageName, IEnumerable<(string Key, string Description, bool Enabled)> rows)
        {
            if (!_data.TryGetValue(pageName, out var pageEntries))
            {
                pageEntries = new Dictionary<string, OperationLogEntry>();
                _data[pageName] = pageEntries;
            }

            var added = false;
            foreach (var (key, description, enabled) in rows)
            {
                if (pageEntries.ContainsKey(key)) continue;
                pageEntries[key] = new OperationLogEntry { Description = description, Enabled = enabled };
                added = true;
            }

            if (added) Save();
        }

        private void Save()
        {
            try
            {
                if (!Directory.Exists(ConstGlobalParam.ConfigPath))
                    Directory.CreateDirectory(ConstGlobalParam.ConfigPath);

                var workbook = new XSSFWorkbook();
                foreach (var (pageName, entries) in _data)
                {
                    var sheet = workbook.CreateSheet(SanitizeSheetName(pageName));
                    var headerRow = sheet.CreateRow(0);
                    for (var c = 0; c < Header.Length; c++)
                        headerRow.CreateCell(c).SetCellValue(Header[c]);

                    var r = 1;
                    foreach (var (key, entry) in entries)
                    {
                        var row = sheet.CreateRow(r++);
                        row.CreateCell(0).SetCellValue(key);
                        row.CreateCell(1).SetCellValue(entry.Description);
                        row.CreateCell(2).SetCellValue(entry.Enabled);
                    }
                }

                using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(stream);
            }
            catch
            {
                // Excel 被工程师打开占用等情况：跳过本次持久化，不影响当次日志记录。
            }
        }

        private static string SanitizeSheetName(string pageName)
        {
            var name = pageName;
            foreach (var c in new[] { '\\', '/', '*', '?', '[', ']', ':' })
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
