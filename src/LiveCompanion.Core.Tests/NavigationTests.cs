using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Tests;

public class NavigationTests
{
    private MetronomeEngine CreateMetronome() => new(Setlist.DefaultPpqn, 120);

    [Fact]
    public void Player_starts_in_idle_state()
    {
        var player = new SetlistPlayer(CreateMetronome());
        Assert.Equal(PlayerState.Idle, player.State);
    }

    [Fact]
    public void Play_transitions_to_playing()
    {
        var player = new SetlistPlayer(CreateMetronome());
        player.Load(TestSetlistFactory.CreateSingleSongSetlist());

        // Use synchronous play so we don't need async in the assertion window
        // We'll check state transitions via events
        var states = new List<PlayerState>();
        player.SongStarted += (_, _) => states.Add(player.State);

        player.PlaySynchronous();

        Assert.Contains(PlayerState.Playing, states);
    }

    [Fact]
    public void Play_without_load_throws()
    {
        var player = new SetlistPlayer(CreateMetronome());
        Assert.Throws<InvalidOperationException>(() => player.PlaySynchronous());
    }

    [Fact]
    public void Stop_transitions_to_stopped()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateSingleSongSetlist());

        player.SongStarted += (_, _) => player.SynchronousStopRequested = true;

        player.PlaySynchronous();

        Assert.Equal(PlayerState.Stopped, player.State);
    }

    [Fact]
    public void Plays_all_songs_and_fires_setlist_completed()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        bool completed = false;
        player.SetlistCompleted += () => completed = true;

        player.PlaySynchronous();

        Assert.True(completed);
        Assert.Equal(PlayerState.Stopped, player.State);
    }

    [Fact]
    public void Songs_play_in_order()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        var setlist = TestSetlistFactory.CreateTwoSongSetlist();
        player.Load(setlist);

        var songTitles = new List<string>();
        player.SongStarted += (song, _) => songTitles.Add(song.Title);

        player.PlaySynchronous();

        Assert.Equal(2, songTitles.Count);
        Assert.Equal(setlist.Songs[0].Title, songTitles[0]);
        Assert.Equal(setlist.Songs[1].Title, songTitles[1]);
    }

    [Fact]
    public void SongEnded_fires_for_each_song()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        var endedTitles = new List<string>();
        player.SongEnded += (song, _) => endedTitles.Add(song.Title);

        player.PlaySynchronous();

        Assert.Equal(2, endedTitles.Count);
    }

    [Fact]
    public void Load_while_playing_throws()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        // Start async play so state is Playing
        player.Play();
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => player.Load(TestSetlistFactory.CreateSingleSongSetlist()));
        }
        finally
        {
            player.Stop();
        }
    }
}
