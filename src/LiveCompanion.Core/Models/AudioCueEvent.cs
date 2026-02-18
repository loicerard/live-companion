namespace LiveCompanion.Core.Models;

public class AudioCueEvent : SongEvent
{
    public string SampleFileName { get; set; } = string.Empty;

    /// <summary>
    /// Playback gain in dB (0 = unity).
    /// </summary>
    public double GainDb { get; set; }
}
