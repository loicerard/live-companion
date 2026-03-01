using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;

namespace LiveCompanion.Tests.Services;

public class DebugLogServiceTests
{
    [Fact]
    public void Log_ShouldAddEntry()
    {
        var log = new DebugLogService();
        log.Log(LogLevel.Info, LogSource.Core, "Test message");

        log.GetEntries().Should().ContainSingle();
        log.GetEntries()[0].Message.Should().Be("Test message");
        log.GetEntries()[0].Level.Should().Be(LogLevel.Info);
        log.GetEntries()[0].Source.Should().Be(LogSource.Core);
    }

    [Fact]
    public void Log_ShouldFireEntryAdded()
    {
        var log = new DebugLogService();
        LogEntry? received = null;

        log.EntryAdded += entry => received = entry;
        log.Log(LogLevel.Debug, LogSource.EngineMock, "Hello");

        received.Should().NotBeNull();
        received!.Message.Should().Be("Hello");
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var log = new DebugLogService();
        log.Log(LogLevel.Info, LogSource.Core, "1");
        log.Log(LogLevel.Info, LogSource.Core, "2");

        log.Clear();

        log.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void Log_ShouldRespectMaxEntries()
    {
        var log = new DebugLogService();

        for (int i = 0; i < DebugLogService.MaxEntries + 100; i++)
            log.Log(LogLevel.Debug, LogSource.Core, $"Entry {i}");

        log.GetEntries().Should().HaveCount(DebugLogService.MaxEntries);

        // Oldest entries should have been removed
        log.GetEntries().First().Message.Should().Be("Entry 100");
    }

    [Fact]
    public void ExtensionMethods_ShouldSetCorrectLevel()
    {
        var log = new DebugLogService();

        log.Debug(LogSource.Core, "debug");
        log.Info(LogSource.UI, "info");
        log.Warn(LogSource.EngineMock, "warn");
        log.Error(LogSource.EngineReal, "error");

        var entries = log.GetEntries();
        entries.Should().HaveCount(4);
        entries[0].Level.Should().Be(LogLevel.Debug);
        entries[1].Level.Should().Be(LogLevel.Info);
        entries[2].Level.Should().Be(LogLevel.Warning);
        entries[3].Level.Should().Be(LogLevel.Error);
    }
}
