using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AISoundGenerator.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public static readonly EnumToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return null;
        return (bool)value ? Enum.Parse(targetType, parameter.ToString()!) : null;
    }
}