using LiveCompanion.Audio.Abstractions;
using LiveCompanion.Audio.Providers;
using NAudio.Wave;

namespace LiveCompanion.Audio;

/// <summary>
/// Manages the ASIO driver lifecycle: detection, initialization, playback,
/// fault handling, and automatic reconnection.
/// </summary>
public sealed class AsioService : IDisposable
{
    private readonly IAsioOutFactory _factory;
    private readonly object _lock = new();
    private IAsioOut? _asio;
    private AsioOutputRouter? _router;
    private AudioConfiguration _config;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public AsioService(IAsioOutFactory factory, AudioConfiguration config)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Fired when the ASIO driver encounters a fault (e.g. device disconnected).
    /// The string parameter contains the error description.
    /// </summary>
    public event Action<string>? AudioFault;

    /// <summary>
    /// Fired when the ASIO driver reconnects after a fault.
    /// </summary>
    public event Action? Reconnected;

    /// <summary>Returns the names of all ASIO drivers installed on the system.</summary>
    public string[] GetAvailableDrivers() => _factory.GetDriverNames();

    /// <summary>Number of output channels on the current ASIO device.</summary>
    public int OutputChannelCount
    {
        get { lock (_lock) return _asio?.NumberOfOutputChannels ?? 0; }
    }

    /// <summary>Whether the ASIO device is currently playing.</summary>
    public bool IsPlaying
    {
        get { lock (_lock) return _asio?.PlaybackState == PlaybackState.Playing; }
    }

    /// <summary>The internal output router. Sources register here.</summary>
    internal AsioOutputRouter? Router
    {
        get { lock (_lock) return _router; }
    }

    /// <summary>
    /// Initializes the ASIO device with the configured driver.
    /// Creates the output router but does not start playback.
    /// </summary>
    public void Initialize()
    {
        if (string.IsNullOrEmpty(_config.AsioDriverName))
            throw new InvalidOperationException("No ASIO driver name configured.");

        lock (_lock)
        {
            DisposeAsio();

            try
            {
                _asio = _factory.Create(_config.AsioDriverName);
                _asio.PlaybackStopped += OnPlaybackStopped;

                // Determine channel count: use the device's available channels,
                // but at least 4 (two stereo pairs: metronome + samples)
                int outputChannels = Math.Max(_asio.NumberOfOutputChannels, 4);

                _router = new AsioOutputRouter(_config.SampleRate, outputChannels);
                _asio.Init(_router);
            }
            catch (Exception ex)
            {
                DisposeAsio();
                throw new InvalidOperationException(
                    $"Failed to initialize ASIO driver '{_config.AsioDriverName}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Updates the configuration. If the driver name changed, a re-initialization is needed.
    /// </summary>
    public void UpdateConfiguration(AudioConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Starts ASIO playback.</summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_asio is null)
                throw new InvalidOperationException("ASIO not initialized. Call Initialize() first.");
            if (_asio.PlaybackState != PlaybackState.Playing)
                _asio.Play();
        }
    }

    /// <summary>Stops ASIO playback.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_asio?.PlaybackState == PlaybackState.Playing)
                _asio.Stop();
        }
    }

    /// <summary>
    /// Registers a stereo source on the specified channel pair.
    /// Must be called after <see cref="Initialize"/> and before <see cref="Play"/>.
    /// </summary>
    internal void RegisterSource(ISampleProvider stereoSource, int channelOffset)
    {
        lock (_lock)
        {
            if (_router is null)
                throw new InvalidOperationException("ASIO not initialized.");
            _router.AddSource(stereoSource, channelOffset);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        lock (_lock)
        {
            DisposeAsio();
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            AudioFault?.Invoke(e.Exception.Message);

            if (_config.AutoReconnect && !_disposed)
            {
                StartReconnectLoop();
            }
        }
    }

    private void StartReconnectLoop()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.ReconnectDelayMs, ct).ConfigureAwait(false);
                    Initialize();
                    Play();
                    Reconnected?.Invoke();
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Retry on next iteration
                }
            }
        }, ct);
    }

    private void DisposeAsio()
    {
        if (_asio is not null)
        {
            _asio.PlaybackStopped -= OnPlaybackStopped;
            try { _asio.Stop(); } catch { /* ignore during cleanup */ }
            _asio.Dispose();
            _asio = null;
        }
        _router = null;
    }
}
