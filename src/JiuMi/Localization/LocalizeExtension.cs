using System;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using JiuMi.ViewModels;

namespace JiuMi.Localization;

/// <summary>
/// Binds a property to a localization key resolved through the shell's localization service.
/// </summary>
public sealed class LocalizeExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizeExtension"/> class.
    /// </summary>
    public LocalizeExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizeExtension"/> class.
    /// </summary>
    /// <param name="key">The localization key to display.</param>
    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Gets or sets the localization key to display.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return new Binding
        {
            Path = $"DataContext.{nameof(MainWindowViewModel.Localization)}",
            Converter = LocalizedStringConverter.Instance,
            ConverterParameter = Key,
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window),
            },
            Mode = BindingMode.OneWay,
        };
    }
}
