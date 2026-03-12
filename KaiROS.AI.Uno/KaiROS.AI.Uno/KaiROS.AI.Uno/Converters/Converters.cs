using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace KaiROS.AI.Uno.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
            var isVisible = invert ? !boolValue : boolValue;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }
}

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null) return string.Empty;
        var format = parameter?.ToString();
        return string.IsNullOrEmpty(format) ? value.ToString() : string.Format(format, value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
        var isNull = value == null || (value is string s && string.IsNullOrEmpty(s));
        var isVisible = invert ? isNull : !isNull;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
