using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace SignalBench.Converters;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDerived && isDerived)
        {
            return new SolidColorBrush(Color.Parse("#107c10"));
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
