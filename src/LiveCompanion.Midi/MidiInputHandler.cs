using LiveCompanion.Midi.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveCompanion.Midi;

/// <summary>
/// Listens on a MIDI input port and translates incoming MIDI messages into
/// player actions (Stop, Pause, NextSong, PreviousSong, TriggerCue).
///
/// Typical use case: the Roland SSPD footswitch sends CC or Note messages
/// to the PC, which the handler maps to live control actions.
///
/// The mapping table is defined in <see cref="MidiConfiguration.InputMappings"/>.
/// </summary>
public sealed class MidiInputHandler : IDisposable
{
    private readonly IMidiPortFactory _factory;
    private readonly MidiConfiguration _config;
    private readonly ILogger<MidiInputHandler> _logger;

    private IMidiInput? _inputPort;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public MidiInputHandler(IMidiPortFactory factory, MidiConfiguration config,
        ILogger<MidiInputHandler>? logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<MidiInputHandler>.Instance;
    }

    // ── Events ────────────────────────────────────────────────────

    /// <summary>
    /// Fired when an incoming MIDI message matches a mapping rule.
    /// Subscribers should implement the actual player action (e.g. call SetlistPlayer.Stop()).
    /// </summary>
    public event Action<MidiAction>? ActionTriggered;

    /// <summary>
    /// Fired when the MIDI input port encounters an error.
    /// Does NOT crash the application.
    /// </summary>
    public event Action<string, Exception>? MidiFault;

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Opens the configured MIDI input port and starts listening.
    /// If no port is configured (<see cref="MidiConfiguration.MidiInputPortName"/> is null/empty),
    /// this is a no-op.
    /// </summary>
    public void Open()
    {
        if (string.IsNullOrWhiteSpace(_config.MidiInputPortName))
        {
            _logger.LogInformation("No MIDI input port configured. MidiInputHandler is passive.");
            return;
        }

        OpenPort(_config.MidiInputPortName);
    }

    /// <summary>Stops listening and releases the input port.</summary>
    public void Close()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;

        ClosePort();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }

    // ── Internal: exposed for testing ────────────────────────────

    /// <summary>
    /// Processes a received MIDI message against the mapping table.
    /// Exposed internally so tests can inject messages without real MIDI hardware.
    /// </summary>
    internal void ProcessMessage(byte status, byte data1, byte data2)
    {
        foreach (var mapping in _config.InputMappings)
        {
            if (!mapping.Matches(status, data1, data2)) continue;

            _logger.LogDebug(
                "MIDI IN: 0x{Status:X2} 0x{D1:X2} 0x{D2:X2} → {Action}",
                status, data1, data2, mapping.Action);

            ActionTriggered?.Invoke(mapping.Action);
            // First match wins
            return;
        }
    }

    // ── Private helpers ───────────────────────────────────────────

    private void OpenPort(string portName)
    {
        ClosePort();

        try
        {
            _inputPort = _factory.OpenInput(portName);
            _inputPort.MessageReceived += OnMessageReceived;
            _inputPort.Start();
            _logger.LogInformation("MIDI input port '{Port}' opened and listening.", portName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open MIDI input port '{Port}'.", portName);
            MidiFault?.Invoke(portName, ex);

            if (!_disposed)
                ScheduleReconnect(portName);
        }
    }

    private void ClosePort()
    {
        if (_inputPort is null) return;
        _inputPort.MessageReceived -= OnMessageReceived;
        try { _inputPort.Stop(); } catch { /* ignore during cleanup */ }
        _inputPort.Dispose();
        _inputPort = null;
    }

    private void OnMessageReceived(object? sender, MidiMessageReceivedEventArgs e)
    {
        try
        {
            ProcessMessage(e.Status, e.Data1, e.Data2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception processing MIDI input message.");
        }
    }

    private void ScheduleReconnect(string portName)
    {
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;
        var delayMs = _config.ReconnectDelayMs;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    OpenPort(portName);
                    return; // success
                }
                catch
                {
                    // Still unavailable — retry on next cycle
                }
            }
        }, ct);
    }
}
