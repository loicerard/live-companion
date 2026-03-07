using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Fournisseur audio multi-canal qui alimente le callback ASIO.
/// Mixe les voices du <see cref="VoicePool"/> via les bus logiques,
/// puis interleave les buffers dans les canaux de sortie ASIO correspondants.
/// </summary>
public sealed class AsioOutputProvider : IWaveProvider
{
    private readonly VoicePool _voicePool;
    private readonly Dictionary<string, (int Left, int Right)> _busChannelMap;
    private readonly int _outputChannelCount;

    /// <summary>Buffers de mixage par bus (pré-alloués).</summary>
    private readonly Dictionary<string, (float[] Left, float[] Right)> _busBuffers;

    /// <summary>Buffers par canal de sortie (pré-alloués).</summary>
    private readonly float[][] _channelBuffers;

    /// <summary>Nombre d'échantillons par buffer (basé sur bufferSize).</summary>
    private readonly int _maxSampleCount;

    /// <summary>Volumes master par bus (0.0–1.0), modifiables en temps réel depuis le thread UI.</summary>
    private readonly ConcurrentDictionary<string, float> _busVolumes = new();

    /// <summary>Double-buffer de niveaux peak : [0] et [1] alternent entre écriture et lecture.</summary>
    private readonly Dictionary<string, (float Left, float Right)>[] _busLevelsBuffers = new Dictionary<string, (float Left, float Right)>[2];

    /// <summary>Index du buffer actuellement exposé en lecture (0 ou 1).</summary>
    private volatile int _busLevelsReadIndex;

    /// <summary>
    /// Retourne les niveaux peak actuels par bus (0.0 à 1.0).
    /// Thread-safe : la référence du dictionnaire est remplacée atomiquement.
    /// </summary>
    public IReadOnlyDictionary<string, (float Left, float Right)> BusLevels => _busLevelsBuffers[_busLevelsReadIndex];

    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Crée un provider audio multi-canal pour ASIO.
    /// </summary>
    /// <param name="voicePool">Pool de voices à mixer.</param>
    /// <param name="busChannelMap">Mapping bus → (canal gauche, canal droit).</param>
    /// <param name="outputChannelCount">Nombre total de canaux de sortie ASIO.</param>
    /// <param name="sampleRate">Fréquence d'échantillonnage (Hz).</param>
    /// <param name="bufferSize">Taille du buffer ASIO (échantillons).</param>
    public AsioOutputProvider(
        VoicePool voicePool,
        Dictionary<string, (int Left, int Right)> busChannelMap,
        int outputChannelCount,
        int sampleRate = 48000,
        int bufferSize = 1024)
    {
        _voicePool = voicePool ?? throw new ArgumentNullException(nameof(voicePool));
        _busChannelMap = busChannelMap ?? throw new ArgumentNullException(nameof(busChannelMap));
        _outputChannelCount = outputChannelCount;
        _maxSampleCount = bufferSize;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputChannelCount);

        // Pré-allouer les buffers de mixage par bus et initialiser les volumes à 1.0
        _busBuffers = new Dictionary<string, (float[] Left, float[] Right)>();
        _busLevelsBuffers[0] = new Dictionary<string, (float Left, float Right)>(busChannelMap.Count);
        _busLevelsBuffers[1] = new Dictionary<string, (float Left, float Right)>(busChannelMap.Count);
        foreach (var busName in busChannelMap.Keys)
        {
            _busBuffers[busName] = (new float[bufferSize], new float[bufferSize]);
            _busVolumes[busName] = 1.0f;
            _busLevelsBuffers[0][busName] = (0f, 0f);
            _busLevelsBuffers[1][busName] = (0f, 0f);
        }

        // Pré-allouer les buffers par canal de sortie
        _channelBuffers = new float[outputChannelCount][];
        for (int ch = 0; ch < outputChannelCount; ch++)
            _channelBuffers[ch] = new float[bufferSize];
    }

    /// <summary>
    /// Lit des échantillons audio interleaved en float IEEE 32 bits.
    /// Appelé par le callback ASIO via NAudio.
    /// </summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesPerSample = sizeof(float); // 32-bit float
        int totalSamples = count / bytesPerSample;
        int sampleCount = totalSamples / _outputChannelCount;

        if (sampleCount > _maxSampleCount)
            sampleCount = _maxSampleCount;

        // 1. Mixer les voices dans les bus buffers
        _voicePool.FillBuffers(_busBuffers, sampleCount);

        // 1a. Appliquer les volumes master par bus (post-fader)
        foreach (var (busName, (left, right)) in _busBuffers)
        {
            float vol = _busVolumes.GetValueOrDefault(busName, 1.0f);
            if (vol >= 1.0f) continue;
            for (int i = 0; i < sampleCount; i++)
            {
                left[i] *= vol;
                right[i] *= vol;
            }
        }

        // 1b. Mesurer les niveaux peak par bus (double-buffer, zéro allocation)
        int writeIndex = 1 - _busLevelsReadIndex;
        var writeBuffer = _busLevelsBuffers[writeIndex];
        foreach (var (busName, (left, right)) in _busBuffers)
        {
            float peakL = 0f, peakR = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float absL = MathF.Abs(left[i]);
                float absR = MathF.Abs(right[i]);
                if (absL > peakL) peakL = absL;
                if (absR > peakR) peakR = absR;
            }
            // Clamp à 1.0
            writeBuffer[busName] = (MathF.Min(peakL, 1f), MathF.Min(peakR, 1f));
        }
        // Swap atomique : le thread UI lira désormais le buffer fraîchement écrit
        _busLevelsReadIndex = writeIndex;

        // 2. Effacer les channel buffers
        for (int ch = 0; ch < _outputChannelCount; ch++)
            Array.Clear(_channelBuffers[ch], 0, sampleCount);

        // 3. Router les bus vers les canaux de sortie (additif pour les bus partagés)
        foreach (var (busName, (leftCh, rightCh)) in _busChannelMap)
        {
            if (!_busBuffers.TryGetValue(busName, out var busBuf))
                continue;

            if (leftCh >= 0 && leftCh < _outputChannelCount)
            {
                for (int i = 0; i < sampleCount; i++)
                    _channelBuffers[leftCh][i] += busBuf.Left[i];
            }

            if (rightCh >= 0 && rightCh < _outputChannelCount)
            {
                for (int i = 0; i < sampleCount; i++)
                    _channelBuffers[rightCh][i] += busBuf.Right[i];
            }
        }

        // 4. Interleaver dans le buffer de sortie
        var floatSpan = MemoryMarshal.Cast<byte, float>(
            buffer.AsSpan(offset, sampleCount * _outputChannelCount * bytesPerSample));

        for (int i = 0; i < sampleCount; i++)
        {
            for (int ch = 0; ch < _outputChannelCount; ch++)
            {
                floatSpan[i * _outputChannelCount + ch] = _channelBuffers[ch][i];
            }
        }

        return sampleCount * _outputChannelCount * bytesPerSample;
    }

    // ------------------------------------------------------------------ //
    // Bus volume control
    // ------------------------------------------------------------------ //

    public IReadOnlyList<string> GetBusNames() => _busVolumes.Keys.ToList();

    public float GetBusVolume(string busName) => _busVolumes.GetValueOrDefault(busName, 1.0f);

    public void SetBusVolume(string busName, float volume)
        => _busVolumes[busName] = Math.Clamp(volume, 0f, 1f);
}
