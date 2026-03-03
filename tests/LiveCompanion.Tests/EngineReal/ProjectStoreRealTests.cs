using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

public class ProjectStoreRealTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly ILogService _log = new DebugLogService();

    private ProjectStoreReal CreateStore() =>
        new(_log, Path.Combine(_tmpDir, "settings.json"));

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    // ------------------------------------------------------------------ //
    // Songs — CreateNew / GetAll
    // ------------------------------------------------------------------ //

    [Fact]
    public void CreateNew_ShouldReturnSongWithSections()
    {
        var store = CreateStore();
        var song = store.CreateNew("Mon morceau");

        song.Title.Should().Be("Mon morceau");
        song.Sections.Should().HaveCount(3);
        song.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void GetAll_AfterCreateNew_ShouldContainSong()
    {
        var store = CreateStore();
        var song = store.CreateNew("Test");

        store.GetAll().Should().ContainSingle().Which.Id.Should().Be(song.Id);
    }

    [Fact]
    public void GetAll_Empty_ShouldReturnEmpty()
    {
        var store = CreateStore();
        store.GetAll().Should().BeEmpty();
    }

    // ------------------------------------------------------------------ //
    // Songs — Update
    // ------------------------------------------------------------------ //

    [Fact]
    public void Update_ShouldPersistChanges()
    {
        var store = CreateStore();
        var song = store.CreateNew("Original");

        song.Title = "Modified";
        store.Update(song);

        store.GetAll().Should().ContainSingle().Which.Title.Should().Be("Modified");
    }

    [Fact]
    public void Update_ShouldSetLastModified()
    {
        var store = CreateStore();
        var song = store.CreateNew("Test");
        var before = DateTime.UtcNow;

        store.Update(song);

        song.LastModified.Should().BeOnOrAfter(before);
    }

    // ------------------------------------------------------------------ //
    // Songs — Delete
    // ------------------------------------------------------------------ //

    [Fact]
    public void Delete_ExistingSong_ShouldReturnTrue()
    {
        var store = CreateStore();
        var song = store.CreateNew("ToDelete");

        store.Delete(song.Id).Should().BeTrue();
        store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Delete_NonExistingSong_ShouldReturnFalse()
    {
        var store = CreateStore();
        store.Delete(Guid.NewGuid()).Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Songs — SaveAsync / LoadAsync (persistance réelle sur disque)
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SaveAndLoad_ShouldReturnDeepCopy()
    {
        var store = CreateStore();
        var path = Path.Combine(_tmpDir, "song.json");
        var song = new Song
        {
            Title = "Saved Song",
            Sections = { new Section { Name = "Intro", Tempo = 140, BarCount = 8 } },
        };

        await store.SaveAsync(song, path);
        var loaded = await store.LoadAsync(path);

        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Saved Song");
        loaded.Sections.Should().HaveCount(1);

        // Deep copy — modifier loaded ne doit pas affecter le fichier
        loaded.Title = "Changed";
        var reloaded = await store.LoadAsync(path);
        reloaded!.Title.Should().Be("Saved Song");
    }

    [Fact]
    public async Task Load_NonExistentPath_ShouldReturnNull()
    {
        var store = CreateStore();
        var result = await store.LoadAsync(Path.Combine(_tmpDir, "does-not-exist.json"));
        result.Should().BeNull();
    }

    [Fact]
    public async Task Save_ShouldCreateFileOnDisk()
    {
        var store = CreateStore();
        var path = Path.Combine(_tmpDir, "sub", "song.json");
        var song = store.CreateNew("Persistance");

        await store.SaveAsync(song, path);

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task SaveThenLoadInNewInstance_ShouldRestoreSong()
    {
        var path = Path.Combine(_tmpDir, "song.json");
        var song = new Song { Title = "Cross-instance" };

        // Première instance : sauvegarde
        await CreateStore().SaveAsync(song, path);

        // Deuxième instance : chargement
        var loaded = await CreateStore().LoadAsync(path);

        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Cross-instance");
        loaded.Id.Should().Be(song.Id);
    }

    // ------------------------------------------------------------------ //
    // Playlists
    // ------------------------------------------------------------------ //

    [Fact]
    public void CreatePlaylist_ShouldBeRetrievable()
    {
        var store = CreateStore();
        var playlist = store.CreatePlaylist("Concert 1");

        playlist.Name.Should().Be("Concert 1");
        store.GetAllPlaylists().Should().ContainSingle().Which.Id.Should().Be(playlist.Id);
    }

    [Fact]
    public void UpdatePlaylist_ShouldPersistChanges()
    {
        var store = CreateStore();
        var playlist = store.CreatePlaylist("Set");

        playlist.SongIds.Add(Guid.NewGuid());
        store.UpdatePlaylist(playlist);

        store.GetAllPlaylists().First().SongIds.Should().HaveCount(1);
    }

    [Fact]
    public void DeletePlaylist_ShouldRemove()
    {
        var store = CreateStore();
        var playlist = store.CreatePlaylist("ToDelete");

        store.DeletePlaylist(playlist.Id).Should().BeTrue();
        store.GetAllPlaylists().Should().BeEmpty();
    }

    [Fact]
    public void DeletePlaylist_NonExistent_ShouldReturnFalse()
    {
        var store = CreateStore();
        store.DeletePlaylist(Guid.NewGuid()).Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Settings
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetSettings_Default_ShouldReturnNonNull()
    {
        var store = CreateStore();
        var settings = store.GetSettings();

        settings.Should().NotBeNull();
        settings.AudioConfig.Should().BeNull();
        settings.MidiConfig.Should().BeNull();
    }

    [Fact]
    public void SaveSettings_ShouldPersistInMemory()
    {
        var store = CreateStore();
        var settings = new AppSettings
        {
            AudioConfig = new AudioConfig { DriverName = "ASIO4ALL", BufferSize = 512 },
            MidiConfig = new MidiConfig { SelectedPorts = { "Port1" } },
        };

        store.SaveSettings(settings);

        var loaded = store.GetSettings();
        loaded.AudioConfig!.DriverName.Should().Be("ASIO4ALL");
        loaded.MidiConfig!.SelectedPorts.Should().ContainSingle("Port1");
    }

    [Fact]
    public void SaveSettings_ShouldCreateFileOnDisk()
    {
        var store = CreateStore();
        store.SaveSettings(new AppSettings
        {
            AudioConfig = new AudioConfig { DriverName = "ASIO4ALL", BufferSize = 256 },
        });

        File.Exists(Path.Combine(_tmpDir, "settings.json")).Should().BeTrue();
    }

    [Fact]
    public void SaveSettingsThenNewInstance_ShouldRestoreSettings()
    {
        // Première instance : sauvegarde
        var store1 = CreateStore();
        store1.SaveSettings(new AppSettings
        {
            AudioConfig = new AudioConfig { DriverName = "ASIO4ALL", BufferSize = 256 },
            MidiConfig = new MidiConfig { SelectedPorts = { "PortA" } },
        });

        // Deuxième instance : les settings sont chargés depuis le fichier dans le constructeur
        var store2 = CreateStore();
        var loaded = store2.GetSettings();

        loaded.AudioConfig!.DriverName.Should().Be("ASIO4ALL");
        loaded.AudioConfig.BufferSize.Should().Be(256);
        loaded.MidiConfig!.SelectedPorts.Should().ContainSingle("PortA");
    }
}
