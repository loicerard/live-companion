using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace LiveCompanion.Audio.Abstractions;

/// <summary>
/// Production factory that creates real ASIO outputs via NAudio.
/// </summary>
public sealed class NAudioAsioOutFactory : IAsioOutFactory
{
    private readonly ILogger<NAudioAsioOutFactory>? _logger;

    /// <param name="logger">
    /// Optional logger. When provided, detailed registry-scan steps are emitted at Debug level.
    /// All steps are also always written to <see cref="System.Diagnostics.Debug"/> for the VS
    /// Output window regardless of whether a logger is supplied.
    /// </param>
    public NAudioAsioOutFactory(ILogger<NAudioAsioOutFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the names of all ASIO drivers installed on the system.
    /// Reads <c>HKLM\SOFTWARE\ASIO</c> from BOTH the 64-bit and 32-bit registry views so that
    /// drivers registered by 32-bit installers (ASIO4ALL, Steinberg, etc.) are found when the
    /// host process runs as x64 — something <c>AsioOut.GetDriverNames()</c> does not do.
    /// </summary>
    public string[] GetDriverNames()
    {
        _logger?.LogDebug("NAudioAsioOutFactory: GetDriverNames() called.");

        if (!OperatingSystem.IsWindows())
        {
            _logger?.LogWarning("NAudioAsioOutFactory: not running on Windows — returning empty list.");
            return [];
        }

        var names = AsioDriverEnumerator.GetDriverNames(_logger);

        _logger?.LogInformation(
            "NAudioAsioOutFactory: GetDriverNames() returning {Count} driver(s).", names.Length);

        return names;
    }

    public IAsioOut Create(string driverName) => new NAudioAsioOut(driverName);
}
