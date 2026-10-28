using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.Localization;
using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// The state of the manage-places dialog: the segmented search, the selected list and the
/// working-hours editor.
/// </summary>
public sealed class WorldClockManageViewModel : ObservableObject, IDisposable
{
    private static readonly int[] _isoDays = [1, 2, 3, 4, 5, 6, 7];

    private readonly WorldClockViewModel _page;
    private readonly ILocalizationService _localization;
    private readonly IReadOnlyList<string> _timeOptions;
    private readonly IRelayCommand<WorldClockCatalogItemViewModel> _toggleCommand;
    private readonly IRelayCommand<WorldClockSelectedPlaceViewModel> _removeCommand;
    private readonly IRelayCommand<WorldClockSelectedPlaceViewModel> _editCommand;
    private WorldClockCatalog? _catalog;
    private WorldClockCatalogItemViewModel[] _results = [];
    private WorldClockSelectedPlaceViewModel[] _selected = [];
    private WorldClockDayToggleViewModel[] _editDays = [];
    private PlaceKind _mode = PlaceKind.City;
    private string _query = string.Empty;
    private bool _isEditing;
    private WorldClockSelectedPlaceViewModel? _editing;
    private string _editPlaceName = string.Empty;
    private string _selectedStart = "09:00";
    private string _selectedEnd = "18:00";
    private string? _editError;
    private bool _canSaveEdit;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockManageViewModel"/> class.
    /// </summary>
    /// <param name="page">The page whose places the dialog edits.</param>
    /// <param name="localization">Supplies the interface text.</param>
    public WorldClockManageViewModel(WorldClockViewModel page, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(localization);
        _page = page;
        _localization = localization;
        _timeOptions = BuildTimeOptions();
        _toggleCommand = new RelayCommand<WorldClockCatalogItemViewModel>(TogglePlace);
        _removeCommand = new RelayCommand<WorldClockSelectedPlaceViewModel>(RemovePlace);
        _editCommand = new RelayCommand<WorldClockSelectedPlaceViewModel>(BeginEdit);
        SetModeCommand = new RelayCommand<PlaceKind>(SetMode);
        ResetCommand = new RelayCommand(Reset);
        SaveEditCommand = new RelayCommand(SaveEdit, () => CanSaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Gets the localization service bound from the dialog.
    /// </summary>
    public ILocalizationService Localization => _localization;

    /// <summary>
    /// Gets the window title in the current language.
    /// </summary>
    public string WindowTitle => _localization["Tool.WorldClock.Manage.Title"];

    /// <summary>
    /// Gets the segmented control command.
    /// </summary>
    public IRelayCommand<PlaceKind> SetModeCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that restores the default places.
    /// </summary>
    public IRelayCommand ResetCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that saves the working-hours editor.
    /// </summary>
    public IRelayCommand SaveEditCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that closes the working-hours editor without saving.
    /// </summary>
    public IRelayCommand CancelEditCommand
    {
        get;
    }

    /// <summary>
    /// Gets the available start and end times in fifteen-minute steps.
    /// </summary>
    public IReadOnlyList<string> TimeOptions => _timeOptions;

    /// <summary>
    /// Gets a value indicating whether the country segment is active.
    /// </summary>
    public bool IsCountryMode => _mode == PlaceKind.Country;

    /// <summary>
    /// Gets a value indicating whether the city segment is active.
    /// </summary>
    public bool IsCityMode => _mode == PlaceKind.City;

    /// <summary>
    /// Gets the search text.
    /// </summary>
    public string SearchQuery
    {
        get => _query;
        set
        {
            string query = value ?? string.Empty;
            if (SetProperty(ref _query, query))
            {
                RefreshResults();
            }
        }
    }

    /// <summary>
    /// Gets the placeholder that follows the active segment.
    /// </summary>
    public string SearchPlaceholder => _localization[
        _mode == PlaceKind.Country
            ? "Tool.WorldClock.Manage.Search.Country"
            : "Tool.WorldClock.Manage.Search.City"];

    /// <summary>
    /// Gets the result rows, with country group headers in city mode.
    /// </summary>
    public IReadOnlyList<WorldClockCatalogItemViewModel> Results => _results;

    /// <summary>
    /// Gets a value indicating whether the result list has selectable rows.
    /// </summary>
    public bool HasResults => _results.Any(item => !item.IsGroupHeader);

    /// <summary>
    /// Gets the empty-result text for the active segment.
    /// </summary>
    public string NoResultsText => _localization[
        _mode == PlaceKind.Country
            ? "Tool.WorldClock.Manage.NoResults.Country"
            : "Tool.WorldClock.Manage.NoResults.City"];

    /// <summary>
    /// Gets the selected places, in display order.
    /// </summary>
    public IReadOnlyList<WorldClockSelectedPlaceViewModel> Selected => _selected;

    /// <summary>
    /// Gets the number of selected places.
    /// </summary>
    public int SelectedCount => _selected.Length;

    /// <summary>
    /// Gets a value indicating whether the working-hours editor is open.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    /// <summary>
    /// Gets the name of the place being edited.
    /// </summary>
    public string EditPlaceName
    {
        get => _editPlaceName;
        private set => SetProperty(ref _editPlaceName, value);
    }

    /// <summary>
    /// Gets or sets the editor's start time.
    /// </summary>
    public string SelectedStart
    {
        get => _selectedStart;
        set
        {
            if (SetProperty(ref _selectedStart, value ?? string.Empty))
            {
                ValidateEdit();
            }
        }
    }

    /// <summary>
    /// Gets or sets the editor's end time.
    /// </summary>
    public string SelectedEnd
    {
        get => _selectedEnd;
        set
        {
            if (SetProperty(ref _selectedEnd, value ?? string.Empty))
            {
                ValidateEdit();
            }
        }
    }

    /// <summary>
    /// Gets the working-day checkboxes of the editor.
    /// </summary>
    public IReadOnlyList<WorldClockDayToggleViewModel> EditDays => _editDays;

    /// <summary>
    /// Gets the validation message of the editor, or <see langword="null"/>.
    /// </summary>
    public string? EditError
    {
        get => _editError;
        private set
        {
            if (SetProperty(ref _editError, value))
            {
                OnPropertyChanged(nameof(HasEditError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the editor shows a validation message.
    /// </summary>
    public bool HasEditError => _editError is not null;

    /// <summary>
    /// Gets a value indicating whether the editor can save.
    /// </summary>
    public bool CanSaveEdit
    {
        get => _canSaveEdit;
        private set
        {
            if (SetProperty(ref _canSaveEdit, value))
            {
                SaveEditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _localization.PropertyChanged -= OnLocalizationChanged;
        foreach (WorldClockDayToggleViewModel toggle in _editDays)
        {
            toggle.PropertyChanged -= OnEditDayChanged;
        }
    }

    /// <summary>
    /// Supplies the catalog once the page has loaded it.
    /// </summary>
    /// <param name="catalog">The loaded catalog.</param>
    public void SetCatalog(WorldClockCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        RefreshFromPage();
    }

    /// <summary>
    /// Clears the search and closes the editor before the dialog is shown again.
    /// </summary>
    public void PrepareForOpen()
    {
        SearchQuery = string.Empty;
        if (_isEditing)
        {
            CloseEdit();
        }

        RefreshFromPage();
    }

    /// <summary>
    /// Refreshes the lists after the page's places changed.
    /// </summary>
    public void RefreshFromPage()
    {
        RefreshResults();
        RefreshSelected();

        if (_isEditing && _editing is not null && _page.Places.All(place => place.Key != _editing.Place.Key))
        {
            CloseEdit();
        }
    }

    /// <summary>
    /// Records a failure to start the drag operation.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public void ReportDragFailure(Exception exception) => _page.ReportManageFailure(exception);

    /// <summary>
    /// Moves a selected place to another position.
    /// </summary>
    /// <param name="fromIndex">The current index.</param>
    /// <param name="toIndex">The target index.</param>
    public void Move(int fromIndex, int toIndex) => _page.MovePlace(fromIndex, toIndex);

    private void SetMode(PlaceKind mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        OnPropertyChanged(nameof(IsCountryMode));
        OnPropertyChanged(nameof(IsCityMode));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(NoResultsText));
        RefreshResults();
    }

    private void RefreshResults()
    {
        if (_catalog is null)
        {
            _results = [];
            OnPropertyChanged(nameof(Results));
            OnPropertyChanged(nameof(HasResults));
            return;
        }

        HashSet<string> selectedKeys = [.. _page.Places.Select(place => place.Key)];
        IReadOnlyList<CatalogItem> items = _catalog.Search(
            _mode,
            _query,
            _localization.CurrentLanguage,
            _localization.Culture);
        var rows = new List<WorldClockCatalogItemViewModel>(items.Count + 8);

        if (_mode == PlaceKind.City && string.IsNullOrWhiteSpace(_query))
        {
            string? currentGroup = null;
            foreach (CatalogItem item in items)
            {
                if (!string.Equals(currentGroup, item.GroupName, StringComparison.Ordinal))
                {
                    currentGroup = item.GroupName;
                    rows.Add(new WorldClockCatalogItemViewModel(null, currentGroup, false, _toggleCommand));
                }

                rows.Add(new WorldClockCatalogItemViewModel(
                    item,
                    item.GroupName,
                    selectedKeys.Contains(item.Key),
                    _toggleCommand));
            }
        }
        else
        {
            foreach (CatalogItem item in items)
            {
                rows.Add(new WorldClockCatalogItemViewModel(
                    item,
                    item.GroupName,
                    selectedKeys.Contains(item.Key),
                    _toggleCommand));
            }
        }

        _results = [.. rows];
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(HasResults));
    }

    private void RefreshSelected()
    {
        var rows = new List<WorldClockSelectedPlaceViewModel>(_page.Places.Count);
        string countryBadge = _localization["Tool.WorldClock.Manage.Kind.Country"];
        string cityBadge = _localization["Tool.WorldClock.Manage.Kind.City"];

        int index = 0;
        foreach (Place place in _page.Places)
        {
            string primaryName = place.Id;
            string secondaryName = string.Empty;
            string dialCode = string.Empty;
            if (_catalog is not null &&
                _catalog.TryPresent(place, _localization.CurrentLanguage, out PlacePresentation? presentation) &&
                presentation is not null)
            {
                primaryName = presentation.PrimaryName;
                secondaryName = presentation.SecondaryName;
                dialCode = presentation.DialCode;
            }

            rows.Add(new WorldClockSelectedPlaceViewModel(
                place,
                index++,
                primaryName,
                secondaryName,
                dialCode,
                place.Kind == PlaceKind.Country ? countryBadge : cityBadge,
                _removeCommand,
                _editCommand));
        }

        _selected = [.. rows];
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(SelectedCount));
    }

    private void TogglePlace(WorldClockCatalogItemViewModel? item)
    {
        if (item?.Item is not { } catalogItem)
        {
            return;
        }

        bool selected = _page.Places.Any(place => place.Key == catalogItem.Key);
        if (selected)
        {
            _page.RemovePlace(catalogItem.Key);
        }
        else
        {
            _page.AddPlace(catalogItem.Kind, catalogItem.Id);
        }
    }

    private void RemovePlace(WorldClockSelectedPlaceViewModel? place)
    {
        if (place is not null)
        {
            _page.RemovePlace(place.Place.Key);
        }
    }

    private void Reset() => _page.ResetPlaces();

    private void BeginEdit(WorldClockSelectedPlaceViewModel? place)
    {
        if (place is null)
        {
            return;
        }

        _editing = place;
        EditPlaceName = place.PrimaryName;
        SelectedStart = FormatTime(place.Place.Hours.Start);
        SelectedEnd = FormatTime(place.Place.Hours.End);
        BuildEditDays(place.Place.Hours.Days);
        IsEditing = true;
        ValidateEdit();
    }

    private void SaveEdit()
    {
        if (!CanSaveEdit || _editing is null)
        {
            return;
        }

        TimeOnly start = TimeOnly.ParseExact(SelectedStart, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        TimeOnly end = TimeOnly.ParseExact(SelectedEnd, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        int[] days = [.. _editDays.Where(day => day.IsChecked).Select(day => day.IsoDay)];
        if (!WorkWindow.TryCreate(start, end, days, out WorkWindow? window, out _) || window is null)
        {
            return;
        }

        _page.SetPlaceHours(_editing.Place.Key, window);
        CloseEdit();
    }

    private void CancelEdit() => CloseEdit();

    private void CloseEdit()
    {
        _editing = null;
        IsEditing = false;
    }

    private void BuildEditDays(IReadOnlyList<int> checkedDays)
    {
        foreach (WorldClockDayToggleViewModel previous in _editDays)
        {
            previous.PropertyChanged -= OnEditDayChanged;
        }

        _editDays = [.. _isoDays.Select(day => new WorldClockDayToggleViewModel(
            day,
            DayLabel(day),
            checkedDays.Contains(day)))];
        foreach (WorldClockDayToggleViewModel toggle in _editDays)
        {
            toggle.PropertyChanged += OnEditDayChanged;
        }

        OnPropertyChanged(nameof(EditDays));
    }

    private void OnEditDayChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        ValidateEdit();

    private void ValidateEdit()
    {
        if (!_isEditing)
        {
            return;
        }

        bool startParsed = TimeOnly.TryParseExact(
            SelectedStart,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out TimeOnly start);
        bool endParsed = TimeOnly.TryParseExact(
            SelectedEnd,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out TimeOnly end);
        int[] days = [.. _editDays.Where(day => day.IsChecked).Select(day => day.IsoDay)];

        if (!startParsed || !endParsed || !WorkWindow.TryCreate(start, end, days, out _, out _))
        {
            EditError = _localization["Tool.WorldClock.Manage.Edit.Invalid"];
            CanSaveEdit = false;
            return;
        }

        EditError = null;
        CanSaveEdit = true;
    }

    private string DayLabel(int isoDay) => WorldClockWeekdayText.Resolve(_localization, isoDay);

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(NoResultsText));
        OnPropertyChanged(nameof(WindowTitle));
        RefreshFromPage();

        if (_isEditing && _editing is not null)
        {
            IReadOnlyList<int> checkedDays = [.. _editDays.Where(day => day.IsChecked).Select(day => day.IsoDay)];
            BuildEditDays(checkedDays);
            ValidateEdit();
        }
    }

    private static string FormatTime(TimeOnly time) =>
        time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static List<string> BuildTimeOptions()
    {
        var options = new List<string>(96);
        for (int minutes = 0; minutes < 24 * 60; minutes += 15)
        {
            options.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes))
                .ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        }

        return options;
    }
}
