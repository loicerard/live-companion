using System.IO;

namespace LiveCompanion.App.Services;

/// <summary>
/// Centralises config file paths under %APPDATA%\LiveCompanion.
/// </summary>
public static class ConfigPaths
{
    private static readonly string BaseDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "LiveCompanion");

    public static string AudioConfigFile  => Path.Combine(BaseDir, "audio_config.json");
    public static string MidiConfigFile   => Path.Combine(BaseDir, "midi_config.json");
    public static string LastSetlistFile  => Path.Combine(BaseDir, "last_setlist.txt");

    /// <summary>
    /// Root directory for audio sample files (Bug 2).
    /// <see cref="LiveCompanion.Core.Models.AudioCueEvent.SampleFileName"/> is stored as a
    /// filename relative to this directory so setlists remain portable.
    /// </summary>
    public static string SamplesDirectory => Path.Combine(BaseDir, "samples");

    public static void EnsureBaseDirectoryExists()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(SamplesDirectory);
    }
}
