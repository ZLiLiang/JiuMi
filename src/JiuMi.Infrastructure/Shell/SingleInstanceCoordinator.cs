using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using JiuMi.Application.Shell;

namespace JiuMi.Infrastructure.Shell;

/// <summary>
/// Guards one process per executable path and user session with a named mutex, and listens on a named
/// pipe for the activation request a later launch writes.
/// </summary>
/// <remarks>
/// The names are derived from the executable path, so two copies in different directories do not block
/// each other. Both objects use the default <c>Local\</c> namespace, which scopes them to the current
/// user session. Nothing needs cleaning up after a crash: the kernel releases the mutex with the
/// process, and the pipe disappears with the listener thread.
/// </remarks>
public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator
{
    private const string ActivationMessage = "SHOW";

    private static readonly TimeSpan _connectTimeout = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan _wakeTimeout = TimeSpan.FromMilliseconds(200);

    private readonly string _name;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();

    private Mutex? _mutex;
    private NamedPipeServerStream? _server;
    private Thread? _listener;
    private volatile bool _stopping;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceCoordinator"/> class for this process.
    /// </summary>
    /// <param name="logger">Receives the failures of the activation channel.</param>
    public SingleInstanceCoordinator(Serilog.ILogger logger)
        : this(ResolveExecutablePath(), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceCoordinator"/> class.
    /// </summary>
    /// <param name="executablePath">The executable whose copies exclude each other.</param>
    /// <param name="logger">Receives the failures of the activation channel.</param>
    public SingleInstanceCoordinator(string executablePath, Serilog.ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(logger);

        _name = CreateName(executablePath);
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Derives the mutex and pipe name from an executable path.
    /// </summary>
    /// <param name="executablePath">The full path of the executable.</param>
    /// <returns>One name, shared by the mutex and the pipe.</returns>
    public static string CreateName(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(executablePath)));
        return "JiuMi." + Convert.ToHexString(hash, 0, 16);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The coordinator owns the pipe from this point and disposes it in Dispose.")]
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mutex is not null)
        {
            return true;
        }

        var mutex = new Mutex(initiallyOwned: false, _name, out bool createdNew);
        if (!createdNew)
        {
            _logger.Debug("Another instance owns the lock; requesting activation");
            SendActivationRequest();
            mutex.Dispose();
            return false;
        }

        NamedPipeServerStream server;
        try
        {
            // Created on this thread before the listener starts: a launch that follows TryAcquire always
            // finds a pipe to connect to instead of racing the listener's first connection.
            server = CreateServer();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Debug(exception, "The activation pipe could not be created");
            mutex.Dispose();
            return false;
        }

        _mutex = mutex;
        _server = server;
        _listener = new Thread(Listen)
        {
            IsBackground = true,
            Name = "JiuMi activation listener",
        };
        _listener.Start();
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;

        // A blocking WaitForConnection is only reliably unblocked by a client connection, not by
        // disposing the stream from this thread. The wake connection carries no request and the
        // listener leaves without reading it.
        WakeListener();

        NamedPipeServerStream? server;
        lock (_sync)
        {
            server = _server;
            _server = null;
        }

        server?.Dispose();

        // The next launch can only bind the pipe once the listener has let go of the instance.
        _listener?.Join(TimeSpan.FromSeconds(2));
        _listener = null;
        _mutex?.Dispose();
        _mutex = null;
    }

    private static string ResolveExecutablePath() =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "JiuMi.Desktop.exe");

    private NamedPipeServerStream CreateServer() =>
        new(_name, PipeDirection.In, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.None);

    private void WakeListener()
    {
        if (_listener is not { IsAlive: true })
        {
            return;
        }

        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out);
            client.Connect((int)_wakeTimeout.TotalMilliseconds);
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The listener is between connections; the stopping flag is enough.
            _logger.Debug(exception, "The activation listener could not be woken");
        }
    }

    private void SendActivationRequest()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out);
            client.Connect((int)_connectTimeout.TotalMilliseconds);

            using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine(ActivationMessage);
            writer.Flush();
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The other process may already be exiting. It still owns the lock, so this launch leaves.
            _logger.Debug(exception, "The running instance did not accept the activation request");
        }
    }

    private void Listen()
    {
        while (!_stopping)
        {
            NamedPipeServerStream? server;
            lock (_sync)
            {
                server = _server;
            }

            if (server is null)
            {
                break;
            }

            try
            {
                server.WaitForConnection();
                if (_stopping)
                {
                    // The wake connection of Dispose: it carries no request and ends the loop.
                    break;
                }

                ReadRequest(server);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                if (_stopping)
                {
                    break;
                }

                _logger.Debug(exception, "An activation connection ended before it was read");
            }
            finally
            {
                server.Dispose();
            }

            if (_stopping)
            {
                break;
            }

            try
            {
                lock (_sync)
                {
                    if (_stopping)
                    {
                        break;
                    }

                    _server = CreateServer();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.Debug(exception, "The activation pipe could not be recreated");
                break;
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The listener is a thread boundary: a failing activation handler must not end the single-instance lock.")]
    private void ReadRequest(NamedPipeServerStream server)
    {
        using var reader = new StreamReader(
            server,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 128,
            leaveOpen: true);
        string? message = reader.ReadLine();

        if (!string.Equals(message, ActivationMessage, StringComparison.Ordinal))
        {
            _logger.Debug(
                "Ignored an activation message that is not {Expected}: {Message}",
                ActivationMessage,
                message ?? "<empty>");
            return;
        }

        try
        {
            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _logger.Debug(exception, "An activation handler failed");
        }
    }
}
