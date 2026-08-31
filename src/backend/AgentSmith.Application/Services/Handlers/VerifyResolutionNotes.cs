namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-28-5f71: what the stage resolver has to say about a repository it ran no
/// command for — in the two kinds that are answered in different places.
/// <para>
/// A FINDING is a refusal: a declared stage that cannot fail, an entry point that is
/// ambiguous or absent. It fails the run before the phase account is taken, because a
/// repository whose declaration is broken has nothing to be accounted against.
/// </para>
/// <para>
/// A SEARCH is a report: every source was consulted and none of them named a command.
/// On its own that is correct — not every repository in a multi-repo run is buildable —
/// so it is not a finding. It becomes a verdict only when it is true of the WHOLE run,
/// and that verdict has to say what was looked for and where, or the operator is told a
/// delivery is unverified without being told how to make it verifiable.
/// </para>
/// </summary>
public sealed class VerifyResolutionNotes
{
    /// <summary>Refusals. No command ran, and the run stops before the account.</summary>
    public List<string> Findings { get; } = [];

    /// <summary>Reports. Nothing resolved here, and this is everywhere it was looked for.</summary>
    public List<string> Searched { get; } = [];

    public void NothingDeclared(string key, string? primaryLanguage) =>
        Searched.Add(
            $"{Named(key)}: searched the context.yaml verify block (none), the analyzer's "
            + "ci.build_command / ci.test_command (none) and a .NET entry point to discover "
            + $"(primary language '{Language(primaryLanguage)}') — nothing named a command.");

    public void EveryDeclaredStageSkipped(string key, int declared) =>
        Searched.Add(
            $"{Named(key)}: all {declared} stage(s) declared in context.yaml were skipped "
            + "because the when_present path each of them needs is absent from this "
            + "repository — nothing was left to run.");

    private static string Named(string key) => string.IsNullOrEmpty(key) ? "(default)" : key;

    private static string Language(string? primaryLanguage) =>
        string.IsNullOrWhiteSpace(primaryLanguage) ? "unknown" : primaryLanguage;
}
