using System;
using CommunityToolkit.Mvvm.ComponentModel;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// One item in the shell's navigation rail.
/// </summary>
public sealed class ShellNavigationItem : ObservableObject
{
    private readonly string _labelKey;
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellNavigationItem"/> class.
    /// </summary>
    /// <param name="id">The stable identifier used by the shell.</param>
    /// <param name="labelKey">The localization key of the item text.</param>
    /// <param name="iconName">The Fluent symbol name shown when the item is not selected.</param>
    /// <param name="selectedIconName">The Fluent symbol name shown when the item is selected.</param>
    /// <param name="page">The page this item opens; null for items that open nothing.</param>
    /// <param name="isEnabled">Whether the item can be focused and selected.</param>
    /// <param name="isComingSoon">Whether the item shows the coming-soon badge.</param>
    /// <param name="localization">The localization service that supplies the item text.</param>
    public ShellNavigationItem(
        string id,
        string labelKey,
        string iconName,
        string selectedIconName,
        ShellPage? page,
        bool isEnabled,
        bool isComingSoon,
        ILocalizationService localization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconName);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedIconName);
        ArgumentNullException.ThrowIfNull(localization);

        Id = id;
        IconName = iconName;
        SelectedIconName = selectedIconName;
        Page = page;
        IsEnabled = isEnabled;
        IsComingSoon = isComingSoon;
        _labelKey = labelKey;
        _localization = localization;
    }

    /// <summary>
    /// Gets the stable identifier used by the shell.
    /// </summary>
    public string Id
    {
        get;
    }

    /// <summary>
    /// Gets the page this item opens; null for items that open nothing.
    /// </summary>
    public ShellPage? Page
    {
        get;
    }

    /// <summary>
    /// Gets the Fluent symbol name shown when the item is not selected.
    /// </summary>
    public string IconName
    {
        get;
    }

    /// <summary>
    /// Gets the Fluent symbol name shown when the item is selected.
    /// </summary>
    public string SelectedIconName
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the item can be focused and selected.
    /// </summary>
    public bool IsEnabled
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the item shows the coming-soon badge.
    /// </summary>
    public bool IsComingSoon
    {
        get;
    }

    /// <summary>
    /// Gets the item text in the current language.
    /// </summary>
    public string Label => _localization[_labelKey];

    /// <summary>
    /// Raises a change notification for <see cref="Label"/> after the language changed.
    /// </summary>
    internal void RefreshLabel() => OnPropertyChanged(nameof(Label));
}
