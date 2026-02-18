using NAudio.Wave;

namespace LiveCompanion.Audio.Abstractions;

/// <summary>
/// Production factory that creates real ASIO outputs via NAudio.
/// </summary>
public sealed class NAudioAsioOutFactory : IAsioOutFactory
{
    public string[] GetDriverNames() => AsioOut.GetDriverNames();

    public IAsioOut Create(string driverName) => new NAudioAsioOut(driverName);
}
