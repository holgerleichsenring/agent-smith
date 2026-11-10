namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-14a3b: read_file with a path that names no source, on a host holding several. Conversation 43 lost six
/// reads to the routing refusal although each path existed in exactly one source. The path is tried in every source:
/// one that holds it answers, with its name first so the next call can carry the prefix; several are named; none keeps
/// the refusal. Aliases of one sandbox (a key and its repository name) count once.
/// </summary>
internal static class UnprefixedRead
{
    internal static async Task<string> TryAsync(
        IReadOnlyDictionary<string, SandboxStepRunner> runners, string? path, int? startLine, int? lineCount,
        bool withLineNumbers, string refusal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || runners.Count < 2) return refusal;
        var hits = new List<(string Source, string Content)>();
        foreach (var group in runners.Where(r => r.Key.Length > 0).GroupBy(r => r.Value))
        {
            var content = await group.Key.ReadAsync(path, startLine, lineCount, withLineNumbers, ct);
            if (!content.StartsWith("Error", StringComparison.Ordinal))
                hits.Add((group.Select(r => r.Key).OrderBy(k => k.Length).First(), content));
        }
        return hits.Count switch
        {
            0 => refusal,
            1 => $"[read from {hits[0].Source}/{path} — prefix the path with '{hits[0].Source}/' next time]\n{hits[0].Content}",
            _ => $"Error: '{path}' exists in {string.Join(", ", hits.Select(h => h.Source))}. "
                 + $"Prefix it with the one you mean, e.g. '{hits[0].Source}/{path}'.",
        };
    }
}
