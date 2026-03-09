using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SignalBench.Converters;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool val)
        {
            var param = parameter?.ToString();
            if (param == "Streaming") return val ? new SolidColorBrush(Color.Parse("#107c10")) : new SolidColorBrush(Color.Parse("#2b579a"));
            if (param == "Recording") return new SolidColorBrush(Color.Parse("#e81123"));
            if (val) return new SolidColorBrush(Color.Parse("#107c10"));
        }
        return new SolidColorBrush(Colors.Black);
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StreamingStateToIndicatorColorConverter : IMultiValueConverter
{
    public static readonly StreamingStateToIndicatorColorConverter Instance = new();
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;
        bool isStreaming = values[0] is bool s && s;
        bool isPaused = values[1] is bool p && p;
        if (isStreaming && !isPaused) return new SolidColorBrush(Color.Parse("#d83b01"));
        if (isPaused) return new SolidColorBrush(Color.Parse("#107c10"));
        return Brushes.Transparent;
    }
}

public class StreamingStateToBadgeVisibilityConverter : IMultiValueConverter
{
    public static readonly StreamingStateToBadgeVisibilityConverter Instance = new();
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;
        bool isStreaming = values[0] is bool s && s;
        bool isPaused = values[1] is bool p && p;
        return isStreaming || isPaused;
    }
}

public class SourceTypeToBadgeVisibilityConverter : IValueConverter
{
    public static readonly SourceTypeToBadgeVisibilityConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SignalBench.ViewModels.PlotSourceType actualType && parameter is string targetTypeStr) return actualType.ToString() == targetTypeStr;
        return false;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
