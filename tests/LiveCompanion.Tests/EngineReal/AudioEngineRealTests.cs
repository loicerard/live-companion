using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

/// <summary>
/// Stub implementation of <see cref="IAsioInterop"/> for unit testing.
/// All ASIO operations are simulated in memory.
/// </summary>
internal sealed class FakeAsioInterop : IAsioInterop
{
    private readonly IReadOnlyList<string> _driverNames;
    private readonly AsioBufferInfo _bufferInfo;
    private readonly int _outputChannelCount;

    private bool _isOpen;
    private bool _isPlaying;

    public FakeAsioInterop(
        IReadOnlyList<string>? driverNames = null,
        AsioBufferInfo? bufferInfo = null,
        int outputChannelCount = 8)
    {
        _driverNames = driverNames ?? ["FakeASIO Driver"];
        _bufferInfo = bufferInfo ?? new AsioBufferInfo(64, 4096, 256, -1);
        _outputChannelCount = outputChannelCount;
    }

    public string? LastOpenedDriver { get; private set; }
    public int OpenDriverCallCount { get; private set; }
    public int CloseDriverCallCount { get; private set; }

    public IReadOnlyList<string> GetDriverNames() => _driverNames;

    public void OpenDriver(string driverName)
    {
        LastOpenedDriver = driverName;
        OpenDriverCallCount++;
        _isOpen = true;
    }

    public void CloseDriver()
    {
        StopPlayback();
        CloseDriverCallCount++;
        _isOpen = false;
    }

    public bool IsDriverOpen => _isOpen;

    public AsioBufferInfo GetBufferInfo()
    {
        if (!_isOpen) throw new InvalidOperationException("No driver open.");
        return _bufferInfo;
    }

    public int OutputChannelCount
    {
        get
        {
            if (!_isOpen) throw new InvalidOperationException("No driver open.");
            return _outputChannelCount;
        }
    }

    public string GetOutputChannelName(int index)
    {
        if (!_isOpen) throw new InvalidOperationException("No driver open.");
        return $"Output {index + 1}";
    }

    public int SampleRate => _isOpen ? 48000 : 0;

    public void InitPlayback(NAudio.Wave.IWaveProvider provider) { }
    public void Play() => _isPlaying = true;
    public void StopPlayback() => _isPlaying = false;
    public bool IsPlaying => _isPlaying;

    public void Dispose() => CloseDriver();
}

public class AudioEngineRealTests
{
    private readonly ILogService _log = new DebugLogService();

    private (AudioEngineReal Engine, FakeAsioInterop Asio, AudioCache Cache) Create(
        IReadOnlyList<string>? driverNames = null,
        AsioBufferInfo? bufferInfo = null,
        int outputChannelCount = 8)
    {
        var asio = new FakeAsioInterop(driverNames, bufferInfo, outputChannelCount);
        var cache = new AudioCache(_log);
        var engine = new AudioEngineReal(_log, asio, cache);
        return (engine, asio, cache);
    }

    private static AudioConfig DefaultConfig => new()
    {
        DriverName = "FakeASIO Driver",
        BufferSize = 256,
        BusMappings = { ["Main"] = "Output 1-2", ["Click"] = "Output 3-4" },
    };

    // ------------------------------------------------------------------ //
    // GetAvailableDrivers
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetAvailableDrivers_ShouldReturnDriverList()
    {
        var (engine, _, _) = Create(driverNames: ["Driver A", "Driver B"]);

        var drivers = engine.GetAvailableDrivers();

        drivers.Should().HaveCount(2);
        drivers.Should().Contain("Driver A");
        drivers.Should().Contain("Driver B");
    }

