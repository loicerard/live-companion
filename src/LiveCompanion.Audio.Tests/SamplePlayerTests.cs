using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Audio.Tests;

public class SamplePlayerTests
{
    private const int SampleRate = 44100;

    /// <summary>Creates a SamplePlayer using the test constructor (no ASIO).</summary>
    private static SamplePlayer CreatePlayer() => new(SampleRate);

    /// <summary>Creates a short stereo PCM sine tone for testing.</summary>
    private static float[] CreateTestSample(float frequency = 440f, float durationMs = 100f)
    {
        int frameCount = (int)(SampleRate * durationMs / 1000f);
        var data = new float[frameCount * 2]; // stereo
        for (int f = 0; f < frameCount; f++)
        {
            float sample = MathF.Sin(2f * MathF.PI * frequency * f / SampleRate) * 0.5f;
            data[f * 2] = sample;     // L
            data[f * 2 + 1] = sample; // R
        }
        return data;
    }

    [Fact]
    public void LoadSample_stores_sample_in_cache()
    {
        using var player = CreatePlayer();
        var pcm = CreateTestSample();

        player.LoadSample("test.wav", pcm);

        Assert.Equal(1, player.LoadedSampleCount);
    }

    [Fact]
    public void OnAudioCueFired_triggers_playback_of_loaded_sample()
    {
        using var player = CreatePlayer();
        var pcm = CreateTestSample(durationMs: 50);
        player.LoadSample("boom.wav", pcm);

        var cue = new AudioCueEvent
        {
            Tick = 0,
            SampleFileName = "boom.wav",
            GainDb = 0.0 // unity gain
        };

        player.OnAudioCueFired(cue);

        // Read from the mixer — should contain non-zero audio
        var buffer = new float[1024];
        int read = player.Mixer.Read(buffer, 0, buffer.Length);

        bool hasAudio = false;
        for (int i = 0; i < read; i++)
        {
            if (Math.Abs(buffer[i]) > 0.001f)
            {
                hasAudio = true;
                break;
            }
        }

        Assert.True(hasAudio, "Expected audio output after triggering a loaded sample.");
    }

