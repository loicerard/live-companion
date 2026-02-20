using CommunityToolkit.Mvvm.ComponentModel;
using LiveCompanion.Core.Models;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// Represents a single song row in the setlist view.
/// </summary>
public sealed partial class SongItemViewModel : ObservableObject
{
    private readonly Song _song;
    private readonly int _ppqn;

    public SongItemViewModel(Song song, int index, int ppqn)
    {
        _song = song;
        _ppqn = ppqn;
        Index = index;
    }

    public int    Index  { get; }
    public string Number => $"{Index + 1}";
    public string Title  => _song.Title;
    public string Artist => _song.Artist;

    /// <summary>BPM of the first section (or "—" if no section event).</summary>
    public string Bpm
    {
        get
        {
            var first = _song.Events
                .OfType<SectionChangeEvent>()
                .OrderBy(e => e.Tick)
                .FirstOrDefault();
            return first is not null ? first.Bpm.ToString() : "—";
        }
    }

    /// <summary>Estimated duration in mm:ss from DurationTicks and first section BPM.</summary>
    public string Duration
    {
        get
        {
            var first = _song.Events
                .OfType<SectionChangeEvent>()
                .OrderBy(e => e.Tick)
                .FirstOrDefault();
            if (first is null || _ppqn == 0) return "—";

            double secondsPerTick = 60.0 / (first.Bpm * _ppqn);
            double totalSeconds = _song.DurationTicks * secondsPerTick;
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)ts.TotalMinutes:D1}:{ts.Seconds:D2}";
        }
    }

    [ObservableProperty]
    private bool _isCurrent;

    public Song Song => _song;
}
