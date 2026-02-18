using LiveCompanion.Audio.Tests.Fakes;

namespace LiveCompanion.Audio.Tests;

public class AsioServiceTests
{
    private static AudioConfiguration CreateConfig(string driverName = "FakeASIO Driver") => new()
    {
        AsioDriverName = driverName,
        BufferSize = 256,
        SampleRate = 44100,
        MetronomeChannelOffset = 0,
        SampleChannelOffset = 2
    };

    [Fact]
    public void GetAvailableDrivers_returns_factory_drivers()
    {
        var drivers = new[] { "Driver A", "Driver B" };
        var factory = new FakeAsioOutFactory(drivers);
        using var service = new AsioService(factory, CreateConfig());

        var result = service.GetAvailableDrivers();

        Assert.Equal(drivers, result);
    }

    [Fact]
    public void Initialize_creates_asio_device()
    {
        var factory = new FakeAsioOutFactory();
        var config = CreateConfig();
        using var service = new AsioService(factory, config);

        service.Initialize();

        Assert.NotNull(factory.LastCreated);
        Assert.Equal("FakeASIO Driver", factory.LastCreated!.DriverName);
    }

    [Fact]
    public void Initialize_exposes_output_channel_count()
    {
        var factory = new FakeAsioOutFactory(outputChannels: 8);
        using var service = new AsioService(factory, CreateConfig());

        service.Initialize();

        Assert.Equal(8, service.OutputChannelCount);
    }

    [Fact]
    public void Initialize_without_driver_name_throws()
    {
        var factory = new FakeAsioOutFactory();
        var config = new AudioConfiguration { AsioDriverName = null };
        using var service = new AsioService(factory, config);

        Assert.Throws<InvalidOperationException>(() => service.Initialize());
    }

    [Fact]
    public void Initialize_with_failing_driver_throws()
    {
        var factory = new FakeAsioOutFactory();
        factory.CreateException = new Exception("No ASIO device found");
        using var service = new AsioService(factory, CreateConfig());

        var ex = Assert.Throws<InvalidOperationException>(() => service.Initialize());
        Assert.Contains("Failed to initialize ASIO driver", ex.Message);
    }

    [Fact]
    public void Play_starts_playback()
    {
        var factory = new FakeAsioOutFactory();
        using var service = new AsioService(factory, CreateConfig());
        service.Initialize();

        service.Play();

        Assert.True(service.IsPlaying);
        Assert.Equal(1, factory.LastCreated!.PlayCallCount);
    }

    [Fact]
    public void Play_without_initialize_throws()
    {
        var factory = new FakeAsioOutFactory();
        using var service = new AsioService(factory, CreateConfig());

        Assert.Throws<InvalidOperationException>(() => service.Play());
    }

    [Fact]
    public void Stop_stops_playback()
    {
        var factory = new FakeAsioOutFactory();
        using var service = new AsioService(factory, CreateConfig());
        service.Initialize();
        service.Play();

        service.Stop();

        Assert.False(service.IsPlaying);
    }

    [Fact]
    public void Fault_fires_AudioFault_event()
    {
        var factory = new FakeAsioOutFactory();
        var config = CreateConfig();
        config.AutoReconnect = false; // Don't reconnect for this test
        using var service = new AsioService(factory, config);
        service.Initialize();
        service.Play();

        string? faultMessage = null;
        service.AudioFault += msg => faultMessage = msg;

        factory.LastCreated!.SimulateFault(new Exception("Device disconnected"));

        Assert.Equal("Device disconnected", faultMessage);
    }

    [Fact]
    public void Dispose_cleans_up_asio()
    {
        var factory = new FakeAsioOutFactory();
        var service = new AsioService(factory, CreateConfig());
        service.Initialize();
        service.Play();

        service.Dispose();

        Assert.False(service.IsPlaying);
    }

    [Fact]
    public void Reinitialize_replaces_previous_device()
    {
        var factory = new FakeAsioOutFactory();
        using var service = new AsioService(factory, CreateConfig());

        service.Initialize();
        var first = factory.LastCreated;

        service.Initialize();
        var second = factory.LastCreated;

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Normal_stop_does_not_fire_AudioFault()
    {
        var factory = new FakeAsioOutFactory();
        using var service = new AsioService(factory, CreateConfig());
        service.Initialize();
        service.Play();

        bool faultFired = false;
        service.AudioFault += _ => faultFired = true;

        factory.LastCreated!.SimulateNormalStop();

        Assert.False(faultFired);
    }
}
