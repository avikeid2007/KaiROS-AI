using KaiROS.AI.Models;

using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KaiROS.AI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool invert = parameter?.ToString() == "Invert";
        bool boolValue = false;

        if (value is bool b)
            boolValue = b;
        else if (value is string s)
            boolValue = !string.IsNullOrWhiteSpace(s);
        else if (value is int i)
            boolValue = i > 0;
        else
            boolValue = value != null;

        if (invert) boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && !b;
    }
}

public class CategoryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString()?.ToLower() switch
        {
            "small" => "#10B981",
            "medium" => "#F59E0B",
            "large" => "#EF4444",
            _ => "#6B7280"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class DownloadStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            DownloadState.NotStarted => "â¬‡",
            DownloadState.Downloading => "â¸",
            DownloadState.Paused => "â–¶",
            DownloadState.Completed => "âœ“",
            DownloadState.Failed => "âœ•",
            DownloadState.Verifying => "â³",
            _ => "?"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BackendToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            ExecutionBackend.Cpu => "CPU",
            ExecutionBackend.Cuda => "CUDA (NVIDIA)",
            ExecutionBackend.Npu => "NPU",
            ExecutionBackend.Auto => "Auto-detect",
            _ => "Unknown"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !string.IsNullOrWhiteSpace(value?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class ProgressToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double progress && parameter is double maxWidth)
        {
            return (progress / 100.0) * maxWidth;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

// NOTE: ProgressToWidthMultiConverter removed — WinUI 3 does not support IMultiValueConverter.
// The ModernProgressBar style now uses WinUI 3's built-in ProgressBar which calculates fill width internally.

public class UrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                // WinUI 3: BitmapImage uses UriSource directly (no BeginInit/EndInit)
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.DecodePixelWidth = 48; // Optimize for display size
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats a value using string.Format with the ConverterParameter as the format string.
/// XAML usage: ConverterParameter='{0:F1}%' or ConverterParameter='Port: {0}'
/// To escape a leading brace in XAML use: ConverterParameter='{}{0:F1}%'
/// </summary>
public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string fmt)
            return string.Format(fmt, value);
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
