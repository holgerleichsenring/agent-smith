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
public sealed record MasterVerification(
    VerificationStatus Status,
    bool BuildRan,
    bool BuildPassed,
    bool TestsRan,
    bool TestsPassed,
    string? Summary);

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
