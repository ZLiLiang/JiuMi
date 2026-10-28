using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// One row of the manage-places result list, or a country group header.
/// </summary>
public sealed class WorldClockCatalogItemViewModel : ObservableObject
{
    private readonly IRelayCommand<WorldClockCatalogItemViewModel> _toggleCommand;
    private bool _isSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockCatalogItemViewModel"/> class.
    /// </summary>
    /// <param name="item">The catalog item, or <see langword="null"/> for a group header.</param>
    /// <param name="groupName">The group name for a header.</param>
    /// <param name="isSelected">Whether the item is already in the selected list.</param>
    /// <param name="toggleCommand">The command that adds or removes the item.</param>
    public WorldClockCatalogItemViewModel(
        CatalogItem? item,
        string? groupName,
        bool isSelected,
        IRelayCommand<WorldClockCatalogItemViewModel> toggleCommand)
    {
        ArgumentNullException.ThrowIfNull(toggleCommand);
        Item = item;
        GroupName = groupName ?? string.Empty;
        _isSelected = isSelected;
        _toggleCommand = toggleCommand;
    }

    /// <summary>
    /// Gets the catalog item, or <see langword="null"/> for a group header.
    /// </summary>
    public CatalogItem? Item
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether this row is a country group header.
    /// </summary>
    public bool IsGroupHeader => Item is null;

    /// <summary>
    /// Gets a value indicating whether this row is a selectable item.
    /// </summary>
    public bool IsItem => Item is not null;

    /// <summary>
    /// Gets the group name of a header.
    /// </summary>
    public string GroupName
    {
        get;
    } = string.Empty;

    /// <summary>
    /// Gets the name in the interface language.
    /// </summary>
    public string PrimaryName => Item?.PrimaryName ?? string.Empty;

    /// <summary>
    /// Gets the name in the other language.
    /// </summary>
    public string SecondaryName => Item?.SecondaryName ?? string.Empty;

    /// <summary>
    /// Gets the country calling code.
    /// </summary>
    public string DialCode => Item?.DialCode ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the item has a dial code.
    /// </summary>
    public bool HasDialCode => !string.IsNullOrEmpty(DialCode);

    /// <summary>
    /// Gets or sets a value indicating whether the item is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Gets the command that adds or removes the item.
    /// </summary>
    public IRelayCommand<WorldClockCatalogItemViewModel> ToggleCommand => _toggleCommand;
}