    [Fact]
    public void OnAudioCueFired_ignores_unknown_sample()
    {
        using var player = CreatePlayer();
        // Don't load any sample

        var cue = new AudioCueEvent
        {
            Tick = 0,
            SampleFileName = "nonexistent.wav",
            GainDb = 0.0
        };

        // Should not throw
        player.OnAudioCueFired(cue);

        // Mixer should output silence
        var buffer = new float[512];
        player.Mixer.Read(buffer, 0, buffer.Length);

        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.Equal(0f, buffer[i]);
        }
    }

    [Fact]
    public void GainDb_is_applied_correctly()
    {
        using var player = CreatePlayer();

        // Create a sample that is a constant 1.0 on both channels
        int frames = 100;
        var pcm = new float[frames * 2];
        for (int i = 0; i < pcm.Length; i++)
            pcm[i] = 1.0f;

        player.LoadSample("constant.wav", pcm);

        // Apply -6 dB (should roughly halve the amplitude)
        var cue = new AudioCueEvent
        {
            Tick = 0,
            SampleFileName = "constant.wav",
            GainDb = -6.0
        };

        player.OnAudioCueFired(cue);

        var buffer = new float[frames * 2];
        player.Mixer.Read(buffer, 0, buffer.Length);

        // -6 dB ≈ 0.501 linear gain
        float expectedGain = (float)Math.Pow(10.0, -6.0 / 20.0);
        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.InRange(buffer[i], expectedGain - 0.01f, expectedGain + 0.01f);
        }
    }

    [Fact]
    public void Multiple_simultaneous_samples_are_mixed()
    {
        using var player = CreatePlayer();

        // Create two different constant-level samples
        int frames = 100;
        var pcm1 = new float[frames * 2];
        var pcm2 = new float[frames * 2];
        for (int i = 0; i < pcm1.Length; i++)
        {
            pcm1[i] = 0.3f;
            pcm2[i] = 0.4f;
        }

        player.LoadSample("sample1.wav", pcm1);
        player.LoadSample("sample2.wav", pcm2);

        player.OnAudioCueFired(new AudioCueEvent { SampleFileName = "sample1.wav", GainDb = 0 });
        player.OnAudioCueFired(new AudioCueEvent { SampleFileName = "sample2.wav", GainDb = 0 });

        var buffer = new float[frames * 2];
        player.Mixer.Read(buffer, 0, buffer.Length);

        // Mixed output should be approximately 0.3 + 0.4 = 0.7
        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.InRange(buffer[i], 0.65f, 0.75f);
        }
    }

    [Fact]
    public void SubscribeTo_SetlistPlayer_receives_AudioCueFired()
    {
        using var player = CreatePlayer();
        var pcm = CreateTestSample(durationMs: 50);
        player.LoadSample("rain-loop.wav", pcm);

        var metronome = new LiveCompanion.Core.Engine.MetronomeEngine(Setlist.DefaultPpqn, 120);
        var setlistPlayer = new SetlistPlayer(metronome);

        player.SubscribeTo(setlistPlayer);

        // Create a setlist with an audio cue
        var setlist = new Setlist
        {
            Name = "Test",
            Ppqn = Setlist.DefaultPpqn,
            Songs =
            [
                new Song
                {
                    Title = "Test Song",
                    Artist = "Test",
                    DurationTicks = Setlist.DefaultPpqn * 4, // 4 beats
                    Events =
                    [
                        new SectionChangeEvent
                        {
                            Tick = 0,
                            SectionName = "Intro",
                            Bpm = 120,
                            TimeSignature = TimeSignature.Common,
                            Presets = []
                        },
                        new AudioCueEvent
                        {
                            Tick = 1,
                            SampleFileName = "rain-loop.wav",
                            GainDb = 0.0
                        }
                    ]
                }
            ]
        };

        setlistPlayer.Load(setlist);
        setlistPlayer.PlaySynchronous();

        // Read from the mixer — there should be audio from the triggered sample
        var buffer = new float[1024];
        int read = player.Mixer.Read(buffer, 0, buffer.Length);

        bool hasAudio = false;
        for (int i = 0; i < read; i++)
        {
            if (Math.Abs(buffer[i]) > 0.001f)
            {
                hasAudio = true;
                break;
            }
        }

        Assert.True(hasAudio, "Sample player should have played audio in response to AudioCueFired event.");
    }

    [Fact]
    public void UnsubscribeFrom_stops_receiving_events()
    {
        using var player = CreatePlayer();
        var pcm = CreateTestSample();
        player.LoadSample("test.wav", pcm);

        var metronome = new LiveCompanion.Core.Engine.MetronomeEngine(Setlist.DefaultPpqn, 120);
        var setlistPlayer = new SetlistPlayer(metronome);

        player.SubscribeTo(setlistPlayer);
        player.UnsubscribeFrom(setlistPlayer);

        // Fire an event manually through the setlist player
        // Since we unsubscribed, the mixer should remain silent
        var cueCount = 0;
        setlistPlayer.AudioCueFired += _ => cueCount++;

        var setlist = new Setlist
        {
            Name = "Test",
            Ppqn = Setlist.DefaultPpqn,
            Songs =
            [
                new Song
                {
                    Title = "T",
                    Artist = "T",
                    DurationTicks = Setlist.DefaultPpqn,
                    Events =
                    [
                        new SectionChangeEvent { Tick = 0, SectionName = "S", Bpm = 120, TimeSignature = TimeSignature.Common, Presets = [] },
                        new AudioCueEvent { Tick = 1, SampleFileName = "test.wav", GainDb = 0 }
                    ]
                }
            ]
        };

        setlistPlayer.Load(setlist);
        setlistPlayer.PlaySynchronous();

        // The setlist player still fires AudioCueFired (confirmed by our counter)
        Assert.True(cueCount > 0, "SetlistPlayer should still fire AudioCueFired.");

        // But the mixer should be silent since we unsubscribed
        var buffer = new float[512];
        player.Mixer.Read(buffer, 0, buffer.Length);

        bool allSilent = true;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Math.Abs(buffer[i]) > 0.001f)
            {
                allSilent = false;
                break;
            }
        }

        Assert.True(allSilent, "Mixer should be silent after unsubscribing.");
    }

    [Fact]
    public void Dispose_clears_sample_cache()
    {
        var player = CreatePlayer();
        player.LoadSample("test.wav", CreateTestSample());
        Assert.Equal(1, player.LoadedSampleCount);

        player.Dispose();

        Assert.Equal(0, player.LoadedSampleCount);
    }

    [Fact]
    public void LoadSample_case_insensitive_lookup()
    {
        using var player = CreatePlayer();
        player.LoadSample("MyFile.WAV", CreateTestSample());

        var cue = new AudioCueEvent
        {
            SampleFileName = "myfile.wav",
            GainDb = 0.0
        };

        player.OnAudioCueFired(cue);

        var buffer = new float[512];
        player.Mixer.Read(buffer, 0, buffer.Length);

        bool hasAudio = false;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Math.Abs(buffer[i]) > 0.001f)
            {
                hasAudio = true;
                break;
            }
        }

        Assert.True(hasAudio, "Sample lookup should be case-insensitive.");
    }
}
