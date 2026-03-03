using System.Reflection;
using NAudio.Wave;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Production implementation of <see cref="IAsioInterop"/> backed by NAudio's <see cref="AsioOut"/>.
/// This class is only functional on Windows where ASIO drivers are available.
/// </summary>
public sealed class AsioInterop : IAsioInterop
{
    /// <summary>Standard ASIO buffer sizes used when the driver doesn't expose its capabilities.</summary>
    private static readonly IReadOnlyList<int> StandardBufferSizes = [64, 128, 256, 512, 1024, 2048, 4096];

    private AsioOut? _asioOut;
    private bool _isPlaying;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetDriverNames()
    {
        try
        {
            return AsioOut.GetDriverNames();
        }
        catch (Exception)
        {
            // Not on Windows or no ASIO drivers installed.
            return [];
        }
    }

    /// <inheritdoc/>
    public void OpenDriver(string driverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverName);
        CloseDriver();
        _asioOut = new AsioOut(driverName);
    }

    /// <inheritdoc/>
    public void CloseDriver()
    {
        if (_asioOut is not null)
        {
            StopPlayback();
            _asioOut.Dispose();
            _asioOut = null;
        }
    }

    /// <inheritdoc/>
    public bool IsDriverOpen => _asioOut is not null;

    /// <inheritdoc/>
    public AsioBufferInfo GetBufferInfo()
    {
        ThrowIfNoDriver();

        // NAudio 2.x does not publicly expose the low-level ASIODriverExt.
        // We use reflection to access the internal driver's Capabilities.
        // If reflection fails, we return sensible defaults.
        try
        {
            var driverField = typeof(AsioOut).GetField("driver", BindingFlags.NonPublic | BindingFlags.Instance);
            if (driverField?.GetValue(_asioOut) is { } driverExt)
            {
                var capsProp = driverExt.GetType().GetProperty("Capabilities");
                if (capsProp?.GetValue(driverExt) is { } caps)
                {
                    int min = (int)(caps.GetType().GetField("BufferMinSize")?.GetValue(caps) ?? 64);
                    int max = (int)(caps.GetType().GetField("BufferMaxSize")?.GetValue(caps) ?? 4096);
                    int pref = (int)(caps.GetType().GetField("BufferPreferredSize")?.GetValue(caps) ?? 256);
                    int gran = (int)(caps.GetType().GetField("BufferGranularity")?.GetValue(caps) ?? -1);
                    return new AsioBufferInfo(min, max, pref, gran);
                }
            }
        }
        catch
        {
            // Reflection failed — fall through to defaults.
        }

        // Fallback: return standard power-of-two range.
        return new AsioBufferInfo(64, 4096, 256, -1);
    }

    /// <inheritdoc/>
    public int OutputChannelCount
    {
        get
        {
            ThrowIfNoDriver();
            return _asioOut!.DriverOutputChannelCount;
        }
    }

    /// <inheritdoc/>
    public string GetOutputChannelName(int index)
    {
        ThrowIfNoDriver();
        return _asioOut!.AsioOutputChannelName(index);
    }

    // ------------------------------------------------------------------ //
    // Playback
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public void InitPlayback(IWaveProvider provider)
    {
        ThrowIfNoDriver();
        ArgumentNullException.ThrowIfNull(provider);
        _asioOut!.Init(provider);
    }

    /// <inheritdoc/>
    public void Play()
    {
        ThrowIfNoDriver();
        _asioOut!.Play();
        _isPlaying = true;
    }

    /// <inheritdoc/>
    public void StopPlayback()
    {
        if (_isPlaying && _asioOut is not null)
        {
            _asioOut.Stop();
            _isPlaying = false;
        }
    }

    /// <inheritdoc/>
    public bool IsPlaying => _isPlaying;

    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public void Dispose() => CloseDriver();

    private void ThrowIfNoDriver()
    {
        if (_asioOut is null)
            throw new InvalidOperationException("No ASIO driver is open. Call OpenDriver first.");
    }
}
