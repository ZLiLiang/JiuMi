using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// The world clock page: the board, the plan grid, the cursor and the manage-places dialog state.
/// </summary>
public sealed class WorldClockViewModel : ViewModelBase, IDisposable
{
    private const int CursorStepMinutes = 15;
    private const int LastCursorMinute = 1425;
    private const int RefreshMinutes = 1;

    private readonly IWorldClockCatalogProvider _catalogProvider;
    private readonly IHolidayProvider _holidayProvider;
    private readonly ISettingsStore _settingsStore;
    private readonly ILocalizationService _localization;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly Serilog.ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<(string CountryCode, int Year), HolidayLookup> _holidayLookups = [];
    private WorldClockCatalog? _catalog;
    private WorldClockTimeline? _timeline;
    private HolidayCalendar _holidays = HolidayCalendar.Empty;
    private IReadOnlyList<Place> _places;
    private IReadOnlyList<WorldClockRowViewModel> _rows = [];
    private IReadOnlyList<WorldClockTickViewModel> _ticks = [];
    private IReadOnlyList<WorldClockBandCellViewModel> _bandCells = [];
    private WorldClockViewMode _viewMode;
    private double _cursorMinutes;
    private double _nowFraction;
    private string _nowLabel = string.Empty;
    private bool _nowLabelFlip;
    private string _summaryText = string.Empty;
    private string _summaryNote = string.Empty;
    private string _zoneNote = string.Empty;
    private string _holidayNote = string.Empty;
    private bool _hasZoneNote;
    private bool _hasHolidayNote;
    private bool _holidaysUnavailable;
    private bool _followNow = true;
    private bool _updatingCursor;
    private bool _isActive;
    private bool _disposed;
    private int _refreshVersion;
    private Task? _refreshTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockViewModel"/> class.
    /// </summary>
    /// <param name="catalogProvider">Loads the bundled country, city and office-hours data.</param>
    /// <param name="holidayProvider">Fetches public holidays for the selected countries.</param>
    /// <param name="settingsStore">Holds the selected places, the view and the network switch.</param>
    /// <param name="localization">Supplies the interface text.</param>
    /// <param name="errorReporter">Reports settings save failures.</param>
    /// <param name="recordFactory">Creates the records reported by this page.</param>
    /// <param name="logger">Receives refresh and holiday diagnostics.</param>
    /// <param name="timeProvider">Supplies the current instant.</param>
    public WorldClockViewModel(
        IWorldClockCatalogProvider catalogProvider,
        IHolidayProvider holidayProvider,
        ISettingsStore settingsStore,
        ILocalizationService localization,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        Serilog.ILogger logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(catalogProvider);
        ArgumentNullException.ThrowIfNull(holidayProvider);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _catalogProvider = catalogProvider;
        _holidayProvider = holidayProvider;
        _settingsStore = settingsStore;
        _localization = localization;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _logger = logger;
        _timeProvider = timeProvider;

        WorldClockSettings settings = settingsStore.Current.WorldClock;
        _places = settings.Places;
        _viewMode = settings.View;
        Manage = new WorldClockManageViewModel(this, localization);
        SetViewCommand = new RelayCommand<WorldClockViewMode>(SetView);
        BackToNowCommand = new RelayCommand(BackToNow);
        RetryHolidaysCommand = new RelayCommand(RetryHolidays);
        ManagePlacesCommand = new RelayCommand(RequestManage);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(RefreshMinutes) };
        _timer.Tick += OnTimerTick;
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Raised when the page asks the shell to show the manage-places dialog.
    /// </summary>
    public event EventHandler? ManageRequested;

    /// <summary>
    /// Gets the manage-places dialog state.
    /// </summary>
    public WorldClockManageViewModel Manage
    {
        get;
    }

    /// <summary>
    /// Gets the rows of the board and the plan.
    /// </summary>
    public IReadOnlyList<WorldClockRowViewModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>
    /// Gets the hour labels of the plan axis.
    /// </summary>
    public IReadOnlyList<WorldClockTickViewModel> Ticks
    {
        get => _ticks;
        private set => SetProperty(ref _ticks, value);
    }

    /// <summary>
    /// Gets the overlap band cells.
    /// </summary>
    public IReadOnlyList<WorldClockBandCellViewModel> BandCells
    {
        get => _bandCells;
        private set => SetProperty(ref _bandCells, value);
    }

