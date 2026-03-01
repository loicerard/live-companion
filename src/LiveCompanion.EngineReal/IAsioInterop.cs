namespace LiveCompanion.EngineReal;

/// <summary>
/// Describes the buffer-size capabilities reported by an ASIO driver.
/// </summary>
/// <param name="MinSize">Minimum buffer size (samples).</param>
/// <param name="MaxSize">Maximum buffer size (samples).</param>
/// <param name="PreferredSize">Driver-preferred buffer size.</param>
/// <param name="Granularity">
/// Size increment between <paramref name="MinSize"/> and <paramref name="MaxSize"/>.
/// <c>-1</c> means only power-of-two sizes are valid.
/// </param>
public record AsioBufferInfo(int MinSize, int MaxSize, int PreferredSize, int Granularity);

/// <summary>
/// Thin abstraction over NAudio's ASIO driver operations.
/// Enables unit-testing <see cref="AudioEngineReal"/> on platforms without real ASIO drivers.
/// </summary>
public interface IAsioInterop : IDisposable
{
    /// <summary>Returns the names of all ASIO drivers installed on the system.</summary>
    IReadOnlyList<string> GetDriverNames();

    /// <summary>Opens the ASIO driver identified by <paramref name="driverName"/>.</summary>
    void OpenDriver(string driverName);

    /// <summary>Closes the currently open driver and releases its resources.</summary>
    void CloseDriver();

    /// <summary>Whether a driver is currently open.</summary>
    bool IsDriverOpen { get; }

    /// <summary>Returns buffer-size capabilities for the currently open driver.</summary>
    /// <exception cref="InvalidOperationException">No driver is open.</exception>
    AsioBufferInfo GetBufferInfo();

    /// <summary>Number of output channels exposed by the currently open driver.</summary>
    /// <exception cref="InvalidOperationException">No driver is open.</exception>
    int OutputChannelCount { get; }

    /// <summary>Returns the name of the output channel at <paramref name="index"/>.</summary>
    /// <exception cref="InvalidOperationException">No driver is open.</exception>
    string GetOutputChannelName(int index);
}
