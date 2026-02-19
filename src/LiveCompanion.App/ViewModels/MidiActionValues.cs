using LiveCompanion.Midi;

namespace LiveCompanion.App.ViewModels;

/// <summary>Helper that exposes all MidiAction values for ComboBox binding.</summary>
public static class MidiActionValues
{
    public static IReadOnlyList<MidiAction> All { get; } =
        Enum.GetValues<MidiAction>();
}
