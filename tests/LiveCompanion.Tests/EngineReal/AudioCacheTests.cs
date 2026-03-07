using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;
using NAudio.Wave;

namespace LiveCompanion.Tests.EngineReal;

public class AudioCacheTests : IDisposable
{
    private readonly ILogService _log = new DebugLogService();
    private readonly string _testDir;

    public AudioCacheTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"LiveCompanion_AudioCacheTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    private AudioCache CreateCache(long maxMemoryBytes = AudioCache.DefaultMaxMemoryBytes)
        => new(_log, maxMemoryBytes);

    // ------------------------------------------------------------------ //
    // Test WAV generation helper
    // ------------------------------------------------------------------ //

    private string CreateTestWav(
        string fileName = "test.wav",
        int sampleRate = 44100,
        int durationMs = 100,
        int channels = 1)
    {
        var path = Path.Combine(_testDir, fileName);
        int totalSamples = sampleRate * durationMs / 1000;

        var format = new WaveFormat(sampleRate, 16, channels);
        using var writer = new WaveFileWriter(path, format);

        var buffer = new float[totalSamples * channels];
        for (int i = 0; i < totalSamples; i++)
        {
            float sample = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
            for (int ch = 0; ch < channels; ch++)
                buffer[i * channels + ch] = sample;
        }

        writer.WriteSamples(buffer, 0, buffer.Length);
        return path;
    }

    // ------------------------------------------------------------------ //
    // PreloadAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task PreloadAsync_ValidWav_ShouldCacheFloatSamples()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav();

        await cache.PreloadAsync([wavPath]);

