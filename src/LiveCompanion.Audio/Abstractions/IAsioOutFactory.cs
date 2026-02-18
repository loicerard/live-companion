namespace LiveCompanion.Audio.Abstractions;

/// <summary>
/// Factory for creating <see cref="IAsioOut"/> instances.
/// Allows injection of fakes for testing.
/// </summary>
public interface IAsioOutFactory
{
    /// <summary>
    /// Returns the names of all ASIO drivers installed on the system.
    /// </summary>
    string[] GetDriverNames();

    /// <summary>
    /// Creates an <see cref="IAsioOut"/> instance for the specified driver.
    /// </summary>
    IAsioOut Create(string driverName);
}
