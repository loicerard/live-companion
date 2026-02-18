namespace LiveCompanion.Audio.Tests;

public class AudioConfigurationTests
{
    [Fact]
    public void Default_values_are_correct()
    {
        var config = new AudioConfiguration();

        Assert.Null(config.AsioDriverName);
        Assert.Equal(AudioConfiguration.DefaultBufferSize, config.BufferSize);
        Assert.Equal(AudioConfiguration.DefaultSampleRate, config.SampleRate);
        Assert.Equal(AudioConfiguration.DefaultMetronomeChannel, config.MetronomeChannelOffset);
        Assert.Equal(AudioConfiguration.DefaultSampleChannel, config.SampleChannelOffset);
        Assert.Equal(AudioConfiguration.DefaultMasterVolume, config.MetronomeMasterVolume);
        Assert.Equal(AudioConfiguration.DefaultStrongBeatVolume, config.StrongBeatVolume);
        Assert.Equal(AudioConfiguration.DefaultWeakBeatVolume, config.WeakBeatVolume);
        Assert.True(config.AutoReconnect);
        Assert.Equal(2000, config.ReconnectDelayMs);
    }

    [Fact]
    public void Roundtrip_json_preserves_all_fields()
    {
        var original = new AudioConfiguration
        {
            AsioDriverName = "Focusrite USB ASIO",
            BufferSize = 512,
            SampleRate = 48000,
            MetronomeChannelOffset = 2,
            SampleChannelOffset = 0,
            MetronomeMasterVolume = 0.8f,
            StrongBeatVolume = 0.9f,
            WeakBeatVolume = 0.5f,
            AutoReconnect = false,
            ReconnectDelayMs = 5000
        };

        var json = original.ToJson();
        var restored = AudioConfiguration.FromJson(json);

        Assert.Equal(original.AsioDriverName, restored.AsioDriverName);
        Assert.Equal(original.BufferSize, restored.BufferSize);
        Assert.Equal(original.SampleRate, restored.SampleRate);
        Assert.Equal(original.MetronomeChannelOffset, restored.MetronomeChannelOffset);
        Assert.Equal(original.SampleChannelOffset, restored.SampleChannelOffset);
        Assert.Equal(original.MetronomeMasterVolume, restored.MetronomeMasterVolume);
        Assert.Equal(original.StrongBeatVolume, restored.StrongBeatVolume);
        Assert.Equal(original.WeakBeatVolume, restored.WeakBeatVolume);
        Assert.Equal(original.AutoReconnect, restored.AutoReconnect);
        Assert.Equal(original.ReconnectDelayMs, restored.ReconnectDelayMs);
    }

    [Fact]
    public void Json_uses_camelCase_naming()
    {
        var config = new AudioConfiguration
        {
            AsioDriverName = "TestDriver",
            BufferSize = 128
        };

        var json = config.ToJson();

        Assert.Contains("\"asioDriverName\"", json);
        Assert.Contains("\"bufferSize\"", json);
        Assert.Contains("\"sampleRate\"", json);
        Assert.Contains("\"metronomeChannelOffset\"", json);
        Assert.Contains("\"sampleChannelOffset\"", json);
    }

    [Fact]
    public void Null_driver_name_is_excluded_from_json()
    {
        var config = new AudioConfiguration(); // AsioDriverName is null by default
        var json = config.ToJson();

        Assert.DoesNotContain("asioDriverName", json);
    }

    [Fact]
    public async Task File_roundtrip_preserves_all_fields()
    {
        var original = new AudioConfiguration
        {
            AsioDriverName = "Test ASIO",
            BufferSize = 256,
            SampleRate = 44100,
            MetronomeChannelOffset = 0,
            SampleChannelOffset = 2,
            MetronomeMasterVolume = 1.0f,
            StrongBeatVolume = 1.0f,
            WeakBeatVolume = 0.7f,
            AutoReconnect = true,
            ReconnectDelayMs = 3000
        };

        var path = Path.Combine(Path.GetTempPath(), $"test-audio-config-{Guid.NewGuid()}.json");
        try
        {
            await AudioConfiguration.SaveAsync(original, path);
            var restored = await AudioConfiguration.LoadAsync(path);

            Assert.Equal(original.AsioDriverName, restored.AsioDriverName);
            Assert.Equal(original.BufferSize, restored.BufferSize);
            Assert.Equal(original.SampleRate, restored.SampleRate);
            Assert.Equal(original.MetronomeChannelOffset, restored.MetronomeChannelOffset);
            Assert.Equal(original.SampleChannelOffset, restored.SampleChannelOffset);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void FromJson_throws_on_invalid_json()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            AudioConfiguration.FromJson("not valid json"));
    }
}
