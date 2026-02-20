namespace LiveCompanion.App.Services;

/// <summary>
/// Centralises all file-system paths used by the application.
/// All sample paths are relative to <see cref="SamplesDirectory"/> so that
/// setlists stay portable when moved between machines.
/// </summary>
public static class AppPathService
{
    /// <summary>%AppData%\LiveCompanion\</summary>
    public static string AppDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LiveCompanion");

    /// <summary>%AppData%\LiveCompanion\samples\</summary>
    public static string SamplesDirectory =>
        Path.Combine(AppDataDirectory, "samples");

    /// <summary>%AppData%\LiveCompanion\setlists\</summary>
    public static string SetlistsDirectory =>
        Path.Combine(AppDataDirectory, "setlists");

    /// <summary>%AppData%\LiveCompanion\audio.json</summary>
    public static string AudioConfigPath =>
        Path.Combine(AppDataDirectory, "audio.json");

    /// <summary>
    /// Resolves a sample file name (relative) to its full path inside
    /// <see cref="SamplesDirectory"/>.
    /// </summary>
    public static string ResolveSamplePath(string relativeFileName) =>
        Path.Combine(SamplesDirectory, relativeFileName);
}
