using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Tests;

public class SerializationTests
{
    [Fact]
    public void Roundtrip_preserves_setlist_structure()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Ppqn, restored.Ppqn);
        Assert.Equal(original.Songs.Count, restored.Songs.Count);
    }

    [Fact]
    public void Roundtrip_preserves_song_properties()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        for (int i = 0; i < original.Songs.Count; i++)
        {
            Assert.Equal(original.Songs[i].Title, restored.Songs[i].Title);
            Assert.Equal(original.Songs[i].Artist, restored.Songs[i].Artist);
            Assert.Equal(original.Songs[i].DurationTicks, restored.Songs[i].DurationTicks);
            Assert.Equal(original.Songs[i].Events.Count, restored.Songs[i].Events.Count);
        }
    }

    [Fact]
    public void Roundtrip_preserves_polymorphic_event_types()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        var originalEvents = original.Songs[0].Events;
        var restoredEvents = restored.Songs[0].Events;

        // Song 1 has: SectionChange, AudioCue, SectionChange
        Assert.IsType<SectionChangeEvent>(restoredEvents[0]);
        Assert.IsType<AudioCueEvent>(restoredEvents[1]);
        Assert.IsType<SectionChangeEvent>(restoredEvents[2]);
    }

    [Fact]
    public void Roundtrip_preserves_section_change_details()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        var originalSection = (SectionChangeEvent)original.Songs[0].Events[0];
        var restoredSection = (SectionChangeEvent)restored.Songs[0].Events[0];

        Assert.Equal(originalSection.SectionName, restoredSection.SectionName);
        Assert.Equal(originalSection.Bpm, restoredSection.Bpm);
        Assert.Equal(originalSection.TimeSignature, restoredSection.TimeSignature);
        Assert.Equal(originalSection.Tick, restoredSection.Tick);
    }

    [Fact]
    public void Roundtrip_preserves_midi_presets()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        var originalSection = (SectionChangeEvent)original.Songs[0].Events[0];
        var restoredSection = (SectionChangeEvent)restored.Songs[0].Events[0];

        Assert.Equal(originalSection.Presets.Count, restoredSection.Presets.Count);
        for (int i = 0; i < originalSection.Presets.Count; i++)
        {
            Assert.Equal(originalSection.Presets[i].Device, restoredSection.Presets[i].Device);
            Assert.Equal(originalSection.Presets[i].Channel, restoredSection.Presets[i].Channel);
            Assert.Equal(originalSection.Presets[i].ProgramChange, restoredSection.Presets[i].ProgramChange);
        }
    }

    [Fact]
    public void Roundtrip_preserves_control_changes()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        var originalSection = (SectionChangeEvent)original.Songs[0].Events[0];
        var restoredSection = (SectionChangeEvent)restored.Songs[0].Events[0];

        var originalCCs = originalSection.Presets[0].ControlChanges;
        var restoredCCs = restoredSection.Presets[0].ControlChanges;

        Assert.Equal(originalCCs.Count, restoredCCs.Count);
        Assert.Equal(originalCCs[0].Controller, restoredCCs[0].Controller);
        Assert.Equal(originalCCs[0].Value, restoredCCs[0].Value);
    }

    [Fact]
    public void Roundtrip_preserves_audio_cue()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(original);
        var restored = SetlistRepository.Deserialize(json);

        var originalCue = (AudioCueEvent)original.Songs[0].Events[1];
        var restoredCue = (AudioCueEvent)restored.Songs[0].Events[1];

        Assert.Equal(originalCue.SampleFileName, restoredCue.SampleFileName);
        Assert.Equal(originalCue.GainDb, restoredCue.GainDb);
        Assert.Equal(originalCue.Tick, restoredCue.Tick);
    }

    [Fact]
    public async Task File_roundtrip_preserves_data()
    {
        var original = TestSetlistFactory.CreateTwoSongSetlist();
        var path = Path.Combine(Path.GetTempPath(), $"test-setlist-{Guid.NewGuid()}.json");

        try
        {
            await SetlistRepository.SaveAsync(original, path);
            var restored = await SetlistRepository.LoadAsync(path);

            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Songs.Count, restored.Songs.Count);
            Assert.Equal(original.Songs[0].Events.Count, restored.Songs[0].Events.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Json_contains_type_discriminator()
    {
        var setlist = TestSetlistFactory.CreateTwoSongSetlist();
        var json = SetlistRepository.Serialize(setlist);

        Assert.Contains("$type", json);
        Assert.Contains("SectionChange", json);
        Assert.Contains("AudioCue", json);
    }
}
