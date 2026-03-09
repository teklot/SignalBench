using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SignalBench.Converters;

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string options)
        {
            var parts = options.Split('|');
            if (parts.Length == 2) return boolValue ? parts[0] : parts[1];
        }
        return value?.ToString();
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DoubleToStringConverter : IValueConverter
{
    public static readonly DoubleToStringConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return $"{d}x";
        return "1x";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            var cleaned = s.Replace("x", "").Trim();
            if (double.TryParse(cleaned, NumberStyles.Any, culture, out var result)) return result;
        }
        return 1.0;
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b && b ? 1.0 : 0.0;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SignalItemToNameConverter : IValueConverter
{
    public static readonly SignalItemToNameConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s) return s;
        if (value is SignalBench.ViewModels.SignalItemViewModel item) return item.Name;
        return null;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SignalBench.ViewModels.SignalItemViewModel item) return item.Name;
        return value?.ToString();
    }
}
