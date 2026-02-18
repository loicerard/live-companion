using System.Text.Json.Serialization;

namespace LiveCompanion.Core.Models;

[JsonDerivedType(typeof(SectionChangeEvent), "SectionChange")]
[JsonDerivedType(typeof(AudioCueEvent), "AudioCue")]
public abstract class SongEvent
{
    /// <summary>
    /// Tick position (PPQN-based) at which this event fires.
    /// </summary>
    public long Tick { get; set; }
}
