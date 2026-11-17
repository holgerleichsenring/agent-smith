using System.Text.Json;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-17-ec48a: a value a run keeps in its pipeline read back either as it was set or as a checkpoint's JSON
/// round trip returns it — extracted from 4860b's message cursor, now shared with the texts it handed over and the
/// count of spec revision reviews.
/// </summary>
public static class CheckpointedValues
{
    /// <summary>The int under <paramref name="key"/>, or <paramref name="absent"/>.</summary>
    public static int Int(PipelineContext pipeline, string key, int absent)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<int>(key, out var index)) return index;
        if (pipeline.TryGet<long>(key, out var wide)) return (int)wide;
        return pipeline.TryGet<JsonElement>(key, out var json) && json.ValueKind == JsonValueKind.Number
            ? json.GetInt32() : absent;
    }

    /// <summary>The strings under <paramref name="key"/>, or none.</summary>
    public static IReadOnlyList<string> Strings(PipelineContext pipeline, string key)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<IReadOnlyList<string>>(key, out var list) && list is not null) return list;
        return pipeline.TryGet<JsonElement>(key, out var json) && json.ValueKind == JsonValueKind.Array
            ? [.. json.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];
    }
}
