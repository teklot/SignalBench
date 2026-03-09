using Avalonia.Data.Converters;
using Material.Icons;
using System;
using System.Collections.Generic;
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
            if (param == "Text" || param == "Tooltip") return isPlaying ? "Stop Streaming" : "Start Streaming";
            return isPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;
        }
        return MaterialIconKind.Play;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToRecordIconConverter : IValueConverter
{
    public static readonly BoolToRecordIconConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording)
        {
            if (parameter?.ToString() == "Tooltip") return isRecording ? "Stop Recording" : "Start Recording";
            return isRecording ? MaterialIconKind.Stop : MaterialIconKind.Record;
        }
        return MaterialIconKind.Record;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToStreamBadgeIconConverter : IValueConverter
{
    public static readonly BoolToStreamBadgeIconConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool isStreaming && isStreaming ? MaterialIconKind.Stop : MaterialIconKind.Play;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StreamingStateToBadgeIconConverter : IMultiValueConverter
{
    public static readonly StreamingStateToBadgeIconConverter Instance = new();
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return MaterialIconKind.Play;
        bool isStreaming = values[0] is bool s && s;
        bool isPaused = values[1] is bool p && p;
        if (isStreaming && !isPaused) return MaterialIconKind.Pause;
        return MaterialIconKind.Play;
    }
}
