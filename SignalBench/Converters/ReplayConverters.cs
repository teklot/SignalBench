using Avalonia.Data.Converters;
using Material.Icons;
using System.Globalization;

namespace SignalBench.Converters;

public class BoolToPlayIconConverter : IValueConverter
{
    public static readonly BoolToPlayIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
        {
            var param = parameter?.ToString();
            if (param == "Text" || param == "Tooltip")
            {
                return isPlaying ? "Stop Streaming" : "Start Streaming";
            }
            return isPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;
        }
        return MaterialIconKind.Play;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToRecordIconConverter : IValueConverter
{
    public static readonly BoolToRecordIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording)
        {
            if (parameter?.ToString() == "Tooltip")
            {
                return isRecording ? "Stop Recording" : "Start Recording";
            }
            return isRecording ? MaterialIconKind.Stop : MaterialIconKind.Record;
        }
        return MaterialIconKind.Record;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return b ? 1.0 : 0.0;
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DoubleToStringConverter : IValueConverter
{
    public static readonly DoubleToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d}x";
        }
        return "1x";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return double.Parse(s.Replace("x", ""));
        }
        return 1.0;
    }
}

public class BoolToStreamBadgeIconConverter : IValueConverter
{
    public static readonly BoolToStreamBadgeIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isStreaming)
        {
            return isStreaming ? MaterialIconKind.Stop : MaterialIconKind.Play;
        }
        return MaterialIconKind.Play;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StreamingStateToBadgeIconConverter : IMultiValueConverter
{
    public static readonly StreamingStateToBadgeIconConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return MaterialIconKind.Play;
        
        bool isStreaming = values[0] is bool s && s;
        bool isPaused = values[1] is bool p && p;

        if (isStreaming && !isPaused)
            return MaterialIconKind.Pause; // Ready to pause
        if (isPaused)
            return MaterialIconKind.Play; // Ready to play/resume
        
        return MaterialIconKind.Play;
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
        if (value is SignalBench.ViewModels.PlotSourceType actualType && parameter is string targetTypeStr)
        {
            return actualType.ToString() == targetTypeStr;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StreamingStateToIndicatorColorConverter : IMultiValueConverter
{
    public static readonly StreamingStateToIndicatorColorConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Avalonia.Media.Brushes.Transparent;
        
        bool isStreaming = values[0] is bool s && s;
        bool isPaused = values[1] is bool p && p;

        if (isStreaming && !isPaused)
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#d83b01")); // Orange (Pause action)
        if (isPaused)
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#107c10")); // Green (Play action)
        
        return Avalonia.Media.Brushes.Transparent;
    }
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
