using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

public class VoicePoolTests
{
    private readonly ILogService _log = new DebugLogService();

    private VoicePool CreatePool() => new(_log);

    /// <summary>
    /// Creates a synthetic CachedAudio with a known waveform for testing.
    /// Stereo: left channel = 1.0, right channel = 0.5 for each sample.
    /// </summary>
    private static CachedAudio CreateTestAudio(int sampleCount = 480, int channels = 2, int sampleRate = 48000)
    {
        var samples = new float[sampleCount * channels];
        for (int i = 0; i < sampleCount; i++)
        {
            if (channels == 2)
            {
                samples[i * 2] = 1.0f;     // Left
                samples[i * 2 + 1] = 0.5f; // Right
            }
            else
            {
                samples[i] = 1.0f; // Mono
            }
        }

        return new CachedAudio
        {
            Samples = samples,
            SampleRate = sampleRate,
            Channels = channels,
        };
    }

    // ------------------------------------------------------------------ //
    // Allocation
    // ------------------------------------------------------------------ //

    [Fact]
    public void InitialActiveCount_ShouldBeZero()
    {
        var pool = CreatePool();
        pool.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TryAllocate_ShouldReturnTrue()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        bool result = pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        result.Should().BeTrue();
        pool.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void TryAllocate_MultipleVoices_ShouldTrackActiveCount()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        for (int i = 0; i < 5; i++)
            pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        pool.ActiveCount.Should().Be(5);
    }

    [Fact]
    public void TryAllocate_MaxVoices_ShouldReturnFalse()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        for (int i = 0; i < VoicePool.MaxVoices; i++)
            pool.TryAllocate(audio, "Main", 1.0f, 0, 0).Should().BeTrue();

