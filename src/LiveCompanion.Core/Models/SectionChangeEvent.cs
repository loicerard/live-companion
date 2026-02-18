namespace LiveCompanion.Core.Models;

public class SectionChangeEvent : SongEvent
{
    public string SectionName { get; set; } = string.Empty;
    public int Bpm { get; set; }
    public TimeSignature TimeSignature { get; set; } = TimeSignature.Common;
    public List<MidiPreset> Presets { get; set; } = [];
}
