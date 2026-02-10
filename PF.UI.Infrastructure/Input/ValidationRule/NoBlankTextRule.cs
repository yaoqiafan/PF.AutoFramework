using System.Globalization;
using System.Windows.Controls;

namespace PF.UI.Shared.Tools;

public class NoBlankTextRule : ValidationRule
{
    public string ErrorContent { get; set; } = "必要";

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is not string text)
        {
            return new ValidationResult(false, "格式错误");
        }

        if (string.IsNullOrEmpty(text))
        {
            return new ValidationResult(false, ErrorContent);
        }

        return ValidationResult.ValidResult;
    }
}
