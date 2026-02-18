namespace LiveCompanion.Core.Models;

public class MidiPreset
{
    public DeviceTarget Device { get; set; }
    public int Channel { get; set; }
    public int ProgramChange { get; set; }
    public List<ControlChange> ControlChanges { get; set; } = [];
}
