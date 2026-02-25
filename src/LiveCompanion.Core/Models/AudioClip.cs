namespace LiveCompanion.Core.Models;

/// <summary>
/// Sample audio associé à un morceau, déclenché à une position précise.
/// </summary>
public class AudioClip
{
    /// <summary>Identifiant unique du clip.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom affiché du clip.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin vers le fichier audio sur le disque.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Nom du bus de sortie audio cible (ex : "Main", "Click", "FX").</summary>
    public string BusName { get; set; } = "Main";

    /// <summary>Volume de lecture (0.0 = silence, 1.0 = plein volume).</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Durée du fade-in en secondes.</summary>
    public double FadeInSeconds { get; set; }

    /// <summary>Durée du fade-out en secondes.</summary>
    public double FadeOutSeconds { get; set; }

    /// <summary>Mode de synchronisation sur la grille rythmique.</summary>
    public SyncMode SyncMode { get; set; } = SyncMode.Free;

    /// <summary>Position de déclenchement dans la timeline.</summary>
    public TimelinePosition Position { get; set; } = TimelinePosition.Zero;
}
