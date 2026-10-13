namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-10-13-50c7: turns the deserialised <c>sources:</c> list into what the discovery carries —
/// trimmed, blanks dropped, null when nothing is left. Whether an entry is rooted or escapes is
/// the structure ownership's reading, the same one it applies to the workdir.
/// </summary>
internal static class ContextYamlSourcesReader
{
    public static IReadOnlyList<string>? Read(List<string>? sources)
    {
        var kept = (sources ?? []).Select(s => s?.Trim()).OfType<string>().Where(s => s.Length > 0).ToList();
        return kept.Count == 0 ? null : kept;
    }
}
