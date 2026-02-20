using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LiveCompanion.Audio;

/// <summary>
/// Enumerates installed ASIO driver names by reading the Windows registry directly.
///
/// WHY this class exists instead of using <c>AsioOut.GetDriverNames()</c> from NAudio:
///   NAudio reads <c>HKLM\SOFTWARE\ASIO</c> using <c>RegistryView.Default</c>.
///   On a 64-bit process that resolves to the NATIVE (64-bit) hive. Most ASIO drivers
///   (ASIO4ALL, Steinberg Generic Low Latency, hardware vendors) are installed by 32-bit
///   setup programs and therefore land in <c>HKLM\SOFTWARE\Wow6432Node\ASIO</c> — the
///   32-bit registry view — which is INVISIBLE to a 64-bit process using the default view.
///
///   This class opens BOTH <see cref="RegistryView.Registry64"/> and
///   <see cref="RegistryView.Registry32"/> explicitly, merges the results, and returns
///   a deduplicated, sorted list regardless of which view the driver installer used.
/// </summary>
internal static class AsioDriverEnumerator
{
    private const string AsioKeyPath = @"SOFTWARE\ASIO";

    /// <summary>
    /// Returns all ASIO driver names found across the 64-bit and 32-bit registry views.
    /// All steps are traced via <paramref name="logger"/> (Debug level) and
    /// <see cref="System.Diagnostics.Debug.WriteLine"/> for visibility in the VS Output window.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static string[] GetDriverNames(ILogger? logger = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Trace("GetDriverNames: scanning HKLM\\{0} in both 64-bit and 32-bit registry views.",
              AsioKeyPath);
        logger?.LogDebug(
            "AsioDriverEnumerator: scanning HKLM\\{Path} in 64-bit and 32-bit views.", AsioKeyPath);

        ReadView(RegistryView.Registry64, "64-bit", names, logger);
        ReadView(RegistryView.Registry32, "32-bit", names, logger);

        var result = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = result.Length > 0 ? string.Join(", ", result) : "(none)";

        Trace("GetDriverNames: total {0} unique driver(s): [{1}]", result.Length, summary);
        logger?.LogInformation(
            "AsioDriverEnumerator: {Count} unique driver(s) found: [{Names}]",
            result.Length, summary);

        return result;
    }

    // ── Private helpers ────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    private static void ReadView(RegistryView view, string label,
                                  HashSet<string> names, ILogger? logger)
    {
        Trace("[{0}] Opening HKLM ({1}) ...", label, view);

        try
        {
            using var hklm   = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var asioKey = hklm.OpenSubKey(AsioKeyPath);

            if (asioKey is null)
            {
                Trace("[{0}] Key HKLM\\{1} not found in this view (key absent or access denied).",
                      label, AsioKeyPath);
                logger?.LogDebug(
                    "AsioDriverEnumerator [{Label}]: HKLM\\{Path} not found in {View} view.",
                    label, AsioKeyPath, view);
                return;
            }

            var subKeys = asioKey.GetSubKeyNames();
            Trace("[{0}] Key HKLM\\{1} found — {2} sub-key(s).", label, AsioKeyPath, subKeys.Length);
            logger?.LogDebug(
                "AsioDriverEnumerator [{Label}]: {Count} sub-key(s) under HKLM\\{Path}.",
                label, subKeys.Length, AsioKeyPath);

            foreach (var name in subKeys)
            {
                bool isNew = names.Add(name);
                Trace("[{0}]   Driver '{1}' [{2}]", label, name, isNew ? "new" : "duplicate");
                logger?.LogDebug(
                    "AsioDriverEnumerator [{Label}]:   '{Name}' [{Status}]",
                    label, name, isNew ? "new" : "duplicate");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace("[{0}] ERROR — access denied reading HKLM\\{1}: {2}", label, AsioKeyPath, ex.Message);
            logger?.LogWarning(ex,
                "AsioDriverEnumerator [{Label}]: access denied reading HKLM\\{Path}.",
                label, AsioKeyPath);
        }
        catch (Exception ex)
        {
            Trace("[{0}] ERROR — {1}: {2}", label, ex.GetType().Name, ex.Message);
            logger?.LogError(ex,
                "AsioDriverEnumerator [{Label}]: unexpected error reading HKLM\\{Path}.",
                label, AsioKeyPath);
        }
    }

    private static void Trace(string format, params object?[] args)
        => Debug.WriteLine("[AsioDriverEnumerator] " + string.Format(format, args));
}
