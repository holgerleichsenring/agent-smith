using System.Text.Json;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the files of a validated PoC revision, path (after the ignore list and the top-folder strip) to
/// sha256, CRC-32 and size. The different-app guard compares the next archive's central directory against it on path,
/// CRC-32 and size; the sha256 names the content for what later reads the source repository.
/// </summary>
public sealed class PocManifest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public PocManifest(IReadOnlyDictionary<string, PocManifestEntry> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = new SortedDictionary<string, PocManifestEntry>(
            files.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, PocManifestEntry> Files { get; }

    /// <summary>The manifest as stored: a JSON object of path to {sha256, crc32, size}, paths in ordinal order.</summary>
    public string ToJson() => JsonSerializer.Serialize(Files, Json);

    /// <summary>A manifest <see cref="ToJson"/> wrote.</summary>
    public static PocManifest FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var files = JsonSerializer.Deserialize<Dictionary<string, PocManifestEntry>>(json, Json)
            ?? throw new JsonException("A PoC manifest is a JSON object of path to file; it was null.");
        return new PocManifest(files);
    }
}
