namespace AgentSmith.Domain.Models;

/// <summary>
/// p0241: the coding master's self-reported verification verdict, emitted as a
/// structured block at the end of its agentic loop and parsed by the framework.
/// It is the agent's CLAIM about build/test state; the framework pairs it with
/// the unfakeable git-diff truth (a real code change must exist) to decide
/// whether a fix/feature run may be reported as success. The model deciding and
/// running the tests itself is the right division of labour — the framework
/// only enforces that an unverified or red run is never a success.
/// </summary>
/// <param name="FailingTests">
/// p0273: the test ids that FAILED after the change (post-edit). Null = the skill
/// did not report a list (older skill) → the keystone falls back to the binary
/// <paramref name="TestsPassed"/> gate. When present, the keystone gates on
/// REGRESSIONS (FailingTests minus BaselineFailingTests), not on any red.
/// </param>
/// <param name="BaselineFailingTests">
/// p0273: the test ids already FAILING at HEAD before any edit (baseline). The
/// agent reports the two raw lists; the framework computes regressions = final
/// minus baseline, so "pre-existing / unrelated" is a measured fact, never the
/// agent's opinion. Null is treated as an empty baseline.
/// </param>
public sealed record MasterVerification(
    VerificationStatus Status,
    bool BuildRan,
    bool BuildPassed,
    bool TestsRan,
    bool TestsPassed,
    string? Summary,
    IReadOnlyList<string>? FailingTests = null,
    IReadOnlyList<string>? BaselineFailingTests = null,
    IReadOnlyList<IgnoredInstruction>? IgnoredInstructions = null,
    // p0340: the master's per-criterion disposition of the ratified acceptance
    // contract. Null = the master reported none → the keystone treats a run WITH
    // ratified criteria as unconfirmed (FAILED). Ordered to match the criteria.
    IReadOnlyList<AcceptanceDisposition>? AcceptanceDispositions = null);

/// <summary>
/// p0340: the master's disposition of ONE ratified acceptance criterion — either
/// <see cref="AcceptanceStatus.Met"/> (with the edit that satisfies it in
/// <paramref name="Evidence"/>) or <see cref="AcceptanceStatus.NotApplicable"/>
/// (with the EVALUATED meaning of not doing it, e.g. "no MassTransit present →
/// nothing to migrate, no messaging behaviour changes"). RunOutcomeKeystone pairs
/// the ordered dispositions with the ratified criteria: a run is success only when
/// every criterion is met or justified-N/A. An unmet criterion is the honest RED
/// the master must report rather than stopping short and self-declaring green.
/// </summary>
public sealed record AcceptanceDisposition(string Criterion, AcceptanceStatus Status, string Evidence);

/// <summary>p0340: how the master disposed of a ratified acceptance criterion.</summary>
public enum AcceptanceStatus
{
    /// <summary>Unaddressed / not satisfied — the default, and it gates the run RED.</summary>
    Unmet,
    /// <summary>Satisfied by an actual edit, named in Evidence.</summary>
    Met,
    /// <summary>Genuinely not applicable; Evidence carries the evaluated reason.</summary>
    NotApplicable,
}

/// <summary>
/// p0316: a ticket-embedded instruction the master REFUSED to follow — either
/// out-of-scope/destructive (the never-comply catalog) or a prompt-injection
/// attempt ("ignore previous instructions"). The framework persists these as
/// auditable data (run event + result.md section), mirroring the p0273
/// failing_tests pattern: the agent reports verbatim quote + reason, the
/// framework surfaces them. Absent/empty = the master ignored nothing.
/// </summary>
public sealed record IgnoredInstruction(string Quote, string Reason);

public enum VerificationStatus
{
    /// <summary>No verdict could be parsed from the master's final answer.</summary>
    Unknown,
    /// <summary>Build clean and tests (where they exist) pass.</summary>
    Green,
    /// <summary>Build clean; the repo has no automated tests to run.</summary>
    NoTests,
    /// <summary>The master could not reach a clean build / passing tests.</summary>
    Failed,
}
