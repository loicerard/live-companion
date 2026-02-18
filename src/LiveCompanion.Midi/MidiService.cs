using LiveCompanion.Core.Models;
using LiveCompanion.Midi.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveCompanion.Midi;

/// <summary>
/// Manages the lifecycle of all MIDI output and input ports.
///
/// Responsibilities:
/// - Enumerate available ports via <see cref="IMidiPortFactory"/>
/// - Open and cache ports on demand
/// - Emit <see cref="MidiFault"/> without crashing on port loss
/// - Queue pending messages and drain them on reconnection
/// - Auto-reconnect every <see cref="MidiConfiguration.ReconnectDelayMs"/> ms
/// </summary>
public sealed class MidiService : IDisposable
{
    private readonly IMidiPortFactory _factory;
    private readonly ILogger<MidiService> _logger;
    private readonly object _lock = new();

    private MidiConfiguration _config;
    private readonly Dictionary<string, PortState> _outputPorts = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public MidiService(IMidiPortFactory factory, MidiConfiguration config,
        ILogger<MidiService>? logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<MidiService>.Instance;
    }

    // ── Events ────────────────────────────────────────────────────

    /// <summary>
    /// Fired when a MIDI port becomes unavailable.
    /// The string parameter contains the port name; the exception describes the fault.
    /// Does NOT crash the application.
    /// </summary>
    public event Action<string, Exception>? MidiFault;

    /// <summary>Fired when a previously faulted port is successfully reconnected.</summary>
    public event Action<string>? PortReconnected;

    // ── Public API ────────────────────────────────────────────────

    /// <summary>Returns names of all system MIDI output ports.</summary>
    public string[] GetOutputPortNames() => _factory.GetOutputPortNames();

    /// <summary>Returns names of all system MIDI input ports.</summary>
    public string[] GetInputPortNames() => _factory.GetInputPortNames();

    /// <summary>
    /// Sends a packed MIDI message to the specified output port.
    /// If the port is unavailable the message is queued and will be replayed
    /// when the port reconnects (suitable for Program Changes and CC).
    /// </summary>
    public void Send(string portName, int packedMessage)
    {
        lock (_lock)
        {
            var state = GetOrOpenPort(portName);
            if (state.Port is not null)
            {
                TrySend(state, portName, packedMessage);
            }
            else
            {
                // Port is down — queue the message
                state.PendingMessages.Enqueue(packedMessage);
                _logger.LogDebug("MIDI port '{Port}' unavailable, queued message 0x{Msg:X6}.",
                    portName, packedMessage);
            }
        }
    }

    /// <summary>
    /// Sends a packed MIDI message immediately (fire-and-forget).
    /// If the port is unavailable the message is silently dropped.
    /// Use this for time-critical messages (MIDI Clock) that must not be queued.
    /// </summary>
    public void SendImmediate(string portName, int packedMessage)
    {
        lock (_lock)
        {
            var state = GetOrOpenPort(portName);
            if (state.Port is null) return;
            TrySend(state, portName, packedMessage);
        }
    }

    /// <summary>
    /// Returns the cached output port for a given name, opening it if needed.
    /// Returns null if the port cannot be opened.
    /// </summary>
    public IMidiOutput? GetOutputPort(string portName)
    {
        lock (_lock)
        {
            return GetOrOpenPort(portName).Port;
        }
    }

    /// <summary>Updates the configuration (e.g. after user changes settings).</summary>
    public void UpdateConfiguration(MidiConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        lock (_lock)
        {
            foreach (var state in _outputPorts.Values)
                state.Port?.Dispose();
            _outputPorts.Clear();
        }
    }

    // ── Internal helpers ──────────────────────────────────────────

    private PortState GetOrOpenPort(string portName)
    {
        if (!_outputPorts.TryGetValue(portName, out var state))
        {
            state = new PortState();
            _outputPorts[portName] = state;
        }

        if (state.Port is null && !state.IsFaulted)
        {
            try
            {
                state.Port = _factory.OpenOutput(portName);
                _logger.LogInformation("Opened MIDI output port '{Port}'.", portName);
            }
            catch (Exception ex)
            {
                state.IsFaulted = true;
                _logger.LogWarning(ex, "Failed to open MIDI output port '{Port}'.", portName);
                RaiseFaultAndScheduleReconnect(portName, ex);
            }
        }

        return state;
    }

    private void TrySend(PortState state, string portName, int packedMessage)
    {
        try
        {
            state.Port!.Send(packedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MIDI send failed on port '{Port}'.", portName);
            state.Port?.Dispose();
            state.Port = null;
            state.IsFaulted = true;
            // Re-queue the failed message
            state.PendingMessages.Enqueue(packedMessage);
            RaiseFaultAndScheduleReconnect(portName, ex);
        }
    }

    private void RaiseFaultAndScheduleReconnect(string portName, Exception ex)
    {
        MidiFault?.Invoke(portName, ex);

        if (!_disposed)
            ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        // Only one reconnect loop at a time
        if (_reconnectCts is { IsCancellationRequested: false })
            return;

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

                ReconnectFaultedPorts();
            }
        }, ct);
    }

    private void ReconnectFaultedPorts()
    {
        List<string> reconnected = [];

        lock (_lock)
        {
            foreach (var (portName, state) in _outputPorts)
            {
                if (!state.IsFaulted || state.Port is not null) continue;

                try
                {
                    state.Port = _factory.OpenOutput(portName);
                    state.IsFaulted = false;
                    _logger.LogInformation("Reconnected MIDI output port '{Port}'.", portName);

                    // Drain pending messages
                    while (state.PendingMessages.TryDequeue(out int msg))
                    {
                        try { state.Port.Send(msg); }
                        catch { /* best-effort drain */ }
                    }

                    reconnected.Add(portName);
                }
                catch
                {
                    // Still unavailable — will retry on next cycle
                }
            }
        }

        foreach (var portName in reconnected)
            PortReconnected?.Invoke(portName);
    }

    // ── Inner types ───────────────────────────────────────────────

    private sealed class PortState
    {
        public IMidiOutput? Port { get; set; }
        public bool IsFaulted { get; set; }
        public Queue<int> PendingMessages { get; } = new();
    }
}
