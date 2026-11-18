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

    /// <summary>2026-11-12-cd4ch: whether a design turn's proposal gets one advisory review pass after the turn.
    /// Off, only the mechanical evidence check runs there (2026-11-18-9c1cb: no run reviews a spec before its master).
    /// 2026-11-12-7c1c: on by default.</summary>
    public bool DesignTurn { get; set; } = true;

    /// <summary>2026-11-17-f9b0b: the fewest looks one attack reviewer gets; a spec citing many paths gets its
    /// distinct cited paths plus 12 instead. Ratified 24 by the operator 2026-11-17.</summary>
    public int AttackLookFloor { get; set; } = 24;

    /// <summary>2026-11-17-f9b0b: the distinct (repository, path) pairs, each candidate read counted as one more, one
    /// entered spec's citations are re-grounded against; a citation past it is left unchecked.</summary>
    public int RegroundReadBudget { get; set; } = 24;

    /// <summary>2026-11-17-f9b0b: the characters of members the series-contract reviewer is shown whole; past it a
    /// member is shown in part.</summary>
    public int SeriesContractMaxChars { get; set; } = 60_000;

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
        if (AttackLookFloor < 1) problems.Add($"spec_review.attack_look_floor is {AttackLookFloor}; it must be 1 or more");
        if (RegroundReadBudget < 1) problems.Add($"spec_review.reground_read_budget is {RegroundReadBudget}; it must be 1 or more");
        if (SeriesContractMaxChars < 1)
            problems.Add($"spec_review.series_contract_max_chars is {SeriesContractMaxChars}; it must be 1 or more");
        return problems;
    }
}
