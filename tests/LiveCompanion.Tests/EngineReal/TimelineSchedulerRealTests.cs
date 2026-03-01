using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

public class TimelineSchedulerRealTests : IDisposable
{
    private readonly ILogService _log = new DebugLogService();
    private readonly TimelineSchedulerReal _scheduler;

    public TimelineSchedulerRealTests()
    {
        _scheduler = new TimelineSchedulerReal(_log, () => false);
    }

    public void Dispose() => _scheduler.Dispose();

    private static Song CreateTestSong(int sectionCount = 3, int barsPerSection = 2, double tempo = 600)
    {
        var song = new Song { Title = "Test Song" };
        for (int i = 0; i < sectionCount; i++)
        {
            song.Sections.Add(new Section
            {
                Name = $"Section {i}",
                Tempo = tempo,
                TimeSignature = TimeSignature.Default,
                BarCount = barsPerSection,
                Order = i,
            });
        }
        return song;
    }

    // ------------------------------------------------------------------ //
    // Start
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Start_ShouldSetPositionToBeginning()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);

        var pos = _scheduler.CurrentPosition;
        pos.SectionIndex.Should().Be(0);
        pos.Bar.Should().Be(1);
        pos.Beat.Should().Be(1);

        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task Start_WithSectionIndex_ShouldStartAtGivenSection()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song, startSectionIndex: 1);

        _scheduler.CurrentPosition.SectionIndex.Should().Be(1);

        await _scheduler.StopAsync();
    }

    [Fact]
    public void Start_EmptySong_ShouldThrow()
    {
        var song = new Song { Title = "Empty" };

        var act = () => _scheduler.StartAsync(song);
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Start_NullSong_ShouldThrow()
    {
        var act = () => _scheduler.StartAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ //
    // Stop
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Stop_ShouldResetBarAndBeat()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);
        await _scheduler.StopAsync();

        var pos = _scheduler.CurrentPosition;
        pos.Bar.Should().Be(1);
        pos.Beat.Should().Be(1);
        pos.Tick.Should().Be(0);
    }

    // ------------------------------------------------------------------ //
    // Events
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task PositionChanged_ShouldFireOnStart()
    {
        var song = CreateTestSong();
        TimelinePosition? received = null;

        _scheduler.PositionChanged += (_, pos) => received = pos;
        await _scheduler.StartAsync(song);

        received.Should().NotBeNull();
        received!.SectionIndex.Should().Be(0);

        await _scheduler.StopAsync();
    }

    // ------------------------------------------------------------------ //
    // CanTransitionNow
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task CanTransitionNow_WhenNoActiveVoices_ShouldBeTrue()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);

        _scheduler.CanTransitionNow.Should().BeTrue();

        await _scheduler.StopAsync();
    }

    [Fact]
    public void CanTransitionNow_WhenNotRunning_ShouldBeTrue()
    {
        _scheduler.CanTransitionNow.Should().BeTrue();
    }

    [Fact]
    public async Task CanTransitionNow_WithActiveVoices_ShouldBeFalse()
    {
        using var scheduler = new TimelineSchedulerReal(_log, () => true);
        var song = CreateTestSong();
        await scheduler.StartAsync(song);

        scheduler.CanTransitionNow.Should().BeFalse();

        await scheduler.StopAsync();
    }

    // ------------------------------------------------------------------ //
    // NextSection
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task NextSection_ShouldAdvance()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);

        int? sectionNotified = null;
        _scheduler.SectionChanged += (_, idx) => sectionNotified = idx;

        await _scheduler.NextSectionAsync();

        _scheduler.CurrentPosition.SectionIndex.Should().Be(1);
        sectionNotified.Should().Be(1);

        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task NextSection_AtLastSection_ShouldStop()
    {
        var song = CreateTestSong(sectionCount: 1);
        await _scheduler.StartAsync(song);

        await _scheduler.NextSectionAsync();

        _scheduler.CurrentPosition.Bar.Should().Be(1);
        _scheduler.CurrentPosition.Beat.Should().Be(1);
    }

    // ------------------------------------------------------------------ //
    // Timer advancement
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Timer_ShouldAdvanceBeats()
    {
        // 600 BPM = 100ms per beat — fast enough for testing
        var song = CreateTestSong(tempo: 600);
        var positions = new List<TimelinePosition>();

        _scheduler.PositionChanged += (_, pos) => positions.Add(pos);
        await _scheduler.StartAsync(song);

        // Wait for several beats
        await Task.Delay(500);
        await _scheduler.StopAsync();

        // Should have received multiple position changes
        positions.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SectionChanged_ShouldFireOnAutoAdvance()
    {
        // 1 bar, 4/4, 600 BPM → 4 beats = 400ms per section
        var song = CreateTestSong(sectionCount: 2, barsPerSection: 1, tempo: 600);
        var sections = new List<int>();

        _scheduler.SectionChanged += (_, idx) => sections.Add(idx);
        await _scheduler.StartAsync(song);

        // Wait for auto-advance (should happen after ~400ms)
        await Task.Delay(800);
        await _scheduler.StopAsync();

        sections.Should().Contain(1);
    }

    [Fact]
    public async Task TickPrecision_ShouldTrackSubBeatPosition()
    {
        // At 60 BPM, 1 beat = 1 second, so after ~50ms we should have ticks > 0
        var song = CreateTestSong(tempo: 60);
        await _scheduler.StartAsync(song);

        // Wait a fraction of a beat
        await Task.Delay(50);

        var pos = _scheduler.CurrentPosition;

        // Should still be on beat 1 but with ticks > 0
        pos.Beat.Should().Be(1);
        pos.Tick.Should().BeGreaterThan(0, "scheduler should track sub-beat ticks");

        await _scheduler.StopAsync();
    }

    [Fact]
    public async Task StopAndRestart_ShouldResetPosition()
    {
        var song = CreateTestSong(tempo: 600);

        await _scheduler.StartAsync(song);
        await Task.Delay(250); // let it advance
        await _scheduler.StopAsync();

        // Restart from beginning
        await _scheduler.StartAsync(song);
        var pos = _scheduler.CurrentPosition;

        pos.SectionIndex.Should().Be(0);
        pos.Bar.Should().Be(1);
        pos.Beat.Should().Be(1);
        // Tick may be slightly > 0 due to thread startup latency
        pos.Tick.Should().BeLessThan(TimelineSchedulerReal.TicksPerBeat / 2,
            "position should be near the start after restart");

        await _scheduler.StopAsync();
    }
}
