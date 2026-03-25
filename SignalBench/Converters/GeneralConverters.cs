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

public class ValueWithUnitConverter : IMultiValueConverter
{
    public static readonly ValueWithUnitConverter Instance = new();
    public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] == null || values[0] is not double d) return "n/a";
        
        string val = d.ToString("G5");
        string? unit = null;
        if (values.Count > 1)
        {
            if (values[1] is string s) unit = s;
            else if (values[1] is SignalBench.ViewModels.SignalItemViewModel item) unit = item.Unit;
        }

        if (!string.IsNullOrEmpty(unit))
        {
            return $"{val} {unit}";
        }
        return val;
    }
    public object? ConvertBack(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CollectionEmptyToBoolConverter : IValueConverter
{
    public static readonly CollectionEmptyToBoolConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count) return count == 0;
        if (value is long lCount) return lCount == 0;
        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            return !enumerator.MoveNext();
        }
        return true;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CollectionNotEmptyToBoolConverter : IValueConverter
{
    public static readonly CollectionNotEmptyToBoolConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count) return count > 0;
        if (value is long lCount) return lCount > 0;
        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            return enumerator.MoveNext();
        }
        return false;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