        // 17th voice should fail
        bool result = pool.TryAllocate(audio, "Main", 1.0f, 0, 0);
        result.Should().BeFalse();
        pool.ActiveCount.Should().Be(VoicePool.MaxVoices);
    }

    [Fact]
    public void TryAllocate_NullAudio_ShouldThrow()
    {
        var pool = CreatePool();

        var act = () => pool.TryAllocate(null!, "Main", 1.0f, 0, 0);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryAllocate_EmptyBusName_ShouldThrow()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        var act = () => pool.TryAllocate(audio, "", 1.0f, 0, 0);

        act.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ //
    // StopAll
    // ------------------------------------------------------------------ //

    [Fact]
    public void StopAll_ShouldReleaseAllVoices()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        for (int i = 0; i < 5; i++)
            pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        pool.StopAll();

        pool.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void StopAll_ThenAllocate_ShouldWork()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio();

        for (int i = 0; i < VoicePool.MaxVoices; i++)
            pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        pool.StopAll();

        // Should be able to allocate again
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0).Should().BeTrue();
        pool.ActiveCount.Should().Be(1);
    }

    // ------------------------------------------------------------------ //
    // Mixing — basic stereo
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_NoActiveVoices_ShouldOutputSilence()
    {
        var pool = CreatePool();
        var left = new float[64];
        var right = new float[64];

        // Pre-fill with non-zero to verify clearing
        Array.Fill(left, 99f);
        Array.Fill(right, 99f);

        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        left.Should().AllBeEquivalentTo(0f);
        right.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void FillBuffers_SingleStereoVoice_ShouldOutputCorrectSamples()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Left channel should be 1.0, Right should be 0.5 (from test audio)
        for (int i = 0; i < 64; i++)
        {
            left[i].Should().BeApproximately(1.0f, 0.001f);
            right[i].Should().BeApproximately(0.5f, 0.001f);
        }
    }

    [Fact]
    public void FillBuffers_MonoVoice_ShouldDuplicateToBothChannels()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100, channels: 1);
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Mono 1.0 → duplicated to both channels
        for (int i = 0; i < 64; i++)
        {
            left[i].Should().BeApproximately(1.0f, 0.001f);
            right[i].Should().BeApproximately(1.0f, 0.001f);
        }
    }

    // ------------------------------------------------------------------ //
    // Mixing — volume
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_WithVolume_ShouldScaleSamples()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);
        pool.TryAllocate(audio, "Main", 0.5f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Left: 1.0 * 0.5 = 0.5, Right: 0.5 * 0.5 = 0.25
        for (int i = 0; i < 64; i++)
        {
            left[i].Should().BeApproximately(0.5f, 0.001f);
            right[i].Should().BeApproximately(0.25f, 0.001f);
        }
    }

    [Fact]
    public void FillBuffers_ZeroVolume_ShouldOutputSilence()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);
        pool.TryAllocate(audio, "Main", 0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        left.Should().AllBeEquivalentTo(0f);
        right.Should().AllBeEquivalentTo(0f);
    }

    // ------------------------------------------------------------------ //
    // Mixing — multiple voices (summing)
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_TwoVoices_ShouldSum()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);

        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Two voices: Left = 1.0 + 1.0 = 2.0, Right = 0.5 + 0.5 = 1.0
        for (int i = 0; i < 64; i++)
        {
            left[i].Should().BeApproximately(2.0f, 0.001f);
            right[i].Should().BeApproximately(1.0f, 0.001f);
        }
    }

    // ------------------------------------------------------------------ //
    // Mixing — bus routing
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_DifferentBuses_ShouldRouteSeparately()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);

        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);
        pool.TryAllocate(audio, "Click", 0.5f, 0, 0);

        var mainLeft = new float[64];
        var mainRight = new float[64];
        var clickLeft = new float[64];
        var clickRight = new float[64];

        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (mainLeft, mainRight),
            ["Click"] = (clickLeft, clickRight),
        };

        pool.FillBuffers(busBuffers, 64);

        // Main bus: vol=1.0 → L=1.0, R=0.5
        for (int i = 0; i < 64; i++)
        {
            mainLeft[i].Should().BeApproximately(1.0f, 0.001f);
            mainRight[i].Should().BeApproximately(0.5f, 0.001f);
        }

        // Click bus: vol=0.5 → L=0.5, R=0.25
        for (int i = 0; i < 64; i++)
        {
            clickLeft[i].Should().BeApproximately(0.5f, 0.001f);
            clickRight[i].Should().BeApproximately(0.25f, 0.001f);
        }
    }

    [Fact]
    public void FillBuffers_UnmappedBus_ShouldBeIgnored()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 100);

        // Voice on "FX" bus, but only "Main" bus buffer is provided
        pool.TryAllocate(audio, "FX", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Main should be silent (voice is on FX bus)
        left.Should().AllBeEquivalentTo(0f);
        right.Should().AllBeEquivalentTo(0f);

        // Voice should still be active (not skipped permanently)
        pool.ActiveCount.Should().Be(1);
    }

    // ------------------------------------------------------------------ //
    // Mixing — fade-in
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_FadeIn_ShouldRampUp()
    {
        var pool = CreatePool();
        // 48000 Hz, 100 samples → fadeIn of 0.001s = 48 samples
        var audio = CreateTestAudio(sampleCount: 200);
        double fadeInSeconds = 48.0 / 48000; // 48 samples
        pool.TryAllocate(audio, "Main", 1.0f, fadeInSeconds, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // First sample should be near 0 (position 0 / 48 = 0)
        left[0].Should().BeApproximately(0f, 0.05f);

        // Mid fade (sample 24 / 48 = 0.5) — left = 1.0 * 0.5 = 0.5
        left[24].Should().BeApproximately(0.5f, 0.05f);

        // After fade (sample 48+) — should be full volume
        left[48].Should().BeApproximately(1.0f, 0.05f);
        left[63].Should().BeApproximately(1.0f, 0.05f);
    }

    // ------------------------------------------------------------------ //
    // Mixing — fade-out
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_FadeOut_ShouldRampDown()
    {
        var pool = CreatePool();
        // 100 samples total, fadeOut = 50 samples → last 50 samples ramp down
        int totalSamples = 100;
        var audio = CreateTestAudio(sampleCount: totalSamples);
        double fadeOutSeconds = 50.0 / 48000; // 50 samples
        pool.TryAllocate(audio, "Main", 1.0f, 0, fadeOutSeconds);

        var left = new float[100];
        var right = new float[100];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 100);

        // Before fade region (sample 0-49) — full volume
        left[0].Should().BeApproximately(1.0f, 0.05f);
        left[49].Should().BeApproximately(1.0f, 0.05f);

        // Mid fade (sample 75 = 25 samples from end, 25/50 = 0.5)
        left[75].Should().BeApproximately(0.5f, 0.05f);

        // Near end — should be near 0
        left[99].Should().BeApproximately(0f, 0.05f);
    }

    // ------------------------------------------------------------------ //
    // Voice auto-release
    // ------------------------------------------------------------------ //

    [Fact]
    public void FillBuffers_VoiceFinished_ShouldAutoRelease()
    {
        var pool = CreatePool();
        // Short audio: only 32 samples
        var audio = CreateTestAudio(sampleCount: 32);
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        pool.FillBuffers(busBuffers, 64);

        // Voice should have finished (32 samples < 64 requested)
        pool.ActiveCount.Should().Be(0);

        // First 32 samples should have audio, rest should be zero
        left[0].Should().BeApproximately(1.0f, 0.001f);
        left[31].Should().BeApproximately(1.0f, 0.001f);
        left[32].Should().Be(0f);
        left[63].Should().Be(0f);
    }

    [Fact]
    public void FillBuffers_MultipleBufferCalls_ShouldAdvancePosition()
    {
        var pool = CreatePool();
        var audio = CreateTestAudio(sampleCount: 128);
        pool.TryAllocate(audio, "Main", 1.0f, 0, 0);

        var left = new float[64];
        var right = new float[64];
        var busBuffers = new Dictionary<string, (float[] Left, float[] Right)>
        {
            ["Main"] = (left, right),
        };

        // First buffer: samples 0-63
        pool.FillBuffers(busBuffers, 64);
        pool.ActiveCount.Should().Be(1);
        left[0].Should().BeApproximately(1.0f, 0.001f);

        // Second buffer: samples 64-127
        pool.FillBuffers(busBuffers, 64);
        pool.ActiveCount.Should().Be(0); // voice should finish at sample 128
    }

    // ------------------------------------------------------------------ //
    // ComputeFadeGain (static helper)
    // ------------------------------------------------------------------ //

    [Fact]
    public void ComputeFadeGain_NoFade_ShouldReturnOne()
    {
        VoicePool.ComputeFadeGain(50, 100, 0, 0).Should().Be(1.0f);
    }

    [Fact]
    public void ComputeFadeGain_FadeInStart_ShouldReturnZero()
    {
        VoicePool.ComputeFadeGain(0, 100, 10, 0).Should().Be(0f);
    }

    [Fact]
    public void ComputeFadeGain_FadeInMid_ShouldReturnHalf()
    {
        VoicePool.ComputeFadeGain(5, 100, 10, 0).Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void ComputeFadeGain_FadeInEnd_ShouldReturnOne()
    {
        VoicePool.ComputeFadeGain(10, 100, 10, 0).Should().Be(1.0f);
    }

    [Fact]
    public void ComputeFadeGain_FadeOutStart_ShouldReturnOne()
    {
        // Total=100, fadeOut=10, fadeOutStart=90
        VoicePool.ComputeFadeGain(89, 100, 0, 10).Should().Be(1.0f);
    }

    [Fact]
    public void ComputeFadeGain_FadeOutMid_ShouldReturnHalf()
    {
        // position=95, fadeOutStart=90, remaining=5 → 5/10=0.5
        VoicePool.ComputeFadeGain(95, 100, 0, 10).Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void ComputeFadeGain_FadeOutEnd_ShouldReturnZero()
    {
        VoicePool.ComputeFadeGain(100, 100, 0, 10).Should().Be(0f);
    }

    [Fact]
    public void ComputeFadeGain_OverlappingFades_ShouldMultiply()
    {
        // Total=20, fadeIn=10, fadeOut=10 → overlap at position 10-10
        // At position 5: fadeIn=5/10=0.5, fadeOut=1.0 → 0.5
        VoicePool.ComputeFadeGain(5, 20, 10, 10).Should().BeApproximately(0.5f, 0.001f);

        // At position 15: fadeIn=1.0, fadeOut=5/10=0.5 → 0.5
        VoicePool.ComputeFadeGain(15, 20, 10, 10).Should().BeApproximately(0.5f, 0.001f);
    }
}
