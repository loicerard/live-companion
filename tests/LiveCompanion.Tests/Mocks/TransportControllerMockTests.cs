using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using Xunit;

namespace LiveCompanion.Tests.Mocks;

public class TransportControllerMockTests
{
    private readonly ILogService _log = new DebugLogService();

    private TransportControllerMock CreateTransport() => new(_log);

    [Fact]
    public void InitialState_ShouldBeStopped()
    {
        var transport = CreateTransport();
        transport.State.Should().Be(TransportState.Stopped);
    }

    [Fact]
    public async Task Play_ShouldChangeStateToPlaying()
    {
        var transport = CreateTransport();
        await transport.PlayAsync();

        transport.State.Should().Be(TransportState.Playing);
    }

    [Fact]
    public async Task Pause_FromPlaying_ShouldChangeStateToPaused()
    {
        var transport = CreateTransport();
        await transport.PlayAsync();
        await transport.PauseAsync();

        transport.State.Should().Be(TransportState.Paused);
    }

    [Fact]
    public async Task Pause_FromStopped_ShouldRemainStopped()
    {
        var transport = CreateTransport();
        await transport.PauseAsync();

        transport.State.Should().Be(TransportState.Stopped);
    }

    [Fact]
    public async Task Stop_FromPlaying_ShouldChangeStateToStopped()
    {
        var transport = CreateTransport();
        await transport.PlayAsync();
        await transport.StopAsync();

        transport.State.Should().Be(TransportState.Stopped);
    }

    [Fact]
    public async Task Stop_FromPaused_ShouldChangeStateToStopped()
    {
        var transport = CreateTransport();
        await transport.PlayAsync();
        await transport.PauseAsync();
        await transport.StopAsync();

        transport.State.Should().Be(TransportState.Stopped);
    }

    [Fact]
    public async Task Play_WhenAlreadyPlaying_ShouldNotFireEvent()
    {
        var transport = CreateTransport();
        await transport.PlayAsync();

        int eventCount = 0;
        transport.StateChanged += (_, _) => eventCount++;

        await transport.PlayAsync(); // already playing

        eventCount.Should().Be(0);
    }

    [Fact]
    public async Task Stop_WhenAlreadyStopped_ShouldNotFireEvent()
    {
        var transport = CreateTransport();

        int eventCount = 0;
        transport.StateChanged += (_, _) => eventCount++;

        await transport.StopAsync(); // already stopped

        eventCount.Should().Be(0);
    }

    [Fact]
    public async Task StateChanged_ShouldFireOnTransition()
    {
        var transport = CreateTransport();
        var states = new List<TransportState>();

        transport.StateChanged += (_, state) => states.Add(state);

        await transport.PlayAsync();
        await transport.PauseAsync();
        await transport.StopAsync();

        states.Should().Equal(
            TransportState.Playing,
            TransportState.Paused,
            TransportState.Stopped);
    }

    [Fact]
    public async Task FullCycle_PlayPausePlayStop()
    {
        var transport = CreateTransport();

        await transport.PlayAsync();
        transport.State.Should().Be(TransportState.Playing);

        await transport.PauseAsync();
        transport.State.Should().Be(TransportState.Paused);

        await transport.PlayAsync();
        transport.State.Should().Be(TransportState.Playing);

        await transport.StopAsync();
        transport.State.Should().Be(TransportState.Stopped);
    }
}
