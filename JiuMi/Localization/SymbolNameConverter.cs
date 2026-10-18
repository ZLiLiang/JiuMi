using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace JiuMi.Localization;

/// <summary>
/// Turns a navigation icon name into the Fluent symbol the shell renders.
/// </summary>
public sealed class SymbolNameConverter : IValueConverter
{
    /// <summary>
    /// Gets the shared converter instance used by the navigation item template.
    /// </summary>
    public static SymbolNameConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name && Enum.TryParse(name, ignoreCase: false, out FASymbol symbol)
            ? symbol
            : default(FASymbol);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
