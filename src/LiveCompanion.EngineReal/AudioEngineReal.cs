using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Real audio engine backed by ASIO through <see cref="IAsioInterop"/>.
/// Manages a <see cref="VoicePool"/> for multi-voice PCM playback with volume and fade ramping.
/// Audio clips are decoded and cached by <see cref="AudioCache"/>, then played through the voice pool.
/// </summary>
public sealed class AudioEngineReal : IAudioEngine
{
    private readonly ILogService _log;
    private readonly IAsioInterop _asio;
    private readonly AudioCache _cache;
    private readonly VoicePool _voicePool;

    private volatile bool _initialized;
    private AudioConfig? _config;

    /// <summary>
    /// Bus mappings resolved during initialization.
    /// Key = logical bus name, Value = (left channel index, right channel index).
    /// </summary>
    private readonly Dictionary<string, (int Left, int Right)> _busChannelMap = new();

    /// <summary>Buffer sizes computed from the ASIO driver capabilities.</summary>
    private IReadOnlyList<int> _supportedBufferSizes = [];

    /// <summary>Number of currently active (playing) voices.</summary>
    public int ActiveVoices => _voicePool.ActiveCount;

    /// <summary>
    /// The voice pool used for multi-voice PCM playback.
    /// Exposed for DI wiring (e.g. <c>hasActiveVoices</c> delegate).
    /// </summary>
    public VoicePool VoicePool => _voicePool;

