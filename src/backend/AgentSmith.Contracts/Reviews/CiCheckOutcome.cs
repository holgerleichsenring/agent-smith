namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-17-3435a: how one check stands.</summary>
public enum CiCheckOutcome
{
    Pending = 0,
    Passed = 1,
    Failed = 2,

    /// <summary>2026-11-23-0219: a required build that has not started on the head and will not start by itself — expired,
    /// or queued with no build behind it (Azure runs no build-validation policy on a draft).</summary>
    NotStarted = 3,
}
