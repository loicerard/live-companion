using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;

namespace LiveCompanion.Tests.Mocks;

public class AudioEngineMockMeteringTests
{
    private readonly AudioEngineMock _engine = new(new DebugLogService());

    private static AudioConfig ConfigWithBuses => new()
    {
        DriverName = "TestDriver",
        BufferSize = 256,
        BusMappings = { ["Main"] = "Output 1-2", ["Click"] = "Output 3-4" }
    };

    [Fact]
    public async Task GetBusLevels_NoVoices_ShouldReturnZeros()
    {
        await _engine.InitializeAsync(ConfigWithBuses);

        var levels = _engine.GetBusLevels();

        levels.Should().ContainKeys("Main", "Click");
        levels["Main"].Left.Should().Be(0f);
        levels["Main"].Right.Should().Be(0f);
        levels["Click"].Left.Should().Be(0f);
        levels["Click"].Right.Should().Be(0f);
    }

    [Fact]
    public async Task GetBusLevels_WithActiveVoices_ShouldReturnNonZero()
    {
        await _engine.InitializeAsync(ConfigWithBuses);
        var clip = new AudioClip { Name = "test", FilePath = "test.wav", Sends = [new BusSend { BusName = "Main" }] };
        await _engine.PlayClipAsync(clip);

        var levels = _engine.GetBusLevels();

        levels["Main"].Left.Should().BeGreaterThan(0f);
        levels["Main"].Right.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void GetBusLevels_BeforeInit_ShouldReturnDefaultBuses()
    {
        var levels = _engine.GetBusLevels();

        // Default buses are Main and Click
        levels.Should().ContainKeys("Main", "Click");
    }

    [Fact]
    public async Task GetBusLevels_LevelsShouldBeClamped()
    {
        await _engine.InitializeAsync(ConfigWithBuses);

        // Play many clips to max out voices
        for (int i = 0; i < 15; i++)
        {
            var clip = new AudioClip { Name = $"clip{i}", FilePath = $"test{i}.wav", Sends = [new BusSend { BusName = "Main" }] };
            await _engine.PlayClipAsync(clip);
        }

        var levels = _engine.GetBusLevels();
        foreach (var (_, (left, right)) in levels)
        {
            left.Should().BeInRange(0f, 1f);
            right.Should().BeInRange(0f, 1f);
        }
    }
}
