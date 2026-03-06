using FluentAssertions;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

public class AsioOutputProviderMeteringTests
{
    private static VoicePool CreatePool() => new(new DebugLogService());

    [Fact]
    public void BusLevels_WhenSilent_ShouldBeZero()
    {
        var pool = CreatePool();
        var busMap = new Dictionary<string, (int Left, int Right)> { ["Main"] = (0, 1) };
        var provider = new AsioOutputProvider(pool, busMap, 2, sampleRate: 48000, bufferSize: 256);

        // Trigger a Read to compute levels
        var buffer = new byte[256 * 2 * sizeof(float)];
        provider.Read(buffer, 0, buffer.Length);

        provider.BusLevels.Should().ContainKey("Main");
        var (left, right) = provider.BusLevels["Main"];
        left.Should().Be(0f);
        right.Should().Be(0f);
    }

    [Fact]
    public void BusLevels_ShouldContainAllBuses()
    {
        var pool = CreatePool();
        var busMap = new Dictionary<string, (int Left, int Right)>
        {
            ["Main"] = (0, 1),
            ["Click"] = (2, 3)
        };
        var provider = new AsioOutputProvider(pool, busMap, 4, sampleRate: 48000, bufferSize: 256);

        var buffer = new byte[256 * 4 * sizeof(float)];
        provider.Read(buffer, 0, buffer.Length);

        provider.BusLevels.Should().ContainKeys("Main", "Click");
    }

    [Fact]
    public void BusLevels_ShouldBeClamped()
    {
        var pool = CreatePool();
        var busMap = new Dictionary<string, (int Left, int Right)> { ["Main"] = (0, 1) };
        var provider = new AsioOutputProvider(pool, busMap, 2, sampleRate: 48000, bufferSize: 256);

        var buffer = new byte[256 * 2 * sizeof(float)];
        provider.Read(buffer, 0, buffer.Length);

        foreach (var (_, (left, right)) in provider.BusLevels)
        {
            left.Should().BeInRange(0f, 1f);
            right.Should().BeInRange(0f, 1f);
        }
    }
}
