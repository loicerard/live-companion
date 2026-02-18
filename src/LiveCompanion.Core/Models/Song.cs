namespace LiveCompanion.Core.Models;

public class Song
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Total length of the song expressed in ticks.
    /// </summary>
    public long DurationTicks { get; set; }

    /// <summary>
    /// Ordered list of events on the timeline.
    /// </summary>
    public List<SongEvent> Events { get; set; } = [];
}
