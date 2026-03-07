using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using NAudio.Midi;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle de l'import MIDI via NAudio.
/// Extrait les marqueurs (sections), tempos et signatures rythmiques d'un fichier MIDI.
/// </summary>
public class MidiImportServiceReal : IMidiImportService
{
    public MidiImportResult Import(string midiFilePath, int countdownBars = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(midiFilePath);
        if (!File.Exists(midiFilePath))
            throw new FileNotFoundException("Fichier MIDI introuvable.", midiFilePath);

        var midiFile = new MidiFile(midiFilePath, false);

        // ------------------------------------------------------------------ //
        // 1. Collecter les méta-événements de la piste 0 (conductor track)
        // ------------------------------------------------------------------ //
        var tempoChanges = new List<(long AbsoluteTime, double Bpm)>();
        var timeSignatureChanges = new List<(long AbsoluteTime, int Numerator, int Denominator)>();
        var markers = new List<(long AbsoluteTime, string Name)>();

        // On parcourt toutes les pistes pour être sûr de capturer les méta-événements
        // (certains DAW les mettent sur la piste 0, d'autres les répartissent)
        for (int track = 0; track < midiFile.Tracks; track++)
        {
            foreach (var midiEvent in midiFile.Events[track])
            {
                if (midiEvent is TempoEvent tempoEvent)
                {
                    tempoChanges.Add((tempoEvent.AbsoluteTime, tempoEvent.Tempo));
                }
                else if (midiEvent is TimeSignatureEvent tsEvent)
                {
                    int numerator = tsEvent.Numerator;
                    int denominator = (int)Math.Pow(2, tsEvent.Denominator);
                    timeSignatureChanges.Add((tsEvent.AbsoluteTime, numerator, denominator));
                }
                else if (midiEvent is TextEvent textEvent)
                {
                    // Les marqueurs MIDI (MetaEventType.Marker = 6) définissent les sections
                    if (textEvent.MetaEventType == MetaEventType.Marker)
                    {
                        markers.Add((textEvent.AbsoluteTime, textEvent.Text));
                    }
                }
            }
        }

        // Trier par position
        tempoChanges.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));
        timeSignatureChanges.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));
        markers.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));

        int ticksPerQuarter = midiFile.DeltaTicksPerQuarterNote;

        // ------------------------------------------------------------------ //
        // 2. Déterminer la durée totale du morceau
        // ------------------------------------------------------------------ //
        long totalTicks = 0;
        for (int track = 0; track < midiFile.Tracks; track++)
        {
            var events = midiFile.Events[track];
            if (events.Count > 0)
            {
                long lastTick = events[events.Count - 1].AbsoluteTime;
                if (lastTick > totalTicks)
                    totalTicks = lastTick;
            }
        }

        // ------------------------------------------------------------------ //
        // 3. Valeurs par défaut si absentes
        // ------------------------------------------------------------------ //
        if (tempoChanges.Count == 0)
            tempoChanges.Add((0, 120.0));

        if (timeSignatureChanges.Count == 0)
            timeSignatureChanges.Add((0, 4, 4));

        // ------------------------------------------------------------------ //
        // 4. Construire les sections
        // ------------------------------------------------------------------ //
        var sections = new List<Section>();

        if (markers.Count > 0)
        {
            // Construire les sections à partir des marqueurs
            sections = BuildSectionsFromMarkers(
                markers, tempoChanges, timeSignatureChanges,
                ticksPerQuarter, totalTicks);
        }
        else
        {
            // Pas de marqueurs : créer des sections basées sur les changements
            // de tempo/signature, ou une seule section si tout est uniforme
            sections = BuildSectionsFromChanges(
                tempoChanges, timeSignatureChanges,
                ticksPerQuarter, totalTicks);
        }

        // ------------------------------------------------------------------ //
        // 5. Ajouter la section de décompte en premier
        // ------------------------------------------------------------------ //
        if (countdownBars > 0 && sections.Count > 0)
        {
            var firstSection = sections[0];
            var countdown = new Section
            {
                Name = "Décompte",
                Tempo = firstSection.Tempo,
                TimeSignature = firstSection.TimeSignature,
                BarCount = countdownBars,
                Order = 0,
            };

            // Décaler les ordres existants
            foreach (var s in sections)
                s.Order++;

            sections.Insert(0, countdown);
        }

        string title = Path.GetFileNameWithoutExtension(midiFilePath);

        return new MidiImportResult
        {
            Title = title,
            Sections = sections,
        };
    }

    /// <summary>
    /// Construit les sections à partir des marqueurs MIDI.
    /// </summary>
    private static List<Section> BuildSectionsFromMarkers(
        List<(long AbsoluteTime, string Name)> markers,
        List<(long AbsoluteTime, double Bpm)> tempoChanges,
        List<(long AbsoluteTime, int Numerator, int Denominator)> tsChanges,
        int ticksPerQuarter,
        long totalTicks)
    {
        var sections = new List<Section>();

        for (int i = 0; i < markers.Count; i++)
        {
            long startTick = markers[i].AbsoluteTime;
            long endTick = i + 1 < markers.Count ? markers[i + 1].AbsoluteTime : totalTicks;
            string name = string.IsNullOrWhiteSpace(markers[i].Name)
                ? $"Section {i + 1}"
                : markers[i].Name;

            double bpm = GetValueAtTick(tempoChanges, startTick);
            var (num, den) = GetTimeSignatureAtTick(tsChanges, startTick);
            int barCount = CalculateBarCount(startTick, endTick, ticksPerQuarter, num, den);

            if (barCount < 1) barCount = 1;

            sections.Add(new Section
            {
                Name = name,
                Tempo = ClampTempo(bpm),
                TimeSignature = new TimeSignature(num, den),
                BarCount = barCount,
                Order = i,
            });
        }

        return sections;
    }

    /// <summary>
    /// Construit les sections à partir des changements de tempo/signature
    /// quand il n'y a pas de marqueurs.
    /// </summary>
    private static List<Section> BuildSectionsFromChanges(
        List<(long AbsoluteTime, double Bpm)> tempoChanges,
        List<(long AbsoluteTime, int Numerator, int Denominator)> tsChanges,
        int ticksPerQuarter,
        long totalTicks)
    {
        // Fusionner les points de changement
        var changePoints = new SortedSet<long>();
        foreach (var tc in tempoChanges) changePoints.Add(tc.AbsoluteTime);
        foreach (var ts in tsChanges) changePoints.Add(ts.AbsoluteTime);

        var sortedPoints = changePoints.ToList();
        var sections = new List<Section>();

        for (int i = 0; i < sortedPoints.Count; i++)
        {
            long startTick = sortedPoints[i];
            long endTick = i + 1 < sortedPoints.Count ? sortedPoints[i + 1] : totalTicks;

            double bpm = GetValueAtTick(tempoChanges, startTick);
            var (num, den) = GetTimeSignatureAtTick(tsChanges, startTick);
            int barCount = CalculateBarCount(startTick, endTick, ticksPerQuarter, num, den);

            if (barCount < 1) barCount = 1;

            sections.Add(new Section
            {
                Name = $"Section {i + 1}",
                Tempo = ClampTempo(bpm),
                TimeSignature = new TimeSignature(num, den),
                BarCount = barCount,
                Order = i,
            });
        }

        return sections;
    }

    /// <summary>Retourne le tempo actif à un tick donné.</summary>
    private static double GetValueAtTick(List<(long AbsoluteTime, double Bpm)> changes, long tick)
    {
        double value = 120.0;
        foreach (var change in changes)
        {
            if (change.AbsoluteTime <= tick)
                value = change.Bpm;
            else
                break;
        }
        return value;
    }

    /// <summary>Retourne la signature rythmique active à un tick donné.</summary>
    private static (int Numerator, int Denominator) GetTimeSignatureAtTick(
        List<(long AbsoluteTime, int Numerator, int Denominator)> changes, long tick)
    {
        int num = 4, den = 4;
        foreach (var change in changes)
        {
            if (change.AbsoluteTime <= tick)
            {
                num = change.Numerator;
                den = change.Denominator;
            }
            else
                break;
        }
        return (num, den);
    }

    /// <summary>Calcule le nombre de mesures entre deux positions en ticks.</summary>
    private static int CalculateBarCount(long startTick, long endTick, int ticksPerQuarter, int numerator, int denominator)
    {
        if (endTick <= startTick) return 1;

        // Durée d'un temps en ticks : ticksPerQuarter * (4 / denominator)
        // Durée d'une mesure en ticks : ticksPerBeat * numerator
        double ticksPerBeat = ticksPerQuarter * (4.0 / denominator);
        double ticksPerBar = ticksPerBeat * numerator;

        double bars = (endTick - startTick) / ticksPerBar;

        // Arrondir au plus proche (tolérance pour les petits écarts MIDI)
        return Math.Max(1, (int)Math.Round(bars));
    }

    /// <summary>Clamp le BPM dans la plage valide [20, 300].</summary>
    private static double ClampTempo(double bpm)
        => Math.Clamp(Math.Round(bpm, 2), 20.0, 300.0);
}
