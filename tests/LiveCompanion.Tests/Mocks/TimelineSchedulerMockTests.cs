using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using Xunit;

namespace LiveCompanion.Tests.Mocks;

public class TimelineSchedulerMockTests : IDisposable
{
    private readonly ILogService _log = new DebugLogService();
    private readonly AudioEngineMock _audioEngine;
    private readonly TimelineSchedulerMock _scheduler;

    public TimelineSchedulerMockTests()
    {
        _audioEngine = new AudioEngineMock(_log);
        _scheduler = new TimelineSchedulerMock(_log, () => _audioEngine.ActiveVoices > 0);
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

    [Fact]
    public async Task Start_ShouldSetPositionToBeginning()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);

        var pos = _scheduler.CurrentPosition;
        pos.SectionIndex.Should().Be(0);
        pos.Bar.Should().Be(1);
        pos.Beat.Should().Be(1);
    }

    [Fact]
    public async Task Start_WithSectionIndex_ShouldStartAtGivenSection()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song, startSectionIndex: 1);

        _scheduler.CurrentPosition.SectionIndex.Should().Be(1);
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

    [Fact]
    public async Task Stop_ShouldResetBarAndBeat()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);
        await _scheduler.StopAsync();

        var pos = _scheduler.CurrentPosition;
        pos.Bar.Should().Be(1);
        pos.Beat.Should().Be(1);
    }

    [Fact]
    public async Task PositionChanged_ShouldFireOnStart()
    {
        var song = CreateTestSong();
        TimelinePosition? received = null;

        _scheduler.PositionChanged += (_, pos) => received = pos;
        await _scheduler.StartAsync(song);

        received.Should().NotBeNull();
        received!.SectionIndex.Should().Be(0);
    }

    [Fact]
    public async Task CanTransitionNow_WhenNoActiveVoices_ShouldBeTrue()
    {
        var song = CreateTestSong();
        await _scheduler.StartAsync(song);

        _scheduler.CanTransitionNow.Should().BeTrue();
    }

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
    }

    [Fact]
    public async Task NextSection_AtLastSection_ShouldStop()
    {
        var song = CreateTestSong(sectionCount: 1);
        await _scheduler.StartAsync(song);

        await _scheduler.NextSectionAsync();

        // After next on last section, position resets (stopped)
        _scheduler.CurrentPosition.Bar.Should().Be(1);
        _scheduler.CurrentPosition.Beat.Should().Be(1);
    }

    [Fact]
    public async Task Timer_ShouldAdvanceBeats()
    {
        // Fast tempo (600 BPM = 100ms per beat) to test timer progression
        var song = CreateTestSong(tempo: 600);
        var positions = new List<TimelinePosition>();

        _scheduler.PositionChanged += (_, pos) => positions.Add(pos);
        await _scheduler.StartAsync(song);

        // Wait for a few beats
        await Task.Delay(500);
        await _scheduler.StopAsync();

        // Should have received multiple position changes
        positions.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SectionChanged_ShouldFireOnAutoAdvance()
    {
        // 1 section with 1 bar, 4/4, at 600 BPM (100ms per beat)
        // After 4 beats = 400ms the section ends
        var song = CreateTestSong(sectionCount: 2, barsPerSection: 1, tempo: 600);
        var sections = new List<int>();

        _scheduler.SectionChanged += (_, idx) => sections.Add(idx);
        await _scheduler.StartAsync(song);

        // Wait for auto-advance
        await Task.Delay(800);
        await _scheduler.StopAsync();

        sections.Should().Contain(1);
    }
}
