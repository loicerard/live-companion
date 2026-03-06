using System.Text.Json.Serialization;

namespace LiveCompanion.Core.Models;

/// <summary>
/// Sample audio associé à un morceau, déclenché à une position précise.
/// Chaque clip peut être routé vers plusieurs bus via <see cref="Sends"/>.
/// </summary>
public class AudioClip
{
    /// <summary>Identifiant unique du clip.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom affiché du clip.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin vers le fichier audio sur le disque.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Envois audio vers les bus de sortie. Chaque send définit un bus et un volume indépendant.
    /// Par défaut, un seul send vers "Main" à volume 1.0.
    /// </summary>
    public List<BusSend> Sends { get; init; } = [new BusSend()];

    /// <summary>Durée du fade-in en secondes.</summary>
    public double FadeInSeconds { get; set; }

    /// <summary>Durée du fade-out en secondes.</summary>
    public double FadeOutSeconds { get; set; }

    /// <summary>Mode de synchronisation sur la grille rythmique.</summary>
    public SyncMode SyncMode { get; set; } = SyncMode.Free;

    /// <summary>Position de déclenchement dans la timeline.</summary>
    public TimelinePosition Position { get; set; } = TimelinePosition.Zero;

    // ------------------------------------------------------------------ //
    // Rétro-compatibilité JSON (ancien format BusName/Volume)
    // ------------------------------------------------------------------ //

    /// <summary>Ancien champ — utilisé uniquement pour la migration JSON.</summary>
    [JsonInclude]
    [JsonPropertyName("BusName")]
    public string? LegacyBusName { get; set; }

    /// <summary>Ancien champ — utilisé uniquement pour la migration JSON.</summary>
    [JsonInclude]
    [JsonPropertyName("Volume")]
    public double? LegacyVolume { get; set; }

    /// <summary>
    /// Migre les anciens champs BusName/Volume vers Sends si nécessaire.
    /// Doit être appelé après désérialisation.
    /// </summary>
    public void MigrateLegacyFields()
    {
        if (LegacyBusName is not null)
        {
            // Si Sends est le défaut (un seul "Main" à 1.0), le remplacer par les valeurs legacy
            if (Sends.Count == 1 && Sends[0].BusName == "Main" && Sends[0].Volume == 1.0)
            {
                Sends[0] = new BusSend
                {
                    BusName = LegacyBusName,
                    Volume = LegacyVolume ?? 1.0
                };
            }

            LegacyBusName = null;
            LegacyVolume = null;
        }
    }
}
