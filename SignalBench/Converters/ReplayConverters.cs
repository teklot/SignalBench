using Avalonia.Data.Converters;
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
            return isPlaying ? "Pause" : "Play";
        }
        return "Play";
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
            return isRecording ? "Stop" : "Record";
        }
        return "Record";
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
            return isStreaming ? "Stop" : "Play";
        }
        return "Play";
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
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#107c10")); // Green
        if (isPaused)
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#d83b01")); // Red
        
        return Avalonia.Media.Brushes.Transparent;
    }
}
