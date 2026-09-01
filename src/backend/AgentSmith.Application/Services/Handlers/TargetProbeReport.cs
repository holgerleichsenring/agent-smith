namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: the three sentences the record can carry about a target — answered,
/// refused, not declared — plus the skip a backend that injects nothing earns.
/// <para>
/// They live together because they are only meaningful against each other: exactly ONE of
/// them (answered) is registered in <c>CommandStepClasses.NoOpSummaries</c>, so a repository
/// nothing asked about can never read like one whose target replied. That is the whole
/// mechanism; a shared prefix or a single parameterised sentence would dissolve it.
/// </para>
/// </summary>
internal static class TargetProbeReport
{
    /// <summary>The one silent outcome. Its opening words are the registered no-op phrase.</summary>
    public static string Answered(IReadOnlyList<ContextTargetProbe> asked) =>
        $"The target answered: {Names(asked)} ({asked.Count} probe(s), exit 0).";

    public const string NotDeclared =
        "No target probe is declared: nothing asked whether a target environment answers, "
        + "so this run proves nothing about one. Declare probe: in the context.yaml of a "
        + "repository whose work depends on a live target.";

    public static string Skipped(IReadOnlyList<ContextTargetProbe> skipped, int answered) =>
        $"Target probe not asked for {Names(skipped)}: this sandbox backend injects no "
        + "credentials, so a refusal would be a fact about the backend and not about the "
        + "target" + (answered > 0 ? $"; {answered} other probe(s) replied." : ".");

    public static string Refused(string key, ContextTargetProbe probe, int exitCode) =>
        $"The target refused: {probe.Target} ({Named(key)}) — the declared probe "
        + $"'{probe.Command}' exited {exitCode}. No captured output is carried here: it can "
        + "hold an injected credential the masker never sees, so the tail is in the run log "
        + "alone.";

    private static string Names(IReadOnlyList<ContextTargetProbe> probes) =>
        string.Join(", ", probes.Select(probe => probe.Target));

    private static string Named(string key) => string.IsNullOrEmpty(key) ? "(default)" : key;
}
