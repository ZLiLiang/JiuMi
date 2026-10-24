using Avalonia.Threading;
using JiuMi.Application.Errors;

namespace JiuMi.ViewModels;

/// <summary>
/// Presents one blocking failure in the error window, using the embedded zh-CN text.
/// </summary>
public sealed class ErrorWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan _copiedDisplayDuration = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _copiedTimer;
    private string _copyText = ErrorWindowText.CopyDetails;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorWindowViewModel"/> class.
    /// </summary>
    /// <param name="request">The failure and the text the copy action places on the clipboard.</param>
    public ErrorWindowViewModel(ErrorWindowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ErrorWindowMessage message = ErrorWindowText.MessageFor(request.Record.Code);
        Request = request;
        Headline = message.Headline;
        Body = message.Body;
        Title = ErrorWindowText.AppTitle;
        DetailsHeader = ErrorWindowText.DetailsHeader;
        ExitText = ErrorWindowText.Exit;
        ContinueText = ErrorWindowText.Continue;
        _copiedTimer = new DispatcherTimer
        {
            Interval = _copiedDisplayDuration,
        };
        _copiedTimer.Tick += OnCopiedTick;
    }

    /// <summary>
    /// Gets the failure being shown.
    /// </summary>
    public ErrorWindowRequest Request
    {
        get;
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string Title
    {
        get;
    }

    /// <summary>
    /// Gets the headline above the explanation.
    /// </summary>
    public string Headline
    {
        get;
    }

    /// <summary>
    /// Gets the explanation of what happened and what the user can do.
    /// </summary>
    public string Body
    {
        get;
    }

    /// <summary>
    /// Gets the expander header that reveals the details.
    /// </summary>
    public string DetailsHeader
    {
        get;
    }

    /// <summary>
    /// Gets the details shown by the expander and copied by the copy action.
    /// </summary>
    public string DetailsText => Request.DetailsText;

    /// <summary>
    /// Gets a value indicating whether the main window is usable, so continuing is offered.
    /// </summary>
    public bool CanContinue => Request.CanContinue;

    /// <summary>
    /// Gets a value indicating whether the failure blocks further use of the application.
    /// </summary>
    public bool IsBlocking => !CanContinue;

    /// <summary>
    /// Gets the action the default controls perform.
    /// </summary>
    public ErrorWindowAction DefaultAction => Request.DefaultAction;

    /// <summary>
    /// Gets the copy action label, which reports success for two seconds.
    /// </summary>
    public string CopyText
    {
        get => _copyText;
        private set => SetProperty(ref _copyText, value);
    }

    /// <summary>
    /// Gets the exit button label.
    /// </summary>
    public string ExitText
    {
        get;
    }

    /// <summary>
    /// Gets the continue button label.
    /// </summary>
    public string ContinueText
    {
        get;
    }

    /// <summary>
    /// Shows the copied feedback for the next two seconds.
    /// </summary>
    public void NotifyCopied()
    {
        CopyText = ErrorWindowText.Copied;
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _copiedTimer.Stop();
        _copiedTimer.Tick -= OnCopiedTick;
    }

    private void OnCopiedTick(object? sender, EventArgs e)
    {
        _copiedTimer.Stop();
        CopyText = ErrorWindowText.CopyDetails;
    }
}
