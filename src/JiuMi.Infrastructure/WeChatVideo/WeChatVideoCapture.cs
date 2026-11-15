using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using JiuMi.Application.Settings;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Starts the short-lived WeChat MITM capture, switches the system HTTPS proxy and restores it afterwards.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WeChatVideoCapture : IWeChatVideoCapture, IDisposable
{
    private readonly WeChatCaptureStateStore _stateStore;
    private readonly WinInetSystemProxy _systemProxy;
    private readonly WindowsWeChatCertificateStore _certificateStore;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile WeChatVideoCaptureStatus _status = WeChatVideoCaptureStatus.Stopped;
    private WinInetProxySnapshot? _snapshot;
    private TitaniumWeChatProxy? _proxy;
    private WeChatCaptureListener? _listener;
    private X509Certificate2? _certificate;
    private bool _stateWritten;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoCapture"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the config directory for the capture state.</param>
    /// <param name="timeProvider">Supplies the capture start time.</param>
    /// <param name="logger">Records capture lifecycle and failures.</param>
    public WeChatVideoCapture(
        ISettingsStore settingsStore,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
        : this(
            new WeChatCaptureStateStore(settingsStore, logger),
            new WinInetSystemProxy(),
            new WindowsWeChatCertificateStore(timeProvider),
            timeProvider,
            logger)
    {
    }

    internal WeChatVideoCapture(
        WeChatCaptureStateStore stateStore,
        WinInetSystemProxy systemProxy,
        WindowsWeChatCertificateStore certificateStore,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(systemProxy);
        ArgumentNullException.ThrowIfNull(certificateStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _stateStore = stateStore;
        _systemProxy = systemProxy;
        _certificateStore = certificateStore;
        _timeProvider = timeProvider;
        _logger = logger;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <inheritdoc />
    public WeChatVideoCaptureStatus Status => _status;

    /// <inheritdoc />
    public bool IsCertificateInstalled => _certificateStore.IsInstalled();

    /// <inheritdoc />
    public event EventHandler<WeChatVideoCaptureItem>? ItemCaptured;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status != WeChatVideoCaptureStatus.Stopped)
            {
                throw new InvalidOperationException("The WeChat video capture is already active.");
            }

            _status = WeChatVideoCaptureStatus.Starting;
            try
            {
                // Starting the certificate trust (a blocking system dialog) and Titanium must not run
                // on the interface thread, or the shell would stop responding while it waits.
                await Task.Run(StartCore, cancellationToken).ConfigureAwait(false);
                _status = WeChatVideoCaptureStatus.Running;
            }
            catch
            {
                CleanupAfterFailedStart();
                _status = WeChatVideoCaptureStatus.Stopped;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status == WeChatVideoCaptureStatus.Stopped)
            {
                return;
            }

            _status = WeChatVideoCaptureStatus.Stopping;
            Exception? restoreFailure = null;
            if (_snapshot is not null)
            {
                try
                {
                    _systemProxy.Restore(_snapshot);
                    _snapshot = null;
                    if (_stateWritten && !_stateStore.Delete())
                    {
                        _logger.Warning("The WeChat capture state file could not be removed after restore");
                    }

                    _stateWritten = false;
                }
                catch (Exception exception)
                {
                    restoreFailure = exception;
                    _logger.Error(exception, "The system proxy could not be restored; the capture state is kept for the next start");
                }
            }

            await StopResourcesAsync().ConfigureAwait(false);
            _status = WeChatVideoCaptureStatus.Stopped;

            if (restoreFailure is not null)
            {
                throw new InvalidOperationException(
                    "The system proxy could not be restored after the WeChat video capture stopped.",
                    restoreFailure);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WeChatVideoResidualRestoreResult> RestoreResidualAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status != WeChatVideoCaptureStatus.Stopped)
            {
                return WeChatVideoResidualRestoreResult.Nothing;
            }

            WeChatCaptureStateReadResult read = _stateStore.Read();
            if (read.Status == WeChatCaptureStateReadStatus.Missing)
            {
                return WeChatVideoResidualRestoreResult.Nothing;
            }

            if (read.Status == WeChatCaptureStateReadStatus.Invalid || read.State is null)
            {
                _logger.Error("A WeChat capture state file exists but cannot be read; the system proxy may still point at a dead port");
                return new WeChatVideoResidualRestoreResult(
                    WeChatVideoResidualRestoreStatus.Failed,
                    Error: new InvalidOperationException("The WeChat capture state file could not be read."));
            }

            try
            {
                _systemProxy.Restore(read.State.Proxy);
                if (!_stateStore.Delete())
                {
                    _logger.Warning("The restored WeChat capture state file could not be removed");
                }

                _logger.Warning(
                    "Restored the system proxy left by an interrupted WeChat video capture (ProxyServer {ProxyServer})",
                    read.State.Proxy.ProxyServer ?? "none");
                return new WeChatVideoResidualRestoreResult(
                    WeChatVideoResidualRestoreStatus.Restored,
                    read.State.Proxy.ProxyServer);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The system proxy left by an interrupted WeChat video capture could not be restored");
                return new WeChatVideoResidualRestoreResult(
                    WeChatVideoResidualRestoreStatus.Failed,
                    read.State.Proxy.ProxyServer,
                    exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UninstallCertificateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status != WeChatVideoCaptureStatus.Stopped)
            {
                throw new InvalidOperationException("Stop the WeChat video capture before removing its root certificate.");
            }

            await Task.Run(WindowsWeChatCertificateStore.Uninstall, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

        // A start or stop in flight owns the gate and its resources; disposing under it would turn
        // its release into an ObjectDisposedException. The process is leaving anyway.
        if (!_gate.Wait(TimeSpan.FromSeconds(2)))
        {
            return;
        }

        try
        {
            RestoreAndStopSynchronously();
        }
        finally
        {
            _gate.Release();
        }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StartCore()
    {
        WeChatCaptureStateReadResult leftover = _stateStore.Read();
        if (leftover.Status != WeChatCaptureStateReadStatus.Missing)
        {
            throw new InvalidOperationException(
                "A previous WeChat video capture left a system proxy snapshot; it must be restored before a new capture can start.");
        }

        _certificate = _certificateStore.EnsureInstalled();

        int proxyPort = LoopbackPortAllocator.Allocate();
        WinInetProxySnapshot snapshot = _systemProxy.CaptureSnapshot();
        string pacScript = WeChatPacScript.Build(snapshot.ProxyEnable, snapshot.ProxyServer, proxyPort);

        var listener = new WeChatCaptureListener(OnItemCaptured, _logger, pacScript);
        WeChatCaptureListenerStart listenerStart = listener.Start();
        _listener = listener;

        var state = new WeChatCaptureState(
            proxyPort,
            listenerStart.Port,
            _timeProvider.GetUtcNow(),
            snapshot);

        if (!_stateStore.Write(state))
        {
            throw new InvalidOperationException(
                "The WeChat capture state cannot be persisted, so the system proxy could not be restored after a crash.");
        }

        _stateWritten = true;
        _snapshot = snapshot;

        var proxy = new TitaniumWeChatProxy(_certificate, listenerStart.ReceiverUrl, _logger);
        _proxy = proxy;
        proxy.Start(proxyPort);

        _systemProxy.ApplyPac(listenerStart.PacUrl.AbsoluteUri);
    }

    private void CleanupAfterFailedStart()
    {
        bool restored = _snapshot is null;
        if (_snapshot is not null)
        {
            try
            {
                _systemProxy.Restore(_snapshot);
                _snapshot = null;
                restored = true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The system proxy could not be restored after the WeChat video capture failed to start");
            }
        }

        StopProxySynchronously();
        _listener?.Dispose();
        _listener = null;
        _certificate?.Dispose();
        _certificate = null;

        // The state file is the only way to recover the snapshot, so it must survive a failed restore.
        if (restored && _stateWritten && _stateStore.Delete())
        {
            _stateWritten = false;
        }
    }

    private async Task StopResourcesAsync()
    {
        TitaniumWeChatProxy? proxy = _proxy;
        _proxy = null;
        if (proxy is not null)
        {
            await proxy.StopAsync().ConfigureAwait(false);
            proxy.Dispose();
        }

        _listener?.Dispose();
        _listener = null;

        // The root certificate stays trusted between sessions: it is reused while it is valid and
        // only UninstallCertificateAsync removes it. Re-adding it on every start would mean a trust
        // dialog per session, and removing a root prompts as well.
        _certificate?.Dispose();
        _certificate = null;
    }

    private void StopProxySynchronously()
    {
        TitaniumWeChatProxy? proxy = _proxy;
        _proxy = null;
        proxy?.Dispose();
    }

    private void OnItemCaptured(WeChatVideoCaptureItem item)
    {
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "A subscriber fault must not break the loopback listener.")]
        void Raise()
        {
            try
            {
                ItemCaptured?.Invoke(this, item);
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "A WeChat capture subscriber threw");
            }
        }

        Raise();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (!_gate.Wait(TimeSpan.FromSeconds(2)))
        {
            return;
        }

        try
        {
            RestoreAndStopSynchronously();
        }
        finally
        {
            _gate.Release();
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Process exit and disposal must attempt every cleanup step even when one fails.")]
    private void RestoreAndStopSynchronously()
    {
        if (_snapshot is not null)
        {
            try
            {
                _systemProxy.Restore(_snapshot);
                _snapshot = null;
                if (_stateWritten)
                {
                    _ = _stateStore.Delete();
                    _stateWritten = false;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The system proxy could not be restored while shutting down");
            }
        }

        StopProxySynchronously();
        _listener?.Dispose();
        _listener = null;
        _certificate?.Dispose();
        _certificate = null;
    }
}
