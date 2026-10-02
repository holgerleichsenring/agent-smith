namespace AgentSmith.Contracts.Services;

/// <summary>2026-10-02-5ab2b: what <see cref="IRunStartClaim"/> decided for one popped request.</summary>
public enum RunStartClaimOutcome
{
    /// <summary>This copy won the row's claim; the consumer beats the row from now on.</summary>
    Claimed,

    /// <summary>The row holds no stored request (a ticket run): it starts as it always did.</summary>
    Unclaimed,

    /// <summary>Another copy holds the claim, or the run already runs: this copy is dropped.</summary>
    Duplicate,
}
