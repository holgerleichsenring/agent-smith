namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-8762e: one reviewer's answer in one review round of a spec — or, with
/// <see cref="Scope"/> <see cref="SpecReviewScopes.Series"/>, of a series, when <see cref="SpecId"/> is null.
/// <see cref="Reviewer"/> is 0-based within the round; <see cref="Verdict"/> is one of
/// <see cref="SpecReviewVerdicts"/> and is only as severe as what the framework admitted. The evidence
/// problems are the ROUND's, found before any reviewer was asked; the residues are the wording a cut left
/// standing in another register.
/// </summary>
public sealed record SpecReviewRoundEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string? SpecId,
    string Scope,
    int Round,
    int Reviewer,
    string Verdict,
    IReadOnlyList<SpecReviewCut> Cuts,
    IReadOnlyList<SpecReviewFinding> Findings,
    IReadOnlyList<SpecEvidenceProblem> EvidenceProblems,
    IReadOnlyList<SpecReviewResidue> Residues,
    int? RevisionNumber = null)
    : RunEvent(RunId, EventType.SpecReviewRound, Timestamp);

/// <summary>
/// 2026-10-13-8762e: one cut a reviewer named — the run of <see cref="Register"/> it changes, what replaces
/// it (null removes it), and the reviewer's own read it rests on, stored as <c>repo/path:line [Aid]</c>.
/// <see cref="SpecId"/> is null on a spec-scope round and names the member on a series round.
/// </summary>
public sealed record SpecReviewCut(
    string Register, string Quote, string? Replacement, string Evidence, string? SpecId = null);

/// <summary>2026-10-13-8762e: one finding, with the reviewer's own look it rests on — <c>repo/path:line [Aid]</c>
/// for a read, <c>repo search:'pattern' [Aid]</c> for a search.</summary>
public sealed record SpecReviewFinding(string Text, string Evidence);

/// <summary>2026-10-13-8762e: a fact whose cited evidence does not resolve, and why.</summary>
public sealed record SpecEvidenceProblem(string Claim, string Problem);

/// <summary>2026-10-13-8762e: wording a cut replaced that still stands in another register.</summary>
public sealed record SpecReviewResidue(string Register, string Quote);

/// <summary>2026-10-13-8762e: the three answers a spec review gives.</summary>
public static class SpecReviewVerdicts
{
    public const string Build = "build";
    public const string BuildWithCuts = "build-with-cuts";
    public const string Refuse = "refuse";
}

/// <summary>2026-10-13-8762e: what one review round is over — one spec, or a series' members together.</summary>
public static class SpecReviewScopes
{
    public const string Spec = "spec";
    public const string Series = "series";
}

/// <summary>2026-10-13-8762e: the seven registers of a spec a cut may quote — the plugin's list.</summary>
public static class SpecRegisterNames
{
    public const string Goal = "goal";
    public const string Scope = "scope";
    public const string Decisions = "decisions";
    public const string Steps = "steps";
    public const string Tests = "tests";
    public const string Done = "done";
    public const string Facts = "facts";

    public static IReadOnlyList<string> All { get; } = [Goal, Scope, Decisions, Steps, Tests, Done, Facts];
}
