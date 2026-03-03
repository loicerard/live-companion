using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;

namespace LiveCompanion.Tests.Services;

public class LiveModeGuardTests
{
    private readonly ILogService _log = new DebugLogService();

    private LiveModeGuard CreateGuard() => new(_log);

    // ------------------------------------------------------------------ //
    // Constructor
    // ------------------------------------------------------------------ //

    [Fact]
    public void Constructor_NullLog_ShouldThrow()
    {
        var act = () => new LiveModeGuard(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ //
    // Default state
    // ------------------------------------------------------------------ //

    [Fact]
    public void IsLive_ShouldBeFalseByDefault()
    {
        var guard = CreateGuard();
        guard.IsLive.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Engage / Disengage
    // ------------------------------------------------------------------ //

    [Fact]
    public void Engage_ShouldSetIsLiveTrue()
    {
        var guard = CreateGuard();
        guard.Engage();
        guard.IsLive.Should().BeTrue();
    }

    [Fact]
    public void Disengage_ShouldSetIsLiveFalse()
    {
        var guard = CreateGuard();
        guard.Engage();
        guard.Disengage();
        guard.IsLive.Should().BeFalse();
    }

    [Fact]
    public void Engage_Twice_ShouldFireEventOnce()
    {
        var guard = CreateGuard();
        int count = 0;
        guard.LiveModeChanged += (_, _) => count++;

        guard.Engage();
        guard.Engage();

        count.Should().Be(1);
    }

    [Fact]
    public void Disengage_Twice_ShouldFireEventOnce()
    {
        var guard = CreateGuard();
        guard.Engage();

        int count = 0;
        guard.LiveModeChanged += (_, _) => count++;

        guard.Disengage();
        guard.Disengage();

        count.Should().Be(1);
    }

    [Fact]
    public void Disengage_WithoutEngage_ShouldNotFireEvent()
    {
        var guard = CreateGuard();
        int count = 0;
        guard.LiveModeChanged += (_, _) => count++;

        guard.Disengage();

        count.Should().Be(0);
    }

    // ------------------------------------------------------------------ //
    // Events
    // ------------------------------------------------------------------ //

    [Fact]
    public void Engage_ShouldFireLiveModeChangedWithTrue()
    {
        var guard = CreateGuard();
        bool? received = null;
        guard.LiveModeChanged += (_, value) => received = value;

        guard.Engage();

        received.Should().BeTrue();
    }

    [Fact]
    public void Disengage_ShouldFireLiveModeChangedWithFalse()
    {
        var guard = CreateGuard();
        guard.Engage();

        bool? received = null;
        guard.LiveModeChanged += (_, value) => received = value;

        guard.Disengage();

        received.Should().BeFalse();
    }

    [Fact]
    public void ToggleCycle_ShouldWorkCorrectly()
    {
        var guard = CreateGuard();
        var states = new List<bool>();
        guard.LiveModeChanged += (_, value) => states.Add(value);

        guard.Engage();
        guard.Disengage();
        guard.Engage();
        guard.Disengage();

        states.Should().Equal(true, false, true, false);
        guard.IsLive.Should().BeFalse();
    }
}
