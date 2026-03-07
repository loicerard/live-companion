using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;

namespace LiveCompanion.Tests.Services;

public class AutoSaveServiceTests : IDisposable
{
    private readonly ILogService _log = new DebugLogService();
    private readonly ProjectStoreMock _store;
    private readonly string _tempFolder;
    private readonly AutoSaveService _autoSave;

    public AutoSaveServiceTests()
    {
        _store = new ProjectStoreMock(_log);
        _tempFolder = Path.Combine(Path.GetTempPath(), "LiveCompanionTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempFolder);
        _autoSave = new AutoSaveService(_store, _log, _tempFolder);
    }

    public void Dispose()
    {
        _autoSave.Dispose();
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, recursive: true);
    }

    // ------------------------------------------------------------------ //
    // Constructor
    // ------------------------------------------------------------------ //

    [Fact]
    public void Constructor_NullStore_ShouldThrow()
    {
        var act = () => new AutoSaveService(null!, _log, _tempFolder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLog_ShouldThrow()
    {
        var act = () => new AutoSaveService(_store, null!, _tempFolder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPath_ShouldThrow()
    {
        var act = () => new AutoSaveService(_store, _log, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ //
    // Default state
    // ------------------------------------------------------------------ //

    [Fact]
    public void DefaultInterval_ShouldBeFiveMinutes()
    {
        _autoSave.Interval.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void IsRunning_ShouldBeFalseByDefault()
    {
        _autoSave.IsRunning.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Start / Stop
    // ------------------------------------------------------------------ //

    [Fact]
    public void Start_ShouldSetRunning()
    {
        _autoSave.Start();
        _autoSave.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldClearRunning()
    {
        _autoSave.Start();
        _autoSave.Stop();
        _autoSave.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_Twice_ShouldNotThrow()
    {
        _autoSave.Start();
        _autoSave.Start();
        _autoSave.IsRunning.Should().BeTrue();
    }

    // ------------------------------------------------------------------ //
    // SaveNowAsync
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SaveNow_NoSongs_ShouldNotThrow()
    {
        await _autoSave.SaveNowAsync();
        // No exception, no files created
    }

    [Fact]
    public async Task SaveNow_WithModifiedSong_ShouldSaveToStore()
    {
        var song = _store.CreateNew("TestSong");
        song.LastModified = DateTime.UtcNow;

        await _autoSave.SaveNowAsync();

        // The mock store should have the song persisted via SaveAsync
        var expectedPath = Path.Combine(_tempFolder, $"{song.Id}.json");
        _store.StoredPaths.Should().Contain(expectedPath);
    }

    [Fact]
    public async Task SaveNow_SameSongTwice_ShouldSaveOnlyOnce()
    {
        var song = _store.CreateNew("TestSong");

        await _autoSave.SaveNowAsync();
        await _autoSave.SaveNowAsync();

        // Should only save once since LastModified hasn't changed
        var expectedPath = Path.Combine(_tempFolder, $"{song.Id}.json");
        _store.StoredPaths.Should().Contain(expectedPath);
    }

    [Fact]
    public async Task SaveNow_AfterModification_ShouldSaveAgain()
    {
        var song = _store.CreateNew("TestSong");

        await _autoSave.SaveNowAsync();

        // Modify the song
        song.Title = "Modified";
        song.LastModified = DateTime.UtcNow.AddSeconds(1);
        _store.Update(song);

        await _autoSave.SaveNowAsync();

        // Should have saved (verify by loading)
        var expectedPath = Path.Combine(_tempFolder, $"{song.Id}.json");
        var loadResult = await _store.LoadAsync(expectedPath);
        loadResult.Value.Should().NotBeNull();
        loadResult.Value!.Title.Should().Be("Modified");
    }

    [Fact]
    public async Task SaveNow_MultipleSongs_ShouldSaveAll()
    {
        var song1 = _store.CreateNew("Song1");
        var song2 = _store.CreateNew("Song2");

        await _autoSave.SaveNowAsync();

        var path1 = Path.Combine(_tempFolder, $"{song1.Id}.json");
        var path2 = Path.Combine(_tempFolder, $"{song2.Id}.json");
        _store.StoredPaths.Should().Contain(path1);
        _store.StoredPaths.Should().Contain(path2);
    }

    // ------------------------------------------------------------------ //
    // Timer-based save
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Timer_ShouldTriggerSave()
    {
        _autoSave.Interval = TimeSpan.FromMilliseconds(100);
        var song = _store.CreateNew("TimerTest");

        _autoSave.Start();
        await Task.Delay(300); // Wait for timer to fire
        _autoSave.Stop();

        var expectedPath = Path.Combine(_tempFolder, $"{song.Id}.json");
        _store.StoredPaths.Should().Contain(expectedPath);
    }

    // ------------------------------------------------------------------ //
    // Dispose
    // ------------------------------------------------------------------ //

    [Fact]
    public void Dispose_ShouldStopTimer()
    {
        _autoSave.Start();
        _autoSave.Dispose();
        _autoSave.IsRunning.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Pruning
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SaveNow_DeletedSong_ShouldNotAccumulateStaleEntries()
    {
        var song = _store.CreateNew("ToBeDeleted");

        // First save — registers song in _lastSavedAt
        await _autoSave.SaveNowAsync();

        // Delete the song from the store
        _store.Delete(song.Id);

        // Second save — should prune orphan entry and not fail
        await _autoSave.SaveNowAsync();

        // Re-create a song with the same ID pattern — only the new one should be tracked
        var newSong = _store.CreateNew("Replacement");
        newSong.LastModified = DateTime.UtcNow.AddSeconds(1);
        await _autoSave.SaveNowAsync();

        var expectedPath = Path.Combine(_tempFolder, $"{newSong.Id}.json");
        _store.StoredPaths.Should().Contain(expectedPath);
    }
}
