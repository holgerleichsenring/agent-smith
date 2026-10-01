namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-01-f5c3b: the prose parts of a phase spec a filed ticket shows a reader — why, what
/// changes, and what is deliberately left out — read from the spec's yaml map. Moved out of
/// <see cref="PhaseTicketBody"/>, which only decides where each part sits.
/// </summary>
internal static class PhaseSpecProse
{
    /// <summary>
    /// 2026-09-13-b7ba: a decision is a bare line in the oldest phases and a {key: '…'} map
    /// in every modern one — which used to render as an empty string and be filtered away,
    /// so the REASONING was absent from every ticket this product has ever filed.
    /// </summary>
    public static IEnumerable<string> Decisions(IReadOnlyDictionary<string, object?> map) =>
        (OutcomeYamlReader.GetList(map, "decisions") ?? []).Select(Decision);

    /// <summary>What the phase changes: the spec's scope.in, trimmed; empty when it states none.</summary>
    public static string ScopeIn(IReadOnlyDictionary<string, object?> map) => Scope(map, "in");

    /// <summary>What the phase deliberately leaves out: scope.out, trimmed; empty when none.</summary>
    public static string ScopeOut(IReadOnlyDictionary<string, object?> map) => Scope(map, "out");

    private static string Decision(object? decision) => decision switch
    {
        string line => line,
        Dictionary<object, object?> entry =>
            string.Join(" ", entry.Values.Select(v => v?.ToString()?.Trim()).Where(v => !string.IsNullOrEmpty(v))),
        _ => decision?.ToString() ?? string.Empty,
    };

    private static string Scope(IReadOnlyDictionary<string, object?> map, string key) =>
        OutcomeYamlReader.GetMap(map, "scope") is { } scope
            ? OutcomeYamlReader.GetString(scope, key)?.Trim() ?? string.Empty
            : string.Empty;
}
