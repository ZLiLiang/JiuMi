using System;
using System.Globalization;
using Avalonia.Data.Converters;
using JiuMi.Application.Localization;

namespace JiuMi.Localization;

/// <summary>
/// Turns the localization service into the text for the key passed as the converter parameter.
/// </summary>
public sealed class LocalizedStringConverter : IValueConverter
{
    /// <summary>
    /// Gets the shared converter instance used by the localization markup extension.
    /// </summary>
    public static LocalizedStringConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string key)
        {
            return null;
        }

        return value is ILocalizationService localization ? localization[key] : key;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
