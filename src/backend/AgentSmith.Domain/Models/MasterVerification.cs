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
    IReadOnlyList<string>? BaselineFailingTests = null);

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
