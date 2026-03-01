using FluentAssertions;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Tests.Models;

public class PlaylistTests
{
    [Fact]
    public void NewPlaylist_ShouldHaveDefaults()
    {
        var playlist = new Playlist();

        playlist.Id.Should().NotBe(Guid.Empty);
        playlist.Name.Should().Be("Playlist");
        playlist.SongIds.Should().BeEmpty();
    }

    [Fact]
    public void Playlist_CanAddSongIds()
    {
        var playlist = new Playlist { Name = "Set 1" };
        var songId = Guid.NewGuid();

        playlist.SongIds.Add(songId);

        playlist.SongIds.Should().ContainSingle().Which.Should().Be(songId);
    }

    [Fact]
    public void Playlist_MaintainsOrder()
    {
        var playlist = new Playlist();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        playlist.SongIds.Add(id1);
        playlist.SongIds.Add(id2);
        playlist.SongIds.Add(id3);

        playlist.SongIds.Should().ContainInOrder(id1, id2, id3);
    }
}
