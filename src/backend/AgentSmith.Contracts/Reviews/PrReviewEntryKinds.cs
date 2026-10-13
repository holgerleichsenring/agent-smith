namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: what an entry of a pull request's record is.</summary>
public static class PrReviewEntryKinds
{
    public const string Comment = "comment";
    public const string Review = "review";

    /// <summary>A decision no note carries — a GitHub approval with an empty body, a threadless vote.</summary>
    public const string Decision = "decision";
}