    /// <summary>
    /// Gets a value indicating whether the board view is active.
    /// </summary>
    public bool IsBoardView => _viewMode == WorldClockViewMode.Board;

    /// <summary>
    /// Gets a value indicating whether the plan view is active.
    /// </summary>
    public bool IsPlanView => _viewMode == WorldClockViewMode.Plan;

    /// <summary>
    /// Gets a value indicating whether any place is selected.
    /// </summary>
    public bool HasPlaces => _places.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the empty state replaces the cards.
    /// </summary>
    public bool ShowEmptyState => !HasPlaces;

    /// <summary>
    /// Gets the summary line above the board or plan content.
    /// </summary>
    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    /// <summary>
    /// Gets the secondary part of the summary line.
    /// </summary>
    public string SummaryNote
    {
        get => _summaryNote;
        private set
        {
            if (SetProperty(ref _summaryNote, value))
            {
                OnPropertyChanged(nameof(HasSummaryNote));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the summary has a secondary part.
    /// </summary>
    public bool HasSummaryNote => !string.IsNullOrEmpty(_summaryNote);

    /// <summary>
    /// Gets the cursor position in minutes from the reference midnight.
    /// </summary>
    public double CursorMinutes
    {
        get => _cursorMinutes;
        set
        {
            double clamped = Math.Clamp(Math.Round(value / CursorStepMinutes) * CursorStepMinutes, 0, LastCursorMinute);
            if (!SetProperty(ref _cursorMinutes, clamped))
            {
                return;
            }

            if (!_updatingCursor)
            {
                _followNow = false;
            }

            OnPropertyChanged(nameof(CursorColumn));
            OnPropertyChanged(nameof(CursorReadout));
            UpdateRowLabels();
            UpdateSummaries();
        }
    }

    /// <summary>
    /// Gets the cursor's hour column.
    /// </summary>
    public int CursorColumn => Math.Clamp((int)(_cursorMinutes / 60), 0, WorldClockTimeline.ColumnCount - 1);

    /// <summary>
    /// Gets the cursor readout in the reference zone.
    /// </summary>
    public string CursorReadout
    {
        get
        {
            if (_timeline is null)
            {
                return string.Empty;
            }

            DateTime local = _timeline.GetReferenceLocalTime(_timeline.InstantAt((int)_cursorMinutes));
            return _localization.Format(
                "Tool.WorldClock.Cursor.Readout",
                local.ToString("HH:mm", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Gets where the current instant falls in the twenty-four hour grid, from 0 to 1.
    /// </summary>
    public double NowFraction
    {
        get => _nowFraction;
        private set => SetProperty(ref _nowFraction, value);
    }

    /// <summary>
    /// Gets the label of the current-instant line.
    /// </summary>
    public string NowLabel
    {
        get => _nowLabel;
        private set => SetProperty(ref _nowLabel, value);
    }

    /// <summary>
    /// Gets a value indicating whether the current-instant label sits to the left of its line.
    /// </summary>
    public bool NowLabelFlip
    {
        get => _nowLabelFlip;
        private set => SetProperty(ref _nowLabelFlip, value);
    }

    /// <summary>
    /// Gets the text shown when a place has no usable time zone.
    /// </summary>
    public string ZoneNote
    {
        get => _zoneNote;
        private set => SetProperty(ref _zoneNote, value);
    }

    /// <summary>
    /// Gets a value indicating whether the zone note is shown.
    /// </summary>
    public bool HasZoneNote
    {
        get => _hasZoneNote;
        private set => SetProperty(ref _hasZoneNote, value);
    }

    /// <summary>
    /// Gets the text shown when holiday data is unavailable.
    /// </summary>
    public string HolidayNote
    {
        get => _holidayNote;
        private set => SetProperty(ref _holidayNote, value);
    }

    /// <summary>
    /// Gets a value indicating whether the holiday note is shown.
    /// </summary>
    public bool HasHolidayNote
    {
        get => _hasHolidayNote;
        private set => SetProperty(ref _hasHolidayNote, value);
    }

    /// <summary>
    /// Gets a value indicating whether the holiday note offers a retry.
    /// </summary>
    public bool CanRetryHolidays => _settingsStore.Current.OnlineDataEnabled;

    /// <summary>
    /// Gets the command that switches between the board and the plan.
    /// </summary>
    public IRelayCommand<WorldClockViewMode> SetViewCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that moves the cursor to the current instant.
    /// </summary>
    public IRelayCommand BackToNowCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that drops failed holiday lookups and refreshes.
    /// </summary>
    public IRelayCommand RetryHolidaysCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that asks the shell to show the manage-places dialog.
    /// </summary>
    public IRelayCommand ManagePlacesCommand
    {
        get;
    }

    /// <summary>
    /// Gets the selected places in display order.
    /// </summary>
    public IReadOnlyList<Place> Places => _places;

    /// <summary>
    /// Records a failure to present the manage-places dialog.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public void ReportManageFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.Warning(exception, "The manage-places dialog failed");
    }

    /// <summary>
    /// Starts the refresh timer and loads the page for the first time.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        _timer.Start();
        Refresh();
    }

    /// <summary>
    /// Stops the refresh timer when the page leaves the screen.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _timer.Stop();
        _refreshVersion++;
    }

    /// <summary>
    /// Recomputes the page; called on activation, every minute and when the window is activated.
    /// </summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _refreshTask = RefreshAsync();
    }

    /// <summary>
    /// Adds a catalog entry with its default working window.
    /// </summary>
    /// <param name="kind">The granularity of the entry.</param>
    /// <param name="id">The ISO alpha-2 code or IANA id.</param>
    public void AddPlace(PlaceKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_places.Any(place => place.Kind == kind && string.Equals(place.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        WorkWindow hours = _catalog?.GetDefaultHours(kind, id) ?? WorkWindow.Default;
        _places = [.. _places, new Place(kind, id, hours)];
        OnPlacesChanged();
    }

    /// <summary>
    /// Removes a place by its stable key.
    /// </summary>
    /// <param name="key">The key returned by <see cref="Place.Key"/>.</param>
    public void RemovePlace(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Place[] remaining = [.. _places.Where(place => !string.Equals(place.Key, key, StringComparison.Ordinal))];
        if (remaining.Length == _places.Count)
        {
            return;
        }

        _places = remaining;
        OnPlacesChanged();
    }

    /// <summary>
    /// Moves a place to another index.
    /// </summary>
    /// <param name="fromIndex">The current index.</param>
    /// <param name="toIndex">The target index.</param>
    public void MovePlace(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _places.Count || toIndex < 0 || toIndex >= _places.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var places = _places.ToList();
        Place moved = places[fromIndex];
        places.RemoveAt(fromIndex);
        places.Insert(toIndex, moved);
        _places = places;
        OnPlacesChanged();
    }

    /// <summary>
    /// Replaces the working window of a place.
    /// </summary>
    /// <param name="key">The key returned by <see cref="Place.Key"/>.</param>
    /// <param name="hours">The new window.</param>
    public void SetPlaceHours(string key, WorkWindow hours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(hours);
        _places = [.. _places.Select(place =>
            string.Equals(place.Key, key, StringComparison.Ordinal) ? place.WithHours(hours) : place)];
        OnPlacesChanged();
    }

    /// <summary>
    /// Restores the default places.
    /// </summary>
    public void ResetPlaces()
    {
        _places = WorldClockSettings.CreateDefault().Places;
        OnPlacesChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Deactivate();
        _timer.Tick -= OnTimerTick;
        _localization.PropertyChanged -= OnLocalizationChanged;
        Manage.Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    private async Task RefreshAsync()
    {
        int version = ++_refreshVersion;
        try
        {
            WorldClockCatalog? catalog = _catalog;
            if (catalog is null)
            {
                catalog = await _catalogProvider.LoadAsync().ConfigureAwait(true);
                if (version != _refreshVersion)
                {
                    return;
                }

                _catalog = catalog;
                Manage.SetCatalog(catalog);
            }

            // Draw the grid from the last known holidays first; the request below can take seconds.
            BuildTimeline(catalog);
            await EnsureHolidaysAsync(catalog).ConfigureAwait(true);
            if (version != _refreshVersion || !_isActive)
            {
                return;
            }

            BuildTimeline(catalog);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "World clock refresh failed");
        }
    }

    private async Task EnsureHolidaysAsync(WorldClockCatalog catalog)
    {
        bool allowNetwork = _settingsStore.Current.OnlineDataEnabled;
        int referenceYear = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).Year;
        var required = new List<(string CountryCode, int Year)>();
        foreach (Place place in _places)
        {
            string? countryCode = catalog.GetCountryCode(place);
            if (countryCode is null)
            {
                continue;
            }

            foreach (int year in new[] { referenceYear, referenceYear + 1 })
            {
                var key = (countryCode, year);
                if (!required.Contains(key))
                {
                    required.Add(key);
                }
            }
        }

        var pending = new List<Task>(required.Count);
        foreach ((string countryCode, int year) in required)
        {
            if (_holidayLookups.TryGetValue((countryCode, year), out HolidayLookup? existing) &&
                existing.Status != HolidayLookupStatus.Disabled)
            {
                continue;
            }

            if (!allowNetwork && _holidayLookups.ContainsKey((countryCode, year)))
            {
                continue;
            }

            pending.Add(LoadHolidayAsync(countryCode, year, allowNetwork));
        }

        if (pending.Count > 0)
        {
            await Task.WhenAll(pending).ConfigureAwait(true);
        }

        var dates = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        bool unavailable = false;
        foreach ((string countryCode, int year) in required)
        {
            if (!_holidayLookups.TryGetValue((countryCode, year), out HolidayLookup? lookup))
            {
                continue;
            }

            if (lookup.Status != HolidayLookupStatus.Available)
            {
                // NoData is a legitimate "no holidays here"; only a failed request is worth surfacing.
                unavailable |= lookup.Status == HolidayLookupStatus.Failed;
                continue;
            }

            if (!dates.TryGetValue(countryCode, out HashSet<DateOnly>? countryDates))
            {
                countryDates = [];
                dates[countryCode] = countryDates;
            }

            countryDates.UnionWith(lookup.Dates);
        }

        var calendarDates = new Dictionary<string, IReadOnlySet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HashSet<DateOnly>> pair in dates)
        {
            calendarDates[pair.Key] = pair.Value;
        }

        _holidays = new HolidayCalendar(calendarDates);
        _holidaysUnavailable = unavailable;
    }

    private async Task LoadHolidayAsync(string countryCode, int year, bool allowNetwork)
    {
        try
        {
            _holidayLookups[(countryCode, year)] =
                await _holidayProvider.GetHolidaysAsync(countryCode, year, allowNetwork).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Debug(exception, "Holiday lookup for {CountryCode} {Year} failed", countryCode, year);
            _holidayLookups[(countryCode, year)] = new HolidayLookup(
                HolidayLookupStatus.Failed,
                new HashSet<DateOnly>(),
                exception);
        }
    }

    private void BuildTimeline(WorldClockCatalog catalog)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            catalog,
            _places,
            _localization.CurrentLanguage,
            now,
            TimeZoneInfo.Local,
            _holidays);
        _timeline = timeline;

        var rows = new List<WorldClockRowViewModel>(timeline.Rows.Count);
        for (int index = 0; index < timeline.Rows.Count; index++)
        {
            rows.Add(new WorldClockRowViewModel(
                timeline.Rows[index],
                _localization,
                isLast: index == timeline.Rows.Count - 1));
        }

        Rows = rows;
        Ticks = [.. Enumerable.Range(0, WorldClockTimeline.ColumnCount)
            .Select(hour => new WorldClockTickViewModel(
                hour % 2 == 0 ? hour.ToString("00", CultureInfo.InvariantCulture) : string.Empty))];
        int placeCount = timeline.Rows.Count(row => row.IsAvailable);
        BandCells = [.. timeline.Columns.Select(column => new WorldClockBandCellViewModel(
            column.WorkingCount,
            placeCount))];

        NowFraction = Math.Clamp((now - timeline.Start).TotalHours / 24.0, 0, 1);
        NowLabel = _localization.Format(
            "Tool.WorldClock.Now",
            TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).ToString("HH:mm", CultureInfo.InvariantCulture));
        NowLabelFlip = NowFraction > 0.82;

        OnPropertyChanged(nameof(HasPlaces));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CanRetryHolidays));

        if (_followNow)
        {
            SetCursor(NowCursorMinutes(now), followNow: true);
        }
        else
        {
            UpdateRowLabels();
            UpdateSummaries();
        }

        UpdateNotes();
    }

    private void SetCursor(double minutes, bool followNow)
    {
        _updatingCursor = true;
        try
        {
            CursorMinutes = minutes;
        }
        finally
        {
            _updatingCursor = false;
        }

        _followNow = followNow;
        OnPropertyChanged(nameof(CursorReadout));
        OnPropertyChanged(nameof(CursorColumn));
        UpdateRowLabels();
        UpdateSummaries();
    }

    private static double NowCursorMinutes(DateTimeOffset now)
    {
        DateTime local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime;
        double minutes = (local.Hour * 60) + local.Minute + (local.Second / 60.0);
        return Math.Clamp(Math.Round(minutes / CursorStepMinutes) * CursorStepMinutes, 0, LastCursorMinute);
    }

    private void UpdateRowLabels()
    {
        if (_timeline is null)
        {
            return;
        }

        DateTimeOffset instant = _viewMode == WorldClockViewMode.Plan
            ? _timeline.InstantAt((int)_cursorMinutes)
            : _timeProvider.GetUtcNow();
        foreach (WorldClockRowViewModel row in _rows)
        {
            row.Update(instant, _holidays);
        }
    }

    private void UpdateSummaries()
    {
        if (_places.Count == 0)
        {
            SummaryText = string.Empty;
            SummaryNote = string.Empty;
            return;
        }

        if (_viewMode == WorldClockViewMode.Board)
        {
            int working = _rows.Count(row => row.Status == WorldClockStatus.Working);
            SummaryText = _localization.Format("Tool.WorldClock.Board.Summary", working);
            SummaryNote = _localization["Tool.WorldClock.Board.SummaryNote"];
            return;
        }

        OverlapSummary? overlap = _timeline?.Overlap;
        if (overlap is null)
        {
            return;
        }

        if (!overlap.HasOverlap)
        {
            SummaryText = _localization["Tool.WorldClock.Plan.Summary.None"];
            SummaryNote = string.Empty;
            return;
        }

        string ranges = FormatRanges(overlap.Ranges);
        if (overlap.HasFullOverlap)
        {
            SummaryText = _localization.Format("Tool.WorldClock.Plan.Summary.All", ranges);
            SummaryNote = string.Empty;
            return;
        }

        SummaryText = _localization.Format("Tool.WorldClock.Plan.Summary.Max", overlap.MaxCount, ranges);
        SummaryNote = _localization["Tool.WorldClock.Plan.Summary.NoAll"];
    }

    private string FormatRanges(IReadOnlyList<OverlapRange> ranges)
    {
        string separator = _localization["Tool.WorldClock.Plan.RangeSeparator"];
        return string.Join(separator, ranges.Select(range => _localization.Format(
            "Tool.WorldClock.Plan.Range",
            range.StartHour.ToString("00", CultureInfo.InvariantCulture),
            range.EndHour.ToString("00", CultureInfo.InvariantCulture))));
    }

    private void UpdateNotes()
    {
        string separator = _localization["Tool.WorldClock.ListSeparator"];
        string[] unavailable = [.. _rows.Where(row => !row.IsAvailable).Select(row => row.PrimaryName)];
        HasZoneNote = unavailable.Length > 0;
        ZoneNote = HasZoneNote
            ? _localization.Format("Tool.WorldClock.Footnote.ZoneUnavailable", string.Join(separator, unavailable))
            : string.Empty;

        HasHolidayNote = _holidaysUnavailable && _places.Count > 0;
        HolidayNote = HasHolidayNote ? _localization["Tool.WorldClock.Footnote.HolidayUnavailable"] : string.Empty;
        OnPropertyChanged(nameof(CanRetryHolidays));
    }

    private void SetView(WorldClockViewMode mode)
    {
        if (_viewMode == mode)
        {
            return;
        }

        _viewMode = mode;
        OnPropertyChanged(nameof(IsBoardView));
        OnPropertyChanged(nameof(IsPlanView));
        Persist();
        UpdateRowLabels();
        UpdateSummaries();
    }

    private void BackToNow() => SetCursor(NowCursorMinutes(_timeProvider.GetUtcNow()), followNow: true);

    private void RetryHolidays()
    {
        foreach (var key in _holidayLookups
            .Where(pair => pair.Value.Status != HolidayLookupStatus.Available)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _holidayLookups.Remove(key);
        }

        Refresh();
    }

    private void RequestManage() => ManageRequested?.Invoke(this, EventArgs.Empty);

    private void OnPlacesChanged()
    {
        Persist();
        OnPropertyChanged(nameof(HasPlaces));
        OnPropertyChanged(nameof(ShowEmptyState));
        Manage.RefreshFromPage();
        Refresh();
    }

    private void Persist()
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            WorldClock = new WorldClockSettings(_viewMode, _places),
        });

        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Save",
                error));
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        if (_timeline is not null)
        {
            NowLabel = _localization.Format(
                "Tool.WorldClock.Now",
                TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local)
                    .ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        OnPropertyChanged(nameof(CursorReadout));
        OnPropertyChanged(nameof(CanRetryHolidays));
        Refresh();
    }
}
