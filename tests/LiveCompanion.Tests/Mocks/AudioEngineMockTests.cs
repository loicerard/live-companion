using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;

namespace LiveCompanion.Tests.Mocks;

public class AudioEngineMockTests
{
    private readonly ILogService _log = new DebugLogService();

    private AudioEngineMock CreateEngine() => new(_log);

    private static AudioConfig DefaultConfig => new()
    {
        DriverName = "TestDriver",
        BufferSize = 256,
    };

    [Fact]
    public async Task Initialize_ShouldSucceed()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        // Should not throw
        engine.ActiveVoices.Should().Be(0);
    }

    [Fact]
    public void Initialize_NullConfig_ShouldThrow()
    {
        var engine = CreateEngine();

        var act = () => engine.InitializeAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GetAvailableDrivers_ShouldReturnFakeDrivers()
    {
        var engine = CreateEngine();
        var drivers = engine.GetAvailableDrivers();

        drivers.Should().HaveCount(2);
        drivers.Should().Contain("MockASIO Driver");
    }

    [Fact]
    public void GetSupportedBufferSizes_ShouldReturnValues()
    {
        var engine = CreateEngine();
        var sizes = engine.GetSupportedBufferSizes();

        sizes.Should().Contain(256);
        sizes.Should().Contain(512);
    }

    [Fact]
    public async Task PlayClip_WithoutInit_ShouldThrow()
    {
        var engine = CreateEngine();
        var clip = new AudioClip { Name = "Test" };

        var act = () => engine.PlayClipAsync(clip);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PlayClip_ShouldIncrementActiveVoices()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var clip = new AudioClip { Name = "Kick", FilePath = "kick.wav" };
        await engine.PlayClipAsync(clip);

        engine.ActiveVoices.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlayClip_VoiceShouldExpireAfterDelay()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var clip = new AudioClip { Name = "Kick" };
        await engine.PlayClipAsync(clip);

        // Wait for the 200ms simulated playback
        await Task.Delay(350);

        engine.ActiveVoices.Should().Be(0);
    }

    [Fact]
    public async Task PlayClip_ExceedingMaxVoices_ShouldDropClip()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        // Play MaxVoices clips
        for (int i = 0; i < AudioEngineMock.MaxVoices; i++)
            await engine.PlayClipAsync(new AudioClip { Name = $"Clip{i}" });

        engine.ActiveVoices.Should().Be(AudioEngineMock.MaxVoices);

        // 17th clip should be dropped
        await engine.PlayClipAsync(new AudioClip { Name = "Dropped" });
        engine.ActiveVoices.Should().Be(AudioEngineMock.MaxVoices);
    }

    [Fact]
    public async Task StopAll_ShouldResetActiveVoices()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        await engine.PlayClipAsync(new AudioClip { Name = "Kick" });
        engine.ActiveVoices.Should().BeGreaterThan(0);

        await engine.StopAllAsync();
        engine.ActiveVoices.Should().Be(0);
    }

    [Fact]
    public async Task Shutdown_ShouldResetState()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);
        await engine.PlayClipAsync(new AudioClip { Name = "Kick" });

        await engine.ShutdownAsync();

        engine.ActiveVoices.Should().Be(0);

        // After shutdown, playing should throw
        var act = () => engine.PlayClipAsync(new AudioClip { Name = "Test" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
