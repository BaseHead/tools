using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AISoundGenerator.ViewModels;

namespace AISoundGenerator.ViewModels;

public class GenerationTypeToWatermarkConverter : IValueConverter
{
    public static readonly GenerationTypeToWatermarkConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GenerationType generationType)
        {
            return generationType switch
            {
                GenerationType.SFX => "Describe the sound effect you want...",
                GenerationType.Speech => "Enter the text you want to convert...",
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}