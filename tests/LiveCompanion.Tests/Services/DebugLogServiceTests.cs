using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;
using Xunit;

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

    [Fact]
    public void Log_ShouldIncludeTimestamp()
    {
        var log = new DebugLogService();
        var before = DateTime.UtcNow;

        log.Log(LogLevel.Info, LogSource.Core, "Timestamped");

        var entry = log.GetEntries().Single();
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Theory]
    [InlineData(LogSource.Core)]
    [InlineData(LogSource.UI)]
    [InlineData(LogSource.EngineMock)]
    [InlineData(LogSource.EngineReal)]
    public void Log_AllSources_ShouldBeAccepted(LogSource source)
    {
        var log = new DebugLogService();

        log.Log(LogLevel.Info, source, "Test");

        log.GetEntries().Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [Fact]
    public void GetEntries_ShouldReturnCopy()
    {
        var log = new DebugLogService();
        log.Log(LogLevel.Info, LogSource.Core, "Entry");

        var entries1 = log.GetEntries();
        var entries2 = log.GetEntries();

        entries1.Should().NotBeSameAs(entries2);
    }

    [Fact]
    public void Log_CircularBuffer_ShouldKeepNewest()
    {
        var log = new DebugLogService();

        for (int i = 0; i < DebugLogService.MaxEntries + 50; i++)
            log.Log(LogLevel.Debug, LogSource.Core, $"Entry {i}");

        var entries = log.GetEntries();
        entries.Should().HaveCount(DebugLogService.MaxEntries);
        entries.Last().Message.Should().Be($"Entry {DebugLogService.MaxEntries + 49}");
    }

    [Fact]
    public void EntryAdded_ShouldContainCorrectSource()
    {
        var log = new DebugLogService();
        LogEntry? received = null;

        log.EntryAdded += entry => received = entry;
        log.Log(LogLevel.Warning, LogSource.EngineReal, "Warning msg");

        received.Should().NotBeNull();
        received!.Source.Should().Be(LogSource.EngineReal);
        received.Level.Should().Be(LogLevel.Warning);
    }
}
