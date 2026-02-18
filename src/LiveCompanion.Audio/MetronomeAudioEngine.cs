using LiveCompanion.Audio.Providers;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Audio;

/// <summary>
/// High-precision metronome driven by the ASIO audio callback.
/// Drop-in replacement for <see cref="LiveCompanion.Core.Engine.MetronomeEngine"/>:
/// same public API surface, but the tick counter is advanced by the ASIO buffer
/// callback instead of <c>Task.Delay</c>.
///
/// The key difference: the audio callback in <see cref="MetronomeWaveProvider.Read"/>
/// is the clock source. Each time the ASIO driver requests a buffer of samples,
/// the provider calculates how many ticks fit in that buffer and advances the counter
/// with sub-sample accuracy.
/// </summary>
public sealed class MetronomeAudioEngine
{
    private readonly MetronomeWaveProvider _provider;
    private readonly AsioService _asioService;
    private readonly int _channelOffset;

    public MetronomeAudioEngine(AsioService asioService, AudioConfiguration config, int ppqn, int initialBpm)
    {
        _asioService = asioService ?? throw new ArgumentNullException(nameof(asioService));
        _channelOffset = config.MetronomeChannelOffset;

        _provider = new MetronomeWaveProvider(
            config.SampleRate,
            ppqn,
            initialBpm,
            config.MetronomeMasterVolume,
            config.StrongBeatVolume,
            config.WeakBeatVolume);

        // Wire up events from the provider
        _provider.TickAdvanced += tick => TickAdvanced?.Invoke(tick);
        _provider.Beat += (beat, bar) => Beat?.Invoke(beat, bar);

        // Register as a source on the ASIO output router
        _asioService.RegisterSource(_provider, _channelOffset);
    }

    /// <summary>
    /// Constructor for testing — accepts the provider directly, no AsioService needed.
    /// </summary>
    internal MetronomeAudioEngine(MetronomeWaveProvider provider)
    {
        _provider = provider;
        _asioService = null!;
        _channelOffset = 0;

        _provider.TickAdvanced += tick => TickAdvanced?.Invoke(tick);
        _provider.Beat += (beat, bar) => Beat?.Invoke(beat, bar);
    }

    /// <summary>Current tick position.</summary>
    public long CurrentTick => _provider.CurrentTick;

    /// <summary>Whether the metronome is actively advancing ticks.</summary>
    public bool IsRunning => _provider.IsRunning;

    /// <summary>Fired on every tick advance. Parameter: current tick.</summary>
    public event Action<long>? TickAdvanced;

    /// <summary>
    /// Fired on every beat boundary (every PPQN ticks).
    /// Parameters: beat number (0-based within current bar), bar number (0-based).
    /// </summary>
    public event Action<int, int>? Beat;

    /// <summary>
    /// Changes the tempo and time signature. Takes effect on the next audio buffer.
    /// </summary>
    public void ChangeTempo(int bpm, TimeSignature timeSignature)
    {
        _provider.ChangeTempo(bpm, timeSignature);
    }

    /// <summary>
    /// Starts the metronome. The ASIO callback will begin advancing ticks
    /// and generating click audio.
    /// </summary>
    public void Start()
    {
        _provider.Start();
    }

    /// <summary>
    /// Stops the metronome. The ASIO callback continues but outputs silence.
    /// </summary>
    public void Stop()
    {
        _provider.Stop();
    }

    /// <summary>
    /// Resets the tick counter to zero.
    /// </summary>
    public void Reset()
    {
        _provider.Reset();
    }

    /// <summary>
    /// Updates the volume levels.
    /// </summary>
    public void SetVolumes(float masterVolume, float strongBeatVolume, float weakBeatVolume)
    {
        _provider.SetVolumes(masterVolume, strongBeatVolume, weakBeatVolume);
    }

    /// <summary>
    /// Exposes the internal provider for testing and direct buffer reads.
    /// </summary>
    internal MetronomeWaveProvider Provider => _provider;
}
