using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;

namespace PF.Modules.Identity.Helpers
{
    /// <summary>
    /// 用户批量导入 Excel 辅助类：导出模板 / 解析导入文件（仅工号 + 密码两列）
    /// </summary>
    public static class UserImportExcelHelper
    {
        /// <summary>导入文件中的一行原始数据</summary>
        public record ImportRow(int RowNumber, string UserName, string Password);

        /// <summary>导出批量导入模板（表头 + 一行示例数据）</summary>
        public static void ExportTemplate(string filePath)
        {
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("用户导入模板");

            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("工号");
            header.CreateCell(1).SetCellValue("密码");

            var sample = sheet.CreateRow(1);
            sample.CreateCell(0).SetCellValue("1001");
            sample.CreateCell(1).SetCellValue("123456");

            sheet.AutoSizeColumn(0);
            sheet.AutoSizeColumn(1);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        /// <summary>解析导入文件，跳过表头行与空行</summary>
        public static List<ImportRow> ParseImportFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            IWorkbook workbook = filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                ? new HSSFWorkbook(fs)
                : new XSSFWorkbook(fs);

            var sheet = workbook.GetSheetAt(0);
            var result = new List<ImportRow>();
            if (sheet == null) return result;

            for (int i = 1; i <= sheet.LastRowNum; i++) // 第 0 行是表头
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var userName = GetCellValue(row.GetCell(0)).Trim();
                var password = GetCellValue(row.GetCell(1)).Trim();
                if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password))
                    continue; // 跳过空行

                result.Add(new ImportRow(i + 1, userName, password));
            }

            return result;
        }

        private static string GetCellValue(ICell? cell)
        {
            if (cell == null) return string.Empty;

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => cell.CellFormula?.Trim() ?? string.Empty,
                CellType.Blank => string.Empty,
                _ => cell.ToString()?.Trim() ?? string.Empty
            };
        }
    }
}
