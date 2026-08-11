namespace MyAccountingApp.Core.Imports.Common;
using System.Globalization;
using System.Text;

public static class CsvParsing
{
    public static decimal ParseEuropeanDecimal(string value)
    {
        string trimmed = value.Trim();
        bool negative = trimmed.StartsWith('-');

        StringBuilder digits = new StringBuilder(trimmed.Length);
        foreach (char c in trimmed)
        {
            if (char.IsDigit(c) || c == '.' || c == ',')
            {
                digits.Append(c);
            }
        }

        string cleaned = digits.ToString();
        int lastDot = cleaned.LastIndexOf('.');
        int lastComma = cleaned.LastIndexOf(',');
        if (lastComma > lastDot)
        {
            cleaned = cleaned.Replace(".", string.Empty).Replace(",", ".");
        }
        else if (lastDot > lastComma)
        {
            string trailing = cleaned[(lastDot + 1) ..];
            if (trailing.Length == 3 && lastDot > 0)
            {
                cleaned = cleaned.Replace(".", string.Empty);
            }
            else
            {
                cleaned = cleaned.Replace(",", string.Empty);
            }
        }

        if (negative)
        {
            cleaned = "-" + cleaned;
        }

        return decimal.Parse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture);
    }
}