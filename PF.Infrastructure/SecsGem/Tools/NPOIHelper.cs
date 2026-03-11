using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace PF.Infrastructure.SecsGem.Tools
{
    public static  class NPOIHelper
    {
        #region ValidateConfiguration
        private static readonly object _lock = new object();

        public static void SaveValidate(string filePath, ValidateConfiguration configuration)
        {
            lock (_lock)
            {
                try
                {
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    SaveValidateToExcel(filePath, configuration);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存配置失败: {ex.Message}");
                    throw;
                }
            }
        }

        public static async Task<bool> LoadValidateFromExcel(string filePath, ValidateConfiguration configuration)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    IWorkbook workbook;
                    if (Path.GetExtension(filePath).ToLower() == ".xlsx")
                    {
                        workbook = new XSSFWorkbook(fs);
                    }
                    else
                    {
                        workbook = new HSSFWorkbook(fs);
                    }

                    // 加载CEID
                    LoadCEIDs(workbook, configuration);

                    // 加载ReportID
                    LoadReportIDs(workbook, configuration);

                    // 加载VID
                    LoadVIDs(workbook, configuration);
                    //加载CommandID
                    LoadCommandIDs(workbook, configuration);

                    workbook.Close();
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        private static void LoadCEIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.GetSheet("CEID");
            if (sheet == null) return;

            for (int i = 1; i <= sheet.LastRowNum; i++) // 跳过标题行
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    uint id = (uint)row.GetCell(0).NumericCellValue;
                    string description = row.GetCell(1)?.ToString() ?? string.Empty;
                    string comment = row.GetCell(2)?.ToString() ?? string.Empty;

                    // 解析LinkReportID（逗号分隔的字符串）
                    string linkReportIDStr = row.GetCell(3)?.ToString() ?? string.Empty;
                    uint[] linkReportID = Array.Empty<uint>();

                    if (!string.IsNullOrEmpty(linkReportIDStr))
                    {
                        var ids = linkReportIDStr.Split(',')
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => uint.Parse(s.Trim()))
                            .ToArray();
                        linkReportID = ids;
                    }

                    string key = row.GetCell(4)?.ToString() ?? string.Empty;

                    var ceid = new CEID(id, description, linkReportID, key)
                    {
                        Comment = comment
                    };

                    // 使用description作为键值
                    config.CEIDS[description] = ceid;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载CEID行{i}时出错: {ex.Message}");
                }
            }
        }

        private static void LoadReportIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.GetSheet("ReportID");
            if (sheet == null) return;

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    uint id = (uint)row.GetCell(0).NumericCellValue;
                    string description = row.GetCell(1)?.ToString() ?? string.Empty;
                    string comment = row.GetCell(2)?.ToString() ?? string.Empty;

                    // 解析LinkVID
                    string linkVIDStr = row.GetCell(3)?.ToString() ?? string.Empty;
                    uint[] linkVID = Array.Empty<uint>();

                    if (!string.IsNullOrEmpty(linkVIDStr))
                    {
                        var ids = linkVIDStr.Split(',')
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => uint.Parse(s.Trim()))
                            .ToArray();
                        linkVID = ids;
                    }

                    var report = new ReportID(id, description, linkVID)
                    {
                        Comment = comment
                    };

                    // 使用description作为键值
                    config.ReportIDS[description] = report;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载ReportID行{i}时出错: {ex.Message}");
                }
            }
        }

        private static void LoadCommandIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.GetSheet("CommandID");
            if (sheet == null) return;
            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;
                try
                {

                    uint id = (uint)Convert.ToInt16(row.GetCell(0)?.ToString() ?? string.Empty);
                    string description = row.GetCell(1)?.ToString() ?? string.Empty;
                    string comment = row.GetCell(2)?.ToString() ?? string.Empty;
                    // 解析LinkVID
                    string linkVIDStr = row.GetCell(3)?.ToString() ?? string.Empty;
                    uint[] linkVID = Array.Empty<uint>();
                    if (!string.IsNullOrEmpty(linkVIDStr))
                    {
                        var ids = linkVIDStr.Split(',')
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => uint.Parse(s.Trim()))
                            .ToArray();
                        linkVID = ids;
                    }
                    string RCMD = row.GetCell(4)?.ToString() ?? string.Empty;
                    string key = row.GetCell(5)?.ToString() ?? string.Empty;
                    var command = new CommandID(id, description, linkVID, RCMD, key)
                    {
                        Comment = comment
                    };

                    // 使用description作为键值
                    config.CommandIDS[description] = command;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载CommandID行{i}时出错: {ex.Message}");
                }
            }
        }

        private static void LoadVIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.GetSheet("VID");
            if (sheet == null) return;

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) continue;

                try
                {
                    uint id = (uint)row.GetCell(0).NumericCellValue;
                    string description = row.GetCell(1)?.ToString() ?? string.Empty;
                    string comment = row.GetCell(2)?.ToString() ?? string.Empty;
                    string dataTypeStr = row.GetCell(3)?.ToString() ?? "ASCII";
                    string valueStr = row.GetCell(4)?.ToString() ?? string.Empty;

                    if (Enum.TryParse<DataType>(dataTypeStr, out DataType dataType))
                    {
                        var vid = new VID(id, description, dataType)
                        {
                            Comment = comment
                        };

                        // 尝试设置值
                        if (!string.IsNullOrEmpty(valueStr))
                        {
                            try
                            {
                                object value = ConvertValue(valueStr, dataType);
                                vid.SetValue(value);
                            }
                            catch
                            {
                                // 如果转换失败，保留空值
                            }
                        }

                        // 使用description作为键值
                        config.VIDS[description] = vid;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载VID行{i}时出错: {ex.Message}");
                }
            }
        }

        private static object ConvertValue(string valueStr, DataType dataType)
        {
            if (string.IsNullOrEmpty(valueStr)) return null;

            switch (dataType)
            {
                case DataType.Boolean:
                    return bool.Parse(valueStr);
                case DataType.ASCII:
                case DataType.JIS8:
                case DataType.CHARACTER_2:
                    return valueStr;
                case DataType.I8:
                    return Int128.Parse(valueStr);
                case DataType.I1:
                    return short.Parse(valueStr);
                case DataType.I2:
                    return int.Parse(valueStr);
                case DataType.I4:
                    return long.Parse(valueStr);
                case DataType.F8:
                    return double.Parse(valueStr, CultureInfo.InvariantCulture);
                case DataType.F4:
                    return float.Parse(valueStr, CultureInfo.InvariantCulture);
                case DataType.U8:
                    return UInt128.Parse(valueStr);
                case DataType.U1:
                    return ushort.Parse(valueStr);
                case DataType.U2:
                    return uint.Parse(valueStr);
                case DataType.U4:
                    return ulong.Parse(valueStr);
                case DataType.Binary:
                    return new byte[] { 0x00 };
                    ;
                default:
                    return valueStr;
            }
        }

        private static void SaveValidateToExcel(string filePath, ValidateConfiguration config)
        {
            IWorkbook workbook;
            if (Path.GetExtension(filePath).ToLower() == ".xlsx")
            {
                workbook = new XSSFWorkbook();
            }
            else
            {
                workbook = new HSSFWorkbook();
            }

            // 保存CEID
            SaveCEIDs(workbook, config);

            // 保存ReportID
            SaveReportIDs(workbook, config);

            //保存CommandID
            SaveCommandIDs(workbook, config);

            // 保存VID
            SaveVIDs(workbook, config);

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fs);
            }
        }

        private static void SaveCEIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.CreateSheet("CEID");

            // 创建标题行
            IRow headerRow = sheet.CreateRow(0);
            headerRow.CreateCell(0).SetCellValue("ID");
            headerRow.CreateCell(1).SetCellValue("Description");
            headerRow.CreateCell(2).SetCellValue("Comment");
            headerRow.CreateCell(3).SetCellValue("LinkReportID");

            int rowIndex = 1;
            foreach (var kvp in config.CEIDS)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(kvp.Value.ID);
                row.CreateCell(1).SetCellValue(kvp.Value.Description);
                row.CreateCell(2).SetCellValue(kvp.Value.Comment ?? string.Empty);

                string linkReportIDStr = string.Join(",", kvp.Value.LinkReportID);
                row.CreateCell(3).SetCellValue(linkReportIDStr);
            }

            // 自动调整列宽
            for (int i = 0; i < 4; i++)
            {
                sheet.AutoSizeColumn(i);
            }
        }

        private static void SaveReportIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.CreateSheet("ReportID");
            IRow headerRow = sheet.CreateRow(0);
            headerRow.CreateCell(0).SetCellValue("ID");
            headerRow.CreateCell(1).SetCellValue("Description");
            headerRow.CreateCell(2).SetCellValue("Comment");
            headerRow.CreateCell(3).SetCellValue(",,");

            int rowIndex = 1;
            foreach (var kvp in config.ReportIDS)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(kvp.Value.ID);
                row.CreateCell(1).SetCellValue(kvp.Value.Description);
                row.CreateCell(2).SetCellValue(kvp.Value.Comment ?? string.Empty);

                string linkVIDStr = string.Join(",", kvp.Value.LinkVID);
                row.CreateCell(3).SetCellValue(linkVIDStr);
            }

            for (int i = 0; i < 4; i++)
            {
                sheet.AutoSizeColumn(i);
            }
        }

        private static void SaveCommandIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.CreateSheet("CommandID");
            IRow headerRow = sheet.CreateRow(0);
            headerRow.CreateCell(0).SetCellValue("ID");
            headerRow.CreateCell(1).SetCellValue("Description");
            headerRow.CreateCell(2).SetCellValue("Comment");
            headerRow.CreateCell(3).SetCellValue("LinkVID");
            headerRow.CreateCell(4).SetCellValue("RCMD");
            headerRow.CreateCell(5).SetCellValue("Key");
            int rowIndex = 1;
            foreach (var kvp in config.CommandIDS)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(kvp.Value.ID);
                row.CreateCell(1).SetCellValue(kvp.Value.Description);
                row.CreateCell(2).SetCellValue(kvp.Value.Comment ?? string.Empty);
                string linkVIDStr = string.Join(",", kvp.Value.LinkVID);
                row.CreateCell(3).SetCellValue(linkVIDStr);
                row.CreateCell(4).SetCellValue(kvp.Value.RCMD);
                row.CreateCell(5).SetCellValue(kvp.Value.Key);
            }
            for (int i = 0; i < 5; i++)
            {
                sheet.AutoSizeColumn(i);
            }
        }

        private static void SaveVIDs(IWorkbook workbook, ValidateConfiguration config)
        {
            ISheet sheet = workbook.CreateSheet("VID");

            IRow headerRow = sheet.CreateRow(0);
            headerRow.CreateCell(0).SetCellValue("ID");
            headerRow.CreateCell(1).SetCellValue("Description");
            headerRow.CreateCell(2).SetCellValue("Comment");
            headerRow.CreateCell(3).SetCellValue("DataType");
            headerRow.CreateCell(4).SetCellValue("Value");

            int rowIndex = 1;
            foreach (var kvp in config.VIDS)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(kvp.Value.ID);
                row.CreateCell(1).SetCellValue(kvp.Value.Description);
                row.CreateCell(2).SetCellValue(kvp.Value.Comment ?? string.Empty);
                row.CreateCell(3).SetCellValue(kvp.Value.DataType.ToString());
                row.CreateCell(4).SetCellValue(kvp.Value.Value?.ToString() ?? string.Empty);
            }

            for (int i = 0; i < 5; i++)
            {
                sheet.AutoSizeColumn(i);
            }
        }
        #endregion



        #region IncentiveCommand
        public static async Task<bool> LoadIncentiveCommandFromExcel(string filePath, ConcurrentDictionary<string, SFCommand> _commandDictionary)
        {
            try
            {
                _commandDictionary.Clear();


                // 如果Excel文件不存在，初始化空字典
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Console.WriteLine($"Excel文件不存在，初始化空的应答命令集: {filePath}");
                    return true;
                }
                // 读取Excel文件
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                IWorkbook workbook = null;

                // 根据文件扩展名决定使用HSSF还是XSSF
                if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook(fileStream);
                }
                else if (filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fileStream);
                }
                else
                {
                    throw new NotSupportedException($"不支持的文件格式: {Path.GetExtension(filePath)}");
                }


                var sheet = workbook.GetSheet("IncentiveCommands");
                if (sheet == null)
                {
                    return false;
                }

                var headerRow = sheet.GetRow(0);

                // 验证表头
                if (headerRow == null)
                {
                    throw new InvalidDataException("Excel文件缺少表头行");
                }

                // 获取列索引
                var columnIndices = new Dictionary<string, int>();
                for (int i = 0; i < headerRow.LastCellNum; i++)
                {
                    var cellValue = GetCellValue(headerRow.GetCell(i));
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        columnIndices[cellValue.Trim()] = i;
                    }
                }

                // 验证必需的列
                var requiredColumns = new[] { "Stream", "Function", "Name", "ID", "Message", "ResponseID" };
                foreach (var col in requiredColumns)
                {
                    if (!columnIndices.ContainsKey(col))
                    {
                        throw new InvalidDataException($"Excel文件中缺少必需的列: {col}");
                    }
                }

                // 从第二行开始读取数据
                for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var dataRow = sheet.GetRow(rowIndex);
                    if (dataRow == null) continue;

                    try
                    {
                        var streamCell = dataRow.GetCell(columnIndices["Stream"]);
                        var functionCell = dataRow.GetCell(columnIndices["Function"]);
                        var nameCell = dataRow.GetCell(columnIndices["Name"]);
                        var idCell = dataRow.GetCell(columnIndices["ID"]);
                        var messageCell = dataRow.GetCell(columnIndices["Message"]);
                        var responseidCell = dataRow.GetCell(columnIndices["ResponseID"]);
                        // 跳过Stream或Function为空的行
                        if (streamCell == null || functionCell == null) continue;

                        var streamStr = GetCellValue(streamCell);
                        var functionStr = GetCellValue(functionCell);

                        if (!uint.TryParse(streamStr, out uint stream) ||
                            !uint.TryParse(functionStr, out uint function))
                        {
                            continue; // 跳过无效的数字行
                        }

                        // 获取Name和ID
                        var name = GetCellValue(nameCell);
                        var id = GetCellValue(idCell);
                        var messageJson = GetCellValue(messageCell);
                        // 创建SFCommand对象
                        var command = new SFCommand
                        {
                            Stream = stream,
                            Function = function,
                            Name = name,
                            ID = id
                        };


                        if (!string.IsNullOrWhiteSpace(messageJson))
                        {
                            try
                            {
                                command = SFCommand.FromJson(messageJson);

                                if (command == null)
                                {
                                    throw new JsonException();
                                }
                            }
                            catch (JsonException ex)
                            {
                                // 记录反序列化错误，但继续处理其他行
                                Console.WriteLine($"第 {rowIndex + 1} 行Message反序列化失败: {ex.Message}");
                            }
                        }

                        

                        // 添加命令到字典
                        var key = command.Name;
                        if (!_commandDictionary.ContainsKey(key))
                        {
                            _commandDictionary[key] = command;
                        }
                        else
                        {
                            // 如果键已存在，记录警告或跳过
                            Console.WriteLine($"警告: 第 {rowIndex + 1} 行的命令 {key} 已存在，跳过");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"第 {rowIndex + 1} 行处理失败: {ex.Message}");
                        return false;
                        // 继续处理其他行
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// 获取单元格的值（处理各种类型）
        /// </summary>
        private static string GetCellValue(ICell cell)
        {
            if (cell == null) return string.Empty;

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                CellType.Numeric => cell.NumericCellValue.ToString().Trim(),
                CellType.Boolean => cell.BooleanCellValue.ToString().Trim(),
                CellType.Formula => cell.CellFormula?.Trim() ?? string.Empty,
                CellType.Blank => string.Empty,
                _ => cell.ToString()?.Trim() ?? string.Empty
            };
        }



        public static async Task SaveIncentiveCommandToExcel(string filePath,ConcurrentDictionary<string, SFCommand> _commandDictionary)
        {
            IWorkbook workbook;
            var savePath = filePath ;
            if (string.IsNullOrEmpty(savePath))
            {
                throw new ArgumentException("保存路径不能为空");
            }

            if (File.Exists(filePath))
            {
                using var fileStreamread = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (savePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook(fileStreamread);
                }
                else if (savePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fileStreamread);
                }
                else
                {
                    // 默认使用.xlsx格式
                    savePath = Path.ChangeExtension(savePath, ".xlsx");
                    workbook = new XSSFWorkbook(fileStreamread);
                }
            }
            else
            {
                if (savePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook();
                }
                else if (savePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook();
                }
                else
                {
                    // 默认使用.xlsx格式
                    savePath = Path.ChangeExtension(savePath, ".xlsx");
                    workbook = new XSSFWorkbook();
                }
            }

            var sheet = workbook.CreateSheet("IncentiveCommands");

            // 创建表头
            var headerRow = sheet.CreateRow(0);
            var headers = new[] { "Stream", "Function", "Name", "ID", "Message", "ResponseID" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
            }

            // 填充数据
            int rowIndex = 1;
            foreach (var command in _commandDictionary.Values.OrderBy(c => c.Stream).ThenBy(c => c.Function))
            {
                var row = sheet.CreateRow(rowIndex++);

                row.CreateCell(0).SetCellValue((int)command.Stream);
                row.CreateCell(1).SetCellValue((int)command.Function);
                row.CreateCell(2).SetCellValue(command.Name ?? string.Empty);
                row.CreateCell(3).SetCellValue(command.ID ?? string.Empty);

                var messageCell = row.CreateCell(4);
                if (command.Message != null)
                {
                    var messageJson = (command.ToJson());
                    messageCell.SetCellValue(messageJson);
                }
                row.CreateCell(5).SetCellValue(command.ResponseID ?? string.Empty);
            }

            // 保存文件
            using var fileStream = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            workbook.Write(fileStream);

            await Task.CompletedTask;
        }

        #endregion



        #region ResponseCommand
        public static async Task<bool> LoadResponseCommandFromExcel(string filePath, ConcurrentDictionary<string, SFCommand> _commandDictionary)
        {
            try
            {
                _commandDictionary.Clear();


                // 如果Excel文件不存在，初始化空字典
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Console.WriteLine($"Excel文件不存在，初始化空的应答命令集: {filePath}");
                    return true;
                }
                // 读取Excel文件
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                IWorkbook workbook = null;

                // 根据文件扩展名决定使用HSSF还是XSSF
                if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook(fileStream);
                }
                else if (filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fileStream);
                }
                else
                {
                    throw new NotSupportedException($"不支持的文件格式: {Path.GetExtension(filePath)}");
                }


                var sheet = workbook.GetSheet("ResponseCommands");
                if (sheet == null)
                {
                    return false;
                }

                var headerRow = sheet.GetRow(0);

                // 验证表头
                if (headerRow == null)
                {
                    throw new InvalidDataException("Excel文件缺少表头行");
                }

                // 获取列索引
                var columnIndices = new Dictionary<string, int>();
                for (int i = 0; i < headerRow.LastCellNum; i++)
                {
                    var cellValue = GetCellValue(headerRow.GetCell(i));
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        columnIndices[cellValue.Trim()] = i;
                    }
                }

                // 验证必需的列
                var requiredColumns = new[] { "Stream", "Function", "Name", "ID", "Message" };
                foreach (var col in requiredColumns)
                {
                    if (!columnIndices.ContainsKey(col))
                    {
                        throw new InvalidDataException($"Excel文件中缺少必需的列: {col}");
                    }
                }

                // 从第二行开始读取数据
                for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var dataRow = sheet.GetRow(rowIndex);
                    if (dataRow == null) continue;

                    try
                    {
                        var streamCell = dataRow.GetCell(columnIndices["Stream"]);
                        var functionCell = dataRow.GetCell(columnIndices["Function"]);
                        var nameCell = dataRow.GetCell(columnIndices["Name"]);
                        var idCell = dataRow.GetCell(columnIndices["ID"]);
                        var messageCell = dataRow.GetCell(columnIndices["Message"]);

                        // 跳过Stream或Function为空的行
                        if (streamCell == null || functionCell == null) continue;

                        var streamStr = GetCellValue(streamCell);
                        var functionStr = GetCellValue(functionCell);

                        if (!uint.TryParse(streamStr, out uint stream) ||
                            !uint.TryParse(functionStr, out uint function))
                        {
                            continue; // 跳过无效的数字行
                        }

                        // 获取Name和ID
                        var name = GetCellValue(nameCell);
                        var id = GetCellValue(idCell);
                        var messageJson = GetCellValue(messageCell);
                        // 创建SFCommand对象
                        var command = new SFCommand
                        {
                            Stream = stream,
                            Function = function,
                            Name = name,
                            ID = id
                        };


                        if (!string.IsNullOrWhiteSpace(messageJson))
                        {
                            try
                            {
                                command = SFCommand.FromJson(messageJson);

                                if (command == null)
                                {
                                    throw new JsonException();
                                }
                            }
                            catch (JsonException ex)
                            {
                                // 记录反序列化错误，但继续处理其他行
                                Console.WriteLine($"第 {rowIndex + 1} 行Message反序列化失败: {ex.Message}");
                            }
                        }

                        // 添加命令到字典
                        var key = command.Name;
                        if (!_commandDictionary.ContainsKey(key))
                        {
                            _commandDictionary[key] = command;
                        }
                        else
                        {
                            // 如果键已存在，记录警告或跳过
                            Console.WriteLine($"警告: 第 {rowIndex + 1} 行的命令 {key} 已存在，跳过");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"第 {rowIndex + 1} 行处理失败: {ex.Message}");
                        return false;
                        // 继续处理其他行
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }



        public static async Task SaveResponseCommandToExcel(string filePath, ConcurrentDictionary<string, SFCommand> _commandDictionary)
        {
            IWorkbook workbook;
            var savePath = filePath;
            if (string.IsNullOrEmpty(savePath))
            {
                throw new ArgumentException("保存路径不能为空");
            }

            if (File.Exists(filePath))
            {
                using var fileStreamread = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (savePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook(fileStreamread);
                }
                else if (savePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook(fileStreamread);
                }
                else
                {
                    // 默认使用.xlsx格式
                    savePath = Path.ChangeExtension(savePath, ".xlsx");
                    workbook = new XSSFWorkbook(fileStreamread);
                }
            }
            else
            {
                if (savePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new XSSFWorkbook();
                }
                else if (savePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    workbook = new HSSFWorkbook();
                }
                else
                {
                    // 默认使用.xlsx格式
                    savePath = Path.ChangeExtension(savePath, ".xlsx");
                    workbook = new XSSFWorkbook();
                }
            }

            var sheet = workbook.CreateSheet("ResponseCommands");

            // 创建表头
            var headerRow = sheet.CreateRow(0);
            var headers = new[] { "Stream", "Function", "Name", "ID", "Message" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
            }

            // 填充数据
            int rowIndex = 1;
            foreach (var command in _commandDictionary.Values.OrderBy(c => c.Stream).ThenBy(c => c.Function))
            {
                var row = sheet.CreateRow(rowIndex++);

                row.CreateCell(0).SetCellValue((int)command.Stream);
                row.CreateCell(1).SetCellValue((int)command.Function);
                row.CreateCell(2).SetCellValue(command.Name ?? string.Empty);
                row.CreateCell(3).SetCellValue(command.ID ?? string.Empty);

                var messageCell = row.CreateCell(4);
                if (command.Message != null)
                {
                    var messageJson = (command.ToJson());
                    messageCell.SetCellValue(messageJson);
                }
            }

            // 保存文件
            using var fileStream = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            workbook.Write(fileStream);

            await Task.CompletedTask;
        }
        #endregion

    }
}