    [Fact]
    public void GetAvailableDrivers_NoDrivers_ShouldReturnEmptyList()
    {
        var (engine, _, _) = Create(driverNames: []);

        var drivers = engine.GetAvailableDrivers();

        drivers.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ //
    // InitializeAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InitializeAsync_ShouldOpenDriver()
    {
        var (engine, asio, _) = Create();

        await engine.InitializeAsync(DefaultConfig);

        asio.LastOpenedDriver.Should().Be("FakeASIO Driver");
        asio.OpenDriverCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_NullConfig_ShouldThrow()
    {
        var (engine, _, _) = Create();

        var act = () => engine.InitializeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeAsync_EmptyDriverName_ShouldThrow()
    {
        var (engine, _, _) = Create();
        var config = new AudioConfig { DriverName = "", BufferSize = 256 };

        var act = () => engine.InitializeAsync(config);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeAsync_WhitespaceDriverName_ShouldThrow()
    {
        var (engine, _, _) = Create();
        var config = new AudioConfig { DriverName = "   ", BufferSize = 256 };

        var act = () => engine.InitializeAsync(config);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_ShouldShutdownFirst()
    {
        var (engine, asio, _) = Create();

        await engine.InitializeAsync(DefaultConfig);
        await engine.InitializeAsync(new AudioConfig
        {
            DriverName = "FakeASIO Driver",
            BufferSize = 512,
        });

        // Should have opened twice and closed once (the first session)
        asio.OpenDriverCallCount.Should().Be(2);
        asio.CloseDriverCallCount.Should().BeGreaterOrEqualTo(1);
    }

    // ------------------------------------------------------------------ //
    // GetSupportedBufferSizes
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetSupportedBufferSizes_NoDriverOpen_ShouldReturnEmpty()
    {
        var (engine, _, _) = Create();

        var sizes = engine.GetSupportedBufferSizes();

        sizes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSupportedBufferSizes_PowerOf2_ShouldReturnCorrectList()
    {
        var bufferInfo = new AsioBufferInfo(64, 2048, 256, -1);
        var (engine, _, _) = Create(bufferInfo: bufferInfo);

        await engine.InitializeAsync(DefaultConfig);
        var sizes = engine.GetSupportedBufferSizes();

        sizes.Should().BeEquivalentTo([64, 128, 256, 512, 1024, 2048]);
    }

    [Fact]
    public async Task GetSupportedBufferSizes_LinearGranularity_ShouldReturnCorrectList()
    {
        var bufferInfo = new AsioBufferInfo(128, 512, 256, 128);
        var (engine, _, _) = Create(bufferInfo: bufferInfo);

        await engine.InitializeAsync(DefaultConfig);
        var sizes = engine.GetSupportedBufferSizes();

        sizes.Should().BeEquivalentTo([128, 256, 384, 512]);
    }

    [Fact]
    public async Task GetSupportedBufferSizes_GranularityZero_ShouldReturnPreferred()
    {
        var bufferInfo = new AsioBufferInfo(128, 1024, 512, 0);
        var (engine, _, _) = Create(bufferInfo: bufferInfo);

        await engine.InitializeAsync(DefaultConfig);
        var sizes = engine.GetSupportedBufferSizes();

        sizes.Should().ContainSingle().Which.Should().Be(512);
    }

    // ------------------------------------------------------------------ //
    // ComputeBufferSizes (static helper)
    // ------------------------------------------------------------------ //

    [Fact]
    public void ComputeBufferSizes_InvalidRange_ShouldReturnPreferred()
    {
        var result = AudioEngineReal.ComputeBufferSizes(new AsioBufferInfo(0, 0, 512, -1));

        result.Should().ContainSingle().Which.Should().Be(512);
    }

    [Fact]
    public void ComputeBufferSizes_InvalidRange_NoPreferred_ShouldReturn256()
    {
        var result = AudioEngineReal.ComputeBufferSizes(new AsioBufferInfo(-1, -1, 0, -1));

        result.Should().ContainSingle().Which.Should().Be(256);
    }

    // ------------------------------------------------------------------ //
    // ShutdownAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task ShutdownAsync_ShouldCloseDriverAndClearCache()
    {
        var (engine, asio, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        await engine.ShutdownAsync();

        asio.CloseDriverCallCount.Should().BeGreaterOrEqualTo(1);
        cache.Count.Should().Be(0);
        engine.GetSupportedBufferSizes().Should().BeEmpty();
    }

    [Fact]
    public async Task ShutdownAsync_WithoutInit_ShouldNotThrow()
    {
        var (engine, _, _) = Create();

        var act = () => engine.ShutdownAsync();

        await act.Should().NotThrowAsync();
    }

    // ------------------------------------------------------------------ //
    // Bus mapping
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InitializeAsync_ShouldResolveBusMappings()
    {
        var (engine, _, _) = Create(outputChannelCount: 8);
        var config = new AudioConfig
        {
            DriverName = "FakeASIO Driver",
            BufferSize = 256,
            BusMappings =
            {
                ["Main"] = "Output 1-2",
                ["Click"] = "Output 3-4",
            },
        };

        // Should not throw — bus mappings get resolved internally
        await engine.InitializeAsync(config);

        // Verify buffer sizes are available after init (proves driver was opened)
        engine.GetSupportedBufferSizes().Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_NotEnoughOutputs_ShouldNotThrow()
    {
        var (engine, _, _) = Create(outputChannelCount: 2);
        var config = new AudioConfig
        {
            DriverName = "FakeASIO Driver",
            BufferSize = 256,
            BusMappings =
            {
                ["Main"] = "Output 1-2",
                ["Click"] = "Output 3-4",  // channels 2-3 don't exist
            },
        };

        // Should log a warning but not throw
        var act = () => engine.InitializeAsync(config);
        await act.Should().NotThrowAsync();
    }

    // ------------------------------------------------------------------ //
    // PlayClipAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task PlayClipAsync_NotInitialized_ShouldThrow()
    {
        var (engine, _, _) = Create();
        var clip = new AudioClip { Name = "Test", FilePath = "/test.wav" };

        var act = () => engine.PlayClipAsync(clip);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PlayClipAsync_NullClip_ShouldThrow()
    {
        var (engine, _, _) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var act = () => engine.PlayClipAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlayClipAsync_FileNotCached_ShouldNotThrow()
    {
        var (engine, _, _) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var clip = new AudioClip { Name = "Missing", FilePath = "/missing.wav" };

        // Should log warning but not throw
        var act = () => engine.PlayClipAsync(clip);
        await act.Should().NotThrowAsync();

        // No voice should be allocated
        engine.ActiveVoices.Should().Be(0);
    }

    [Fact]
    public async Task PlayClipAsync_CachedFile_ShouldAllocateVoice()
    {
        var (engine, _, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        // Create a temp WAV file and cache it
        var tempFile = CreateTempWavFile();
        try
        {
            await cache.PreloadAsync([tempFile]);

            var clip = new AudioClip { Name = "Test", FilePath = tempFile, Sends = [new BusSend { BusName = "Main" }] };
            await engine.PlayClipAsync(clip);

            engine.ActiveVoices.Should().Be(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PlayClipAsync_MultipleClips_ShouldTrackActiveVoices()
    {
        var (engine, _, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var tempFile = CreateTempWavFile();
        try
        {
            await cache.PreloadAsync([tempFile]);

            for (int i = 0; i < 3; i++)
            {
                var clip = new AudioClip { Name = $"Clip {i}", FilePath = tempFile, Sends = [new BusSend { BusName = "Main" }] };
                await engine.PlayClipAsync(clip);
            }

            engine.ActiveVoices.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PlayClipAsync_WithVolumeAndFades_ShouldNotThrow()
    {
        var (engine, _, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var tempFile = CreateTempWavFile();
        try
        {
            await cache.PreloadAsync([tempFile]);

            var clip = new AudioClip
            {
                Name = "Faded",
                FilePath = tempFile,
                Sends = [new BusSend { BusName = "Main", Volume = 0.75 }],
                FadeInSeconds = 0.1,
                FadeOutSeconds = 0.2,
            };

            var act = () => engine.PlayClipAsync(clip);
            await act.Should().NotThrowAsync();
            engine.ActiveVoices.Should().Be(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------ //
    // StopAllAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task StopAllAsync_NotInitialized_ShouldThrow()
    {
        var (engine, _, _) = Create();

        var act = () => engine.StopAllAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StopAllAsync_ShouldReleaseAllVoices()
    {
        var (engine, _, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var tempFile = CreateTempWavFile();
        try
        {
            await cache.PreloadAsync([tempFile]);

            for (int i = 0; i < 5; i++)
            {
                var clip = new AudioClip { Name = $"Clip {i}", FilePath = tempFile, Sends = [new BusSend { BusName = "Main" }] };
                await engine.PlayClipAsync(clip);
            }

            engine.ActiveVoices.Should().Be(5);

            await engine.StopAllAsync();

            engine.ActiveVoices.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------ //
    // ShutdownAsync — voice pool
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task ShutdownAsync_ShouldStopAllVoices()
    {
        var (engine, _, cache) = Create();
        await engine.InitializeAsync(DefaultConfig);

        var tempFile = CreateTempWavFile();
        try
        {
            await cache.PreloadAsync([tempFile]);

            var clip = new AudioClip { Name = "Test", FilePath = tempFile, Sends = [new BusSend { BusName = "Main" }] };
            await engine.PlayClipAsync(clip);
            engine.ActiveVoices.Should().Be(1);

            await engine.ShutdownAsync();

            engine.ActiveVoices.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ------------------------------------------------------------------ //
    // GetAvailableOutputPairs
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GetAvailableOutputPairs_ShouldReturnStereoPairs()
    {
        var (engine, _, _) = Create(outputChannelCount: 8);
        await engine.InitializeAsync(DefaultConfig);

        var pairs = engine.GetAvailableOutputPairs();

        pairs.Should().HaveCount(4);
        pairs[0].Should().Be("Output 1-Output 2");
        pairs[1].Should().Be("Output 3-Output 4");
    }

    [Fact]
    public void GetAvailableOutputPairs_NoDriverOpen_ShouldReturnEmpty()
    {
        var (engine, _, _) = Create();

        var pairs = engine.GetAvailableOutputPairs();

        pairs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableOutputPairs_OddChannelCount_ShouldIncludeLastSolo()
    {
        var (engine, _, _) = Create(outputChannelCount: 5);
        await engine.InitializeAsync(DefaultConfig);

        var pairs = engine.GetAvailableOutputPairs();

        // 2 stereo pairs + 1 solo channel
        pairs.Should().HaveCount(3);
        pairs[2].Should().Be("Output 5");
    }

    // ------------------------------------------------------------------ //
    // TryResolveChannelPair
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("Output 1-2", 0, 1)]
    [InlineData("Output 3-4", 2, 3)]
    [InlineData("Ch 1-2", 0, 1)]
    public void TryResolveChannelPair_ShortFormat_ShouldResolve(string name, int expectedLeft, int expectedRight)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var result = AudioEngineReal.TryResolveChannelPair(name, lookup, out int left, out int right);

        result.Should().BeTrue();
        left.Should().Be(expectedLeft);
        right.Should().Be(expectedRight);
    }

    [Theory]
    [InlineData("Output 1-Output 2", 0, 1)]
    [InlineData("Output 3-Output 4", 2, 3)]
    [InlineData("Analog 1-Analog 2", 0, 1)]
    public void TryResolveChannelPair_FullNameFormat_ShouldResolve(string name, int expectedLeft, int expectedRight)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var result = AudioEngineReal.TryResolveChannelPair(name, lookup, out int left, out int right);

        result.Should().BeTrue();
        left.Should().Be(expectedLeft);
        right.Should().Be(expectedRight);
    }

    [Fact]
    public void TryResolveChannelPair_ChannelNameLookup_ShouldResolve()
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Analog L"] = 0,
            ["Analog R"] = 1,
        };

        var result = AudioEngineReal.TryResolveChannelPair("Analog L-Analog R", lookup, out int left, out int right);

        result.Should().BeTrue();
        left.Should().Be(0);
        right.Should().Be(1);
    }

    [Fact]
    public void TryResolveChannelPair_ExactNameFallback_ShouldResolve()
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Headphones"] = 4,
        };

        var result = AudioEngineReal.TryResolveChannelPair("Headphones", lookup, out int left, out int right);

        result.Should().BeTrue();
        left.Should().Be(4);
        right.Should().Be(5);
    }

    [Fact]
    public void TryResolveChannelPair_UnknownName_ShouldReturnFalse()
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var result = AudioEngineReal.TryResolveChannelPair("Unknown", lookup, out _, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithFullNameFormat_ShouldResolveBusMappings()
    {
        var (engine, _, _) = Create(outputChannelCount: 8);
        var config = new AudioConfig
        {
            DriverName = "FakeASIO Driver",
            BufferSize = 256,
            BusMappings =
            {
                ["Main"] = "Output 1-Output 2",
                ["Click"] = "Output 3-Output 4",
            },
        };

        // Should resolve correctly — no exception
        await engine.InitializeAsync(config);
        engine.GetSupportedBufferSizes().Should().NotBeEmpty();
    }

    // ------------------------------------------------------------------ //
    // ASIO Playback wiring
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InitializeAsync_ShouldStartPlayback()
    {
        var (engine, asio, _) = Create();

        await engine.InitializeAsync(DefaultConfig);

        asio.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldStopPlayback()
    {
        var (engine, asio, _) = Create();
        await engine.InitializeAsync(DefaultConfig);

        await engine.ShutdownAsync();

        asio.IsPlaying.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Creates a minimal valid WAV file in the temp directory.
    /// Returns the full path to the file.
    /// </summary>
    private static string CreateTempWavFile(int sampleCount = 4800, int sampleRate = 48000)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.wav");
        var format = new NAudio.Wave.WaveFormat(sampleRate, 16, 2);
        using var writer = new NAudio.Wave.WaveFileWriter(path, format);

        for (int i = 0; i < sampleCount; i++)
        {
            float sample = MathF.Sin(2 * MathF.PI * 440 * i / sampleRate);
            writer.WriteSample(sample); // Left
            writer.WriteSample(sample); // Right
        }

        return path;
    }
}
