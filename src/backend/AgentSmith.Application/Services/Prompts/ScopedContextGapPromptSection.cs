namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-09-08-1830: the pin sent back into the derivation conversation when a
/// deliverable cut covers fewer contexts than the scope call named. The model sees
/// its own first cut and the looks it took; this names the gap and the two ways to
/// close it — carry the context in a phase, or list it as discarded with the reason.
/// </summary>
public static class ScopedContextGapPromptSection
{
    public static string Render(IReadOnlyList<string> gap, IReadOnlyList<string> carried)
    {
        ArgumentNullException.ThrowIfNull(gap);
        ArgumentNullException.ThrowIfNull(carried);
        var missing = string.Join(", ", gap.Select(g => $"`{g}`"));
        var covered = carried.Count == 0
            ? "no context at all"
            : string.Join(", ", carried.Select(c => $"`{c}`")) + " only";
        return $"""
            ## Your cut does not cover every context the scope call named
            The scope call named {missing}; the cut carries {covered}. Either carry {missing} in a
            phase and name it in that phase's "contexts", or list it under "discarded_contexts"
            with the reason it is outside this ticket's work. A context that is neither carried
            nor discarded with a reason is work silently dropped.
            Respond again with ONLY the corrected JSON object.
            """;
    }
}
