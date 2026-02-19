namespace LiveCompanion.App.Services;

/// <summary>
/// Centralises config file paths under %APPDATA%\LiveCompanion.
/// </summary>
public static class ConfigPaths
{
    private static readonly string BaseDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "LiveCompanion");

    public static string AudioConfigFile => Path.Combine(BaseDir, "audio_config.json");
    public static string MidiConfigFile  => Path.Combine(BaseDir, "midi_config.json");

    public static void EnsureBaseDirectoryExists() => Directory.CreateDirectory(BaseDir);
}
