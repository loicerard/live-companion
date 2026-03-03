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

        // Pré-allouer les buffers de mixage par bus
        _busBuffers = new Dictionary<string, (float[] Left, float[] Right)>();
        foreach (var busName in busChannelMap.Keys)
        {
            _busBuffers[busName] = (new float[bufferSize], new float[bufferSize]);
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
}
