namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: the decision vocabulary the state readers return.</summary>
public static class PrReviewDecisions
{
    public const string Approved = "approved";

    /// <summary>Azure DevOps +5; a ticket reads it as approved.</summary>
    public const string ApprovedWithSuggestions = "approved-with-suggestions";

    public const string ChangesRequested = "changes-requested";

    /// <summary>Azure DevOps -10.</summary>
    public const string Rejected = "rejected";
}