        var cached = cache.Get(wavPath);
        cached.Should().NotBeNull();
        cached!.Samples.Should().NotBeEmpty();
        cached.SampleRate.Should().Be(48_000); // resampled to 48kHz
        cached.Channels.Should().Be(1);
    }

    [Fact]
    public async Task PreloadAsync_StereoWav_ShouldCacheStereoSamples()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav(fileName: "stereo.wav", channels: 2);

        await cache.PreloadAsync([wavPath]);

        var cached = cache.Get(wavPath);
        cached.Should().NotBeNull();
        cached!.Channels.Should().Be(2);
    }

    [Fact]
    public async Task PreloadAsync_48kHzWav_ShouldNotResample()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav(fileName: "48k.wav", sampleRate: 48000);

        await cache.PreloadAsync([wavPath]);

        var cached = cache.Get(wavPath);
        cached.Should().NotBeNull();
        cached!.SampleRate.Should().Be(48_000);
    }

    [Fact]
    public async Task PreloadAsync_NonExistentFile_ShouldLogError_NotThrow()
    {
        var cache = CreateCache();

        var act = () => cache.PreloadAsync(["/nonexistent/path/audio.wav"]);

        await act.Should().NotThrowAsync();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task PreloadAsync_EmptyPaths_ShouldNotThrow()
    {
        var cache = CreateCache();

        var act = () => cache.PreloadAsync([]);

        await act.Should().NotThrowAsync();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task PreloadAsync_AlreadyCached_ShouldNotReload()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav();

        await cache.PreloadAsync([wavPath]);
        var first = cache.Get(wavPath);

        await cache.PreloadAsync([wavPath]);
        var second = cache.Get(wavPath);

        // Same reference — was not reloaded
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public async Task PreloadAsync_MultipleFiles_ShouldCacheAll()
    {
        var cache = CreateCache();
        var wav1 = CreateTestWav(fileName: "clip1.wav");
        var wav2 = CreateTestWav(fileName: "clip2.wav");

        await cache.PreloadAsync([wav1, wav2]);

        cache.Count.Should().Be(2);
        cache.Get(wav1).Should().NotBeNull();
        cache.Get(wav2).Should().NotBeNull();
    }

    [Fact]
    public async Task PreloadAsync_UnsupportedExtension_ShouldSkip()
    {
        var cache = CreateCache();
        var path = Path.Combine(_testDir, "test.ogg");
        File.WriteAllText(path, "fake ogg data");

        await cache.PreloadAsync([path]);

        cache.Count.Should().Be(0);
    }

    // ------------------------------------------------------------------ //
    // Get
    // ------------------------------------------------------------------ //

    [Fact]
    public void Get_UncachedFile_ShouldReturnNull()
    {
        var cache = CreateCache();

        var result = cache.Get("/some/random/file.wav");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_CaseInsensitive_ShouldMatch()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav(fileName: "TestClip.wav");

        await cache.PreloadAsync([wavPath]);

        // Query with different casing
        var upper = cache.Get(wavPath.ToUpperInvariant());
        upper.Should().NotBeNull();
    }

    // ------------------------------------------------------------------ //
    // Clear
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Clear_ShouldEmptyCache()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav();
        await cache.PreloadAsync([wavPath]);

        cache.Count.Should().Be(1);

        cache.Clear();

        cache.Count.Should().Be(0);
        cache.Get(wavPath).Should().BeNull();
    }

    // ------------------------------------------------------------------ //
    // TotalMemoryBytes
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task TotalMemoryBytes_ShouldReflectCachedData()
    {
        var cache = CreateCache();

        cache.TotalMemoryBytes.Should().Be(0);

        var wavPath = CreateTestWav(durationMs: 100);
        await cache.PreloadAsync([wavPath]);

        cache.TotalMemoryBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TotalMemoryBytes_AfterClear_ShouldBeZero()
    {
        var cache = CreateCache();
        var wavPath = CreateTestWav();
        await cache.PreloadAsync([wavPath]);

        cache.Clear();

        cache.TotalMemoryBytes.Should().Be(0);
    }

    // ------------------------------------------------------------------ //
    // LRU eviction
    // ------------------------------------------------------------------ //

    [Fact]
    public void MaxMemoryBytes_ShouldBeConfigurable()
    {
        var cache = CreateCache(maxMemoryBytes: 1024 * 1024);
        cache.MaxMemoryBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void MaxMemoryBytes_Default_ShouldBe256MB()
    {
        var cache = CreateCache();
        cache.MaxMemoryBytes.Should().Be(AudioCache.DefaultMaxMemoryBytes);
    }

    [Fact]
    public async Task Evict_WhenOverMemoryLimit_ShouldRemoveOldestEntries()
    {
        // Create a cache with a very small limit so our test WAVs will trigger eviction.
        // A 100ms mono WAV at 48kHz ≈ 4800 samples × 4 bytes = ~19 KB
        var cache = CreateCache(maxMemoryBytes: 25_000);

        var wav1 = CreateTestWav(fileName: "clip1.wav", durationMs: 100);
        var wav2 = CreateTestWav(fileName: "clip2.wav", durationMs: 100);

        // Load sequentially to control LRU order
        await cache.PreloadAsync([wav1]);
        await cache.PreloadAsync([wav2]);

        // wav1 was loaded first (oldest), so it should be evicted
        cache.Get(wav1).Should().BeNull("oldest entry should be evicted when over limit");
        cache.Get(wav2).Should().NotBeNull("most recent entry should be kept");
        cache.TotalMemoryBytes.Should().BeLessOrEqualTo(25_000);
    }

    [Fact]
    public async Task Get_ShouldUpdateLruOrder()
    {
        // Limit that fits ~2 clips but not 3
        var cache = CreateCache(maxMemoryBytes: 45_000);

        var wav1 = CreateTestWav(fileName: "a.wav", durationMs: 100);
        var wav2 = CreateTestWav(fileName: "b.wav", durationMs: 100);
        var wav3 = CreateTestWav(fileName: "c.wav", durationMs: 100);

        await cache.PreloadAsync([wav1]);
        await cache.PreloadAsync([wav2]);

        // Access wav1 to make it most-recently-used
        cache.Get(wav1).Should().NotBeNull();

        // Load wav3 — should evict wav2 (now the oldest), not wav1
        await cache.PreloadAsync([wav3]);

        cache.Get(wav1).Should().NotBeNull("recently accessed entry should survive eviction");
        cache.Get(wav2).Should().BeNull("oldest untouched entry should be evicted");
        cache.Get(wav3).Should().NotBeNull("newly loaded entry should be present");
    }
}
