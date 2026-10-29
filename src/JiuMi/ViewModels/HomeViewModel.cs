using CommunityToolkit.Mvvm.Input;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the home page. Its text and cards are bound from the view.
/// </summary>
public sealed class HomeViewModel : ViewModelBase
{
    private IRelayCommand? _openWorldClockCommand;
    private IRelayCommand? _openImageConvertCommand;

    /// <summary>
    /// Gets or sets the command that opens the world clock page from its tool card.
    /// </summary>
    public IRelayCommand? OpenWorldClockCommand
    {
        get => _openWorldClockCommand;
        set => SetProperty(ref _openWorldClockCommand, value);
    }

    /// <summary>
    /// Gets or sets the command that opens the image conversion page from its tool card.
    /// </summary>
    public IRelayCommand? OpenImageConvertCommand
    {
        get => _openImageConvertCommand;
        set => SetProperty(ref _openImageConvertCommand, value);
    }
}
