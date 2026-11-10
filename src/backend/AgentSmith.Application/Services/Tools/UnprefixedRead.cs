namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-14a3b: read_file with a path that names no source, on a design turn's host holding several. Conversation
/// 43 lost six reads to the refusal although each path existed in exactly one source. The path is tried in the sources
/// already open — 2026-11-10-85e2: never opening one (a lazy repository clones, an upload starts a container) and never
/// a template. One holder answers with its name first; several are named; none keeps the refusal, which lists the
/// sources. Aliases of one sandbox count once.
/// </summary>
internal static class UnprefixedRead
{
    internal static async Task<(string? Resolved, string Text)> TryAsync(
        IReadOnlyDictionary<string, SandboxStepRunner> runners, string? path, int? startLine, int? lineCount,
        bool withLineNumbers, string refusal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || runners.Count < 2) return (null, refusal);
        var hits = new List<(string Source, string Content)>();
        var open = runners.Where(r => r.Key.Length > 0 && r.Value.IsOpen
            && !r.Key.StartsWith(Specs.TemplateScopeName.Prefix, StringComparison.Ordinal));
        foreach (var group in open.GroupBy(r => r.Value))
        {
            var content = await group.Key.ReadAsync(path, startLine, lineCount, withLineNumbers, ct);
            if (!content.StartsWith("Error", StringComparison.Ordinal))
                hits.Add((group.Select(r => r.Key).OrderBy(k => k.Length).First(), content));
        }
        return hits.Count switch
        {
            0 => (null, refusal),
            1 => ($"{hits[0].Source}/{path}",
                $"[read from {hits[0].Source}/{path} — prefix the path with '{hits[0].Source}/' next time]\n{hits[0].Content}"),
            _ => (null, $"Error: '{path}' exists in {string.Join(", ", hits.Select(h => h.Source))}. "
                 + $"Prefix it with the one you mean, e.g. '{hits[0].Source}/{path}'."),
        };
    }
}
