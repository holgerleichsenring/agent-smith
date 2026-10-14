namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-13-8762g: the run-time spec review, from agentsmith.yml's top-level <c>spec_review:</c> block —
/// how many rounds each unbuilt spec gets, how many fresh reviewers attack it per round and how many of them
/// run at once, and the share of the run's cost cap and wall clock the review may use before the specs it
/// did not clear are left to the next attempt. Global by design: the machinery has no project dimension.
/// </summary>
public sealed class SpecReviewConfig
{
    /// <summary>Rounds per spec; 0 turns the review off.</summary>
    public int Rounds { get; set; } = 4;

    public int Reviewers { get; set; } = 2;

    public int MaxConcurrent { get; set; } = 2;

    /// <summary>The percent of the run's cost cap and wall clock the review may use.</summary>
    public int BudgetSharePercent { get; set; } = 40;

    /// <summary>What is wrong with these values, one sentence each; empty when nothing is. The ONE rule —
    /// a run refuses what this names, never clamping it.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (Rounds < 0) problems.Add($"spec_review.rounds is {Rounds}; it must be 0 or more");
        if (Reviewers < 1) problems.Add($"spec_review.reviewers is {Reviewers}; it must be 1 or more");
        if (MaxConcurrent < 1) problems.Add($"spec_review.max_concurrent is {MaxConcurrent}; it must be 1 or more");
        if (BudgetSharePercent is < 1 or > 100)
            problems.Add($"spec_review.budget_share_percent is {BudgetSharePercent}; it must be between 1 and 100");
        return problems;
    }
}
