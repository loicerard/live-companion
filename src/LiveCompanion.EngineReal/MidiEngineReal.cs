using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du moteur MIDI.
/// Stub : toutes les méthodes lèvent <see cref="NotImplementedException"/>.
/// </summary>
public sealed class MidiEngineReal : IMidiEngine
{
    /// <inheritdoc/>
    public Task InitializeAsync(MidiConfig config)
        => throw new NotImplementedException("TODO: ouvrir les ports MIDI de sortie réels.");

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailablePorts()
        => throw new NotImplementedException("TODO: énumérer les ports MIDI OUT du système.");

    /// <inheritdoc/>
    public Task SendEventAsync(MidiEvent midiEvent)
        => throw new NotImplementedException("TODO: envoyer l'événement MIDI sur le device réel.");

    /// <inheritdoc/>
    public Task ShutdownAsync()
        => throw new NotImplementedException("TODO: fermer les ports MIDI et libérer les ressources.");
}
