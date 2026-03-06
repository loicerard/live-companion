using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Validation;

/// <summary>
/// Validation statique des modèles métier.
/// </summary>
public static class ModelValidator
{
    private static readonly int[] ValidDenominators = [1, 2, 4, 8, 16, 32];

    /// <summary>Valide la structure d'un <see cref="Song"/> (pas de vérification fichier).</summary>
    public static ValidationResult ValidateSong(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(song.Title))
            result.AddError("Title", "Le titre du morceau ne peut pas être vide.");

        for (int i = 0; i < song.Sections.Count; i++)
        {
            var s = song.Sections[i];
            var prefix = $"Sections[{i}]";

            if (s.Tempo < 20 || s.Tempo > 300)
                result.AddError($"{prefix}.Tempo",
                    $"Le tempo de la section '{s.Name}' doit être entre 20 et 300 BPM (actuel : {s.Tempo}).");

            if (s.BarCount < 1)
                result.AddError($"{prefix}.BarCount",
                    $"La section '{s.Name}' doit avoir au moins 1 mesure (actuel : {s.BarCount}).");

            if (s.TimeSignature.Numerator < 1)
                result.AddError($"{prefix}.TimeSignature.Numerator",
                    $"Le numérateur de la signature de '{s.Name}' doit être ≥ 1.");

            if (!ValidDenominators.Contains(s.TimeSignature.Denominator))
                result.AddError($"{prefix}.TimeSignature.Denominator",
                    $"Le dénominateur de la signature de '{s.Name}' doit être 1, 2, 4, 8, 16 ou 32 (actuel : {s.TimeSignature.Denominator}).");
        }

        for (int i = 0; i < song.AudioClips.Count; i++)
        {
            var clip = song.AudioClips[i];
            var prefix = $"AudioClips[{i}]";

            if (string.IsNullOrWhiteSpace(clip.FilePath))
                result.AddError($"{prefix}.FilePath",
                    $"Le chemin audio du clip '{clip.Name}' ne peut pas être vide.");

            if (clip.Volume < 0.0 || clip.Volume > 1.0)
                result.AddError($"{prefix}.Volume",
                    $"Le volume du clip '{clip.Name}' doit être entre 0.0 et 1.0 (actuel : {clip.Volume}).");

            if (clip.FadeInSeconds < 0)
                result.AddError($"{prefix}.FadeInSeconds",
                    $"Le fade-in du clip '{clip.Name}' ne peut pas être négatif.");

            if (clip.FadeOutSeconds < 0)
                result.AddError($"{prefix}.FadeOutSeconds",
                    $"Le fade-out du clip '{clip.Name}' ne peut pas être négatif.");
        }

        for (int i = 0; i < song.MidiEvents.Count; i++)
        {
            var e = song.MidiEvents[i];
            var prefix = $"MidiEvents[{i}]";

            if (e.Channel < 1 || e.Channel > 16)
                result.AddError($"{prefix}.Channel",
                    $"Le canal MIDI doit être entre 1 et 16 (actuel : {e.Channel}).");

            if (e.Data1 < 0 || e.Data1 > 127)
                result.AddError($"{prefix}.Data1",
                    $"Data1 MIDI doit être entre 0 et 127 (actuel : {e.Data1}).");

            if (e.Data2 < 0 || e.Data2 > 127)
                result.AddError($"{prefix}.Data2",
                    $"Data2 MIDI doit être entre 0 et 127 (actuel : {e.Data2}).");
        }

        return result;
    }

    /// <summary>Vérifie l'existence des fichiers audio référencés par un <see cref="Song"/>.</summary>
    public static ValidationResult ValidateSongFiles(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var result = new ValidationResult();

        for (int i = 0; i < song.AudioClips.Count; i++)
        {
            var clip = song.AudioClips[i];
            if (!string.IsNullOrWhiteSpace(clip.FilePath) && !File.Exists(clip.FilePath))
                result.AddWarning($"AudioClips[{i}].FilePath",
                    $"Fichier audio introuvable pour le clip '{clip.Name}' : '{clip.FilePath}'.");
        }

        if (!string.IsNullOrWhiteSpace(song.ClickTrackPath) && !File.Exists(song.ClickTrackPath))
            result.AddWarning("ClickTrackPath",
                $"Fichier de piste de clic introuvable : '{song.ClickTrackPath}'.");

        return result;
    }

    /// <summary>Vérifie la cohérence des playlists par rapport aux morceaux existants.</summary>
    public static ValidationResult ValidatePlaylists(
        IReadOnlyList<Playlist> playlists,
        IReadOnlyList<Song> songs)
    {
        ArgumentNullException.ThrowIfNull(playlists);
        ArgumentNullException.ThrowIfNull(songs);

        var result = new ValidationResult();
        var songIds = new HashSet<Guid>(songs.Select(s => s.Id));

        for (int i = 0; i < playlists.Count; i++)
        {
            var pl = playlists[i];
            var prefix = $"Playlists[{i}]";

            if (string.IsNullOrWhiteSpace(pl.Name))
                result.AddError($"{prefix}.Name", "Le nom de la playlist ne peut pas être vide.");

            for (int j = 0; j < pl.SongIds.Count; j++)
            {
                if (!songIds.Contains(pl.SongIds[j]))
                    result.AddWarning($"{prefix}.SongIds[{j}]",
                        $"La playlist '{pl.Name}' référence un morceau inexistant : {pl.SongIds[j]}.");
            }
        }

        return result;
    }
}
