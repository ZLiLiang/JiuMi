using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// One row of the selected places list in the manage dialog.
/// </summary>
public sealed class WorldClockSelectedPlaceViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockSelectedPlaceViewModel"/> class.
    /// </summary>
    /// <param name="place">The selected place.</param>
    /// <param name="index">The display index.</param>
    /// <param name="primaryName">The name in the interface language.</param>
    /// <param name="secondaryName">The name in the other language.</param>
    /// <param name="dialCode">The country calling code.</param>
    /// <param name="kindText">The localized country or city badge.</param>
    /// <param name="removeCommand">Removes the place.</param>
    /// <param name="editCommand">Opens the working-hours editor.</param>
    public WorldClockSelectedPlaceViewModel(
        Place place,
        int index,
        string primaryName,
        string secondaryName,
        string dialCode,
        string kindText,
        IRelayCommand<WorldClockSelectedPlaceViewModel> removeCommand,
        IRelayCommand<WorldClockSelectedPlaceViewModel> editCommand)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(removeCommand);
        ArgumentNullException.ThrowIfNull(editCommand);
        Place = place;
        Index = index;
        PrimaryName = primaryName;
        SecondaryName = secondaryName;
        DialCode = dialCode;
        KindText = kindText;
        RemoveCommand = removeCommand;
        EditCommand = editCommand;
    }

    /// <summary>
    /// Gets the selected place.
    /// </summary>
    public Place Place
    {
        get;
    }

    /// <summary>
    /// Gets the display index.
    /// </summary>
    public int Index
    {
        get;
    }

    /// <summary>
    /// Gets the name in the interface language.
    /// </summary>
    public string PrimaryName
    {
        get;
    }

    /// <summary>
    /// Gets the name in the other language.
    /// </summary>
    public string SecondaryName
    {
        get;
    }

    /// <summary>
    /// Gets the country calling code.
    /// </summary>
    public string DialCode
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the row has a dial code.
    /// </summary>
    public bool HasDialCode => !string.IsNullOrEmpty(DialCode);

    /// <summary>
    /// Gets the localized country or city badge.
    /// </summary>
    public string KindText
    {
        get;
    }

    /// <summary>
    /// Gets the command that removes the place.
    /// </summary>
    public IRelayCommand<WorldClockSelectedPlaceViewModel> RemoveCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that opens the working-hours editor.
    /// </summary>
    public IRelayCommand<WorldClockSelectedPlaceViewModel> EditCommand
    {
        get;
    }
}
