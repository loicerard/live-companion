using LiveCompanion.Audio.Abstractions;

namespace LiveCompanion.Audio.Tests.Fakes;

/// <summary>
/// Fake ASIO factory that returns pre-configured <see cref="FakeAsioOut"/> instances.
/// </summary>
internal sealed class FakeAsioOutFactory : IAsioOutFactory
{
    private readonly string[] _driverNames;
    private readonly int _outputChannels;

    /// <summary>
    /// The last <see cref="FakeAsioOut"/> instance created by this factory.
    /// </summary>
    public FakeAsioOut? LastCreated { get; private set; }

    /// <summary>
    /// If set, <see cref="Create"/> will throw this exception.
    /// Simulates driver initialization failure.
    /// </summary>
    public Exception? CreateException { get; set; }

    public FakeAsioOutFactory(string[]? driverNames = null, int outputChannels = 4)
    {
        _driverNames = driverNames ?? ["FakeASIO Driver"];
        _outputChannels = outputChannels;
    }

    public string[] GetDriverNames() => _driverNames;

    public IAsioOut Create(string driverName)
    {
        if (CreateException is not null)
            throw CreateException;

        var fake = new FakeAsioOut(driverName, _outputChannels);
        LastCreated = fake;
        return fake;
    }
}
