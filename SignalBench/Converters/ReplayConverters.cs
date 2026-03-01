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