    public AudioEngineReal(ILogService log, IAsioInterop asio, AudioCache cache)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _asio = asio ?? throw new ArgumentNullException(nameof(asio));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _voicePool = new VoicePool(log);
    }

    // ------------------------------------------------------------------ //
    // IAudioEngine — detection & configuration
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableDrivers()
    {
        try
        {
            var drivers = _asio.GetDriverNames();
            _log.Debug(LogSource.EngineReal, $"[AudioEngine] Found {drivers.Count} ASIO driver(s).");
            return drivers;
        }
        catch (Exception ex)
        {
            _log.Warn(LogSource.EngineReal, $"[AudioEngine] Cannot enumerate ASIO drivers: {ex.Message}");
            return [];
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> GetSupportedBufferSizes()
    {
        if (_supportedBufferSizes.Count > 0)
            return _supportedBufferSizes;

        if (!_asio.IsDriverOpen)
            return [];

        try
        {
            var info = _asio.GetBufferInfo();
            _supportedBufferSizes = ComputeBufferSizes(info);
            return _supportedBufferSizes;
        }
        catch (Exception ex)
        {
            _log.Warn(LogSource.EngineReal, $"[AudioEngine] Cannot query buffer sizes: {ex.Message}");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(AudioConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.DriverName))
            throw new ArgumentException("DriverName must not be empty.", nameof(config));

        // Shutdown previous session if any
        if (_initialized)
            await ShutdownAsync().ConfigureAwait(false);

        _log.Info(LogSource.EngineReal, $"[AudioEngine] Initializing — driver='{config.DriverName}', buffer={config.BufferSize}");

        // Open ASIO driver
        _asio.OpenDriver(config.DriverName);

        // Refresh supported buffer sizes from the newly opened driver
        _supportedBufferSizes = [];
        var bufferSizes = GetSupportedBufferSizes();

        if (bufferSizes.Count > 0 && !bufferSizes.Contains(config.BufferSize))
        {
            _log.Warn(LogSource.EngineReal,
                $"[AudioEngine] Requested buffer size {config.BufferSize} is not supported. " +
                $"Available: {string.Join(", ", bufferSizes)}");
        }

        // Resolve bus mappings → ASIO channel pairs
        ResolveBusMappings(config);

        _config = config;
        _initialized = true;

        // Start ASIO playback callback — audio flows continuously,
        // silence is output when no voices are active.
        StartAsioPlayback(config.BufferSize);

        _log.Info(LogSource.EngineReal,
            $"[AudioEngine] Initialized — outputs={_asio.OutputChannelCount}, " +
            $"buses={_busChannelMap.Count}, bufferSizes=[{string.Join(',', bufferSizes)}]");
    }

    // ------------------------------------------------------------------ //
    // IAudioEngine — playback
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task PlayClipAsync(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ThrowIfNotInitialized();

        var cached = _cache.Get(clip.FilePath);
        if (cached is null)
        {
            _log.Warn(LogSource.EngineReal,
                $"[AudioEngine] Clip '{clip.Name}' not in cache (path='{clip.FilePath}') — skipped.");
            return Task.CompletedTask;
        }

        bool allocated = _voicePool.TryAllocate(
            cached,
            clip.BusName,
            (float)clip.Volume,
            clip.FadeInSeconds,
            clip.FadeOutSeconds);

        if (allocated)
        {
            _log.Debug(LogSource.EngineReal,
                $"[AudioEngine] Playing '{clip.Name}' on bus '{clip.BusName}' " +
                $"vol={clip.Volume:F2} — active voices={_voicePool.ActiveCount}");
        }
        else
        {
            _log.Warn(LogSource.EngineReal,
                $"[AudioEngine] Voice limit reached — clip '{clip.Name}' dropped.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAllAsync()
    {
        ThrowIfNotInitialized();
        _voicePool.StopAll();
        _log.Debug(LogSource.EngineReal, "[AudioEngine] StopAll — all voices stopped.");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // IAudioEngine — lifecycle
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task ShutdownAsync()
    {
        _voicePool.StopAll();
        _asio.StopPlayback();
        _asio.CloseDriver();
        _cache.Clear();
        _busChannelMap.Clear();
        _supportedBufferSizes = [];
        _config = null;
        _initialized = false;

        _log.Info(LogSource.EngineReal, "[AudioEngine] Shutdown complete.");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Output pair queries
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableOutputPairs()
    {
        if (!_asio.IsDriverOpen)
            return [];

        var pairs = new List<string>();
        int count = _asio.OutputChannelCount;

        for (int i = 0; i + 1 < count; i += 2)
        {
            var left = _asio.GetOutputChannelName(i);
            var right = _asio.GetOutputChannelName(i + 1);
            pairs.Add($"{left}-{right}");
        }

        // Si nombre impair, ajouter le dernier canal seul
        if (count % 2 != 0)
        {
            var last = _asio.GetOutputChannelName(count - 1);
            pairs.Add(last);
        }

        return pairs.AsReadOnly();
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Initialise et démarre le callback de playback ASIO.
    /// </summary>
    private void StartAsioPlayback(int bufferSize)
    {
        try
        {
            var provider = new AsioOutputProvider(
                _voicePool,
                _busChannelMap,
                _asio.OutputChannelCount,
                sampleRate: 48000,
                bufferSize: bufferSize);

            _asio.InitPlayback(provider);
            _asio.Play();

            _log.Info(LogSource.EngineReal,
                $"[AudioEngine] ASIO playback started — {_asio.OutputChannelCount} channels, buffer={bufferSize}");
        }
        catch (Exception ex)
        {
            _log.Error(LogSource.EngineReal,
                $"[AudioEngine] Failed to start ASIO playback: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes the list of valid buffer sizes from the ASIO driver's capabilities.
    /// </summary>
    public static IReadOnlyList<int> ComputeBufferSizes(AsioBufferInfo info)
    {
        if (info.MinSize <= 0 || info.MaxSize <= 0 || info.MinSize > info.MaxSize)
            return [info.PreferredSize > 0 ? info.PreferredSize : 256];

        var sizes = new List<int>();

        if (info.Granularity == -1)
        {
            // Power-of-two increments
            for (int s = info.MinSize; s <= info.MaxSize; s *= 2)
                sizes.Add(s);
        }
        else if (info.Granularity > 0)
        {
            // Linear increments
            for (int s = info.MinSize; s <= info.MaxSize; s += info.Granularity)
                sizes.Add(s);
        }
        else
        {
            // Granularity == 0 → only the preferred size is valid
            sizes.Add(info.PreferredSize > 0 ? info.PreferredSize : info.MinSize);
        }

        return sizes;
    }

    /// <summary>
    /// Resolves logical bus names from <see cref="AudioConfig.BusMappings"/> to ASIO output channel pairs.
    /// Falls back to sequential pairs (0-1, 2-3, …) when output names don't match.
    /// </summary>
    private void ResolveBusMappings(AudioConfig config)
    {
        _busChannelMap.Clear();

        int outputCount = _asio.OutputChannelCount;

        // Build a lookup: channel name → channel index
        var channelLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < outputCount; i++)
        {
            var name = _asio.GetOutputChannelName(i);
            channelLookup[name] = i;
        }

        int nextPair = 0;
        foreach (var (busName, outputName) in config.BusMappings)
        {
            if (TryResolveChannelPair(outputName, channelLookup, out int left, out int right))
            {
                _busChannelMap[busName] = (left, right);
            }
            else
            {
                // Fallback: assign sequential stereo pairs
                left = nextPair * 2;
                right = left + 1;
                if (right < outputCount)
                {
                    _busChannelMap[busName] = (left, right);
                    nextPair++;
                }
                else
                {
                    _log.Warn(LogSource.EngineReal,
                        $"[AudioEngine] Not enough outputs for bus '{busName}' — skipped.");
                }
            }

            if (_busChannelMap.ContainsKey(busName))
            {
                var (l, r) = _busChannelMap[busName];
                _log.Debug(LogSource.EngineReal,
                    $"[AudioEngine] Bus '{busName}' → channels ({l}, {r})");
            }
        }
    }

    /// <summary>
    /// Tries to match an output name like "Output 1-2" to a pair of channel indices.
    /// Supports patterns: "Output 1-2", "Ch 3-4", or matching the first channel by exact name.
    /// </summary>
    private static bool TryResolveChannelPair(
        string outputName,
        Dictionary<string, int> channelLookup,
        out int left,
        out int right)
    {
        left = right = -1;

        // Try to parse "Output X-Y" pattern (1-based in UI, 0-based internally)
        var parts = outputName.Split('-');
        if (parts.Length == 2)
        {
            var leftPart = parts[0].Trim();
            var rightPart = parts[1].Trim();

            // Extract trailing number from the left part (e.g. "Output 1" → 1)
            if (TryExtractTrailingNumber(leftPart, out int leftNum)
                && int.TryParse(rightPart, out int rightNum))
            {
                left = leftNum - 1;   // convert to 0-based
                right = rightNum - 1;
                return left >= 0 && right >= 0;
            }
        }

        // Fallback: try to find the channel by exact name
        if (channelLookup.TryGetValue(outputName, out int idx))
        {
            left = idx;
            right = idx + 1;
            return true;
        }

        return false;
    }

    private static bool TryExtractTrailingNumber(string text, out int number)
    {
        number = 0;
        int i = text.Length - 1;
        while (i >= 0 && char.IsDigit(text[i]))
            i--;

        return i < text.Length - 1 && int.TryParse(text.AsSpan(i + 1), out number);
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "AudioEngineReal is not initialized. Call InitializeAsync first.");
    }
}
