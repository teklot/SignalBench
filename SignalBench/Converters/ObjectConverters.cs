using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SignalBench.Converters;

public static class ObjectConverters
{
    public static readonly IValueConverter IsNotNull = 
        new FuncValueConverter<object?, bool>(x => x is not null);

    public static readonly IValueConverter IsNull = 
        new FuncValueConverter<object?, bool>(x => x is null);
}
