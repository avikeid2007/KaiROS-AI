using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Text.RegularExpressions;

namespace KaiROS.AI.Uno.Converters;

/// <summary>
/// Converts markdown text to formatted content for display
/// Note: This is a simplified version. For full markdown support, consider using a markdown library.
/// </summary>
public class MarkdownContentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string text || string.IsNullOrEmpty(text))
            return string.Empty;

        // Basic markdown conversion for display
        // This is simplified - for full support use a proper markdown library
        var result = text;

        // Code blocks
        result = Regex.Replace(result, @"```(\w*)\n([\s\S]*?)```", match =>
            $"[CODE]\n{match.Groups[2].Value}\n[/CODE]");

        // Inline code
        result = Regex.Replace(result, @"`([^`]+)`", match => $"[{match.Groups[1].Value}]");

        // Bold
        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", match => match.Groups[1].Value);

        // Italic
        result = Regex.Replace(result, @"\*([^*]+)\*", match => match.Groups[1].Value);

        // Headers (simplified)
        result = Regex.Replace(result, @"^### (.+)$", match => match.Groups[1].Value, RegexOptions.Multiline);
        result = Regex.Replace(result, @"^## (.+)$", match => match.Groups[1].Value, RegexOptions.Multiline);
        result = Regex.Replace(result, @"^# (.+)$", match => match.Groups[1].Value, RegexOptions.Multiline);

        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
