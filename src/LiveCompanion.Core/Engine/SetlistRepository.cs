using System.Text.Json;
using System.Text.Json.Serialization;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Engine;

/// <summary>
/// Persists and loads <see cref="Setlist"/> instances as JSON files.
/// Uses System.Text.Json polymorphic serialization for <see cref="SongEvent"/> subtypes.
/// </summary>
public static class SetlistRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task SaveAsync(Setlist setlist, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, setlist, JsonOptions).ConfigureAwait(false);
    }

    public static async Task<Setlist> LoadAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Setlist>(stream, JsonOptions).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Failed to deserialize setlist from {filePath}");
    }

    public static string Serialize(Setlist setlist)
    {
        return JsonSerializer.Serialize(setlist, JsonOptions);
    }

    public static Setlist Deserialize(string json)
    {
        return JsonSerializer.Deserialize<Setlist>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize setlist from JSON string.");
    }
}
