using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Logging.Converter
{
    /// <summary>
    /// 日志消息显示用截断：超过 <see cref="MaxChars"/> 字符或 <see cref="MaxLines"/> 行时只显示开头一段，末尾注明总长度。
    /// <para>日志列表的消息列是自动换行的 TextBlock，排版在界面线程上做；单条几 MB 的消息（例如把整条检测结果报文
    /// 写进日志）排版一次就要数秒，之后每次刷新、滚动还要重排，整个界面随之卡死。只截显示——复制、导出、搜索、
    /// 日志文件里仍是完整内容。</para>
    /// </summary>
    public class LogMessageDisplayConverter : IValueConverter
    {
        /// <summary>最多显示的字符数。</summary>
        public int MaxChars { get; set; } = 2000;

        /// <summary>最多显示的行数。</summary>
        public int MaxLines { get; set; } = 40;

        /// <summary>超长时截取开头并加说明；不超长原样返回。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string text) return value;

            int cut = -1, lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (i >= MaxChars) { cut = i; break; }
                if (text[i] == '\n' && ++lines > MaxLines) { cut = i; break; }
            }
            if (cut < 0) return text;

            int totalLines = 1;
            foreach (char c in text) if (c == '\n') totalLines++;
            return text[..cut].TrimEnd() +
                   $"\n…（这条日志共 {text.Length:N0} 字符 / {totalLines:N0} 行，这里只显示开头；复制、导出或日志文件里是完整内容）";
        }

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
