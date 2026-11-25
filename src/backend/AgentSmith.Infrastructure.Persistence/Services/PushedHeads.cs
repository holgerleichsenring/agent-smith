using System.Text.Json;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-25-098f: a run's pushed heads as stored on Runs.PushedHeadsJson — a JSON array of shas, each once.</summary>
public static class PushedHeads
{
    /// <summary>The stored list with <paramref name="sha"/> appended; a replayed or repeated push leaves it as it was.</summary>
    public static string With(string? stored, string sha)
    {
        var heads = stored is null ? [] : JsonSerializer.Deserialize<List<string>>(stored) ?? [];
        if (!heads.Contains(sha, StringComparer.Ordinal)) heads.Add(sha);
        return JsonSerializer.Serialize(heads);
    }
}
