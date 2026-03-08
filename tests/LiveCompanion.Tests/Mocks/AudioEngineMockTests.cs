using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using Xunit;

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

    [Fact]
    public void GetAvailableOutputPairs_ShouldReturnPairs()
    {
        var engine = CreateEngine();
        var pairs = engine.GetAvailableOutputPairs();

        pairs.Should().NotBeEmpty();
        pairs.Should().Contain("Output 1-2");
    }

    [Fact]
    public async Task PreloadAsync_ShouldCompleteWithoutError()
    {
        var engine = CreateEngine();
        var paths = new[] { "kick.wav", "snare.wav" };

        await engine.PreloadAsync(paths);

        // PreloadAsync is a no-op mock — should not throw
    }

    [Fact]
    public async Task PlayClip_NullClip_ShouldThrow()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var act = () => engine.PlayClipAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StopAll_WithoutInit_ShouldThrow()
    {
        var engine = CreateEngine();

        var act = () => engine.StopAllAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ActiveVoices_AfterStopAll_ShouldNotGoNegative()
    {
        // Regression: fire-and-forget Task.Delay decrement was not cancelled
        // by StopAll, causing _activeVoices to go negative after Stop.
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        // Lancer un clip (200ms mock duration)
        await engine.PlayClipAsync(new AudioClip { Name = "Click" });
        engine.ActiveVoices.Should().Be(1);

        // Stop immédiatement (avant que le Task.Delay(200) ne se termine)
        await engine.StopAllAsync();
        engine.ActiveVoices.Should().Be(0);

        // Attendre que le Task.Delay(200) ait eu le temps de se terminer
        await Task.Delay(350);

        // Le compteur ne doit PAS être passé en négatif
        engine.ActiveVoices.Should().Be(0,
            "StopAll doit annuler les voix fantômes pour empêcher le compteur de devenir négatif");
    }

    [Fact]
    public async Task PlayStopPlay_ShouldMaintainCorrectVoiceCount()
    {
        // Regression: Play→Stop→Play cycle caused _activeVoices drift
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        // Cycle 1 : Play → Stop
        await engine.PlayClipAsync(new AudioClip { Name = "Click" });
        engine.ActiveVoices.Should().Be(1);
        await engine.StopAllAsync();

        // Cycle 2 : Play à nouveau
        await engine.PlayClipAsync(new AudioClip { Name = "Click" });
        engine.ActiveVoices.Should().Be(1,
            "Après un cycle Play→Stop→Play, le compteur de voix doit refléter la seule voix active");

        // Laisser la voix se terminer normalement
        await Task.Delay(350);
        engine.ActiveVoices.Should().Be(0);
    }
}
