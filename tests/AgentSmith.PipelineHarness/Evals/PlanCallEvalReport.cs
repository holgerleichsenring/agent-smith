namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0397: one plan-call eval run — the model × output-cap matrix for a single
/// ticket source. Each row records whether the REAL plan prompt (production
/// AgentPromptBuilder + embedded template) came back complete at that
/// MaxOutputTokens cap, whether PlanParser could read it, and whether the
/// resulting steps cover both scoped repositories.
/// </summary>
public sealed record PlanCallEvalReport(
    string TicketSource,
    int SystemPromptChars,
    int UserPromptChars,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PlanCallEvalReport.Row> Rows)
{
    /// <summary>One model × cap combination. <c>ParsedSteps</c> is null when
    /// the tolerant parse failed (then <c>SalvagedSteps</c> counts what
    /// <c>PlanParser.SalvageProse</c> recovered); <c>Error</c> is non-null when
    /// the call itself failed (rate limit, auth, transport) or the budget
    /// fence refused it; <c>CostUsd</c> is the actual spend from returned
    /// usage (p0397a), null for refused/errored calls.</summary>
    public sealed record Row(
        string Model,
        int MaxOutputTokens,
        string FinishReason,
        long? OutputTokens,
        int ResponseChars,
        int? ParsedSteps,
        int? SalvagedSteps,
        bool BothReposCovered,
        IReadOnlyList<string> SampleTargets,
        string? Error,
        decimal? CostUsd = null);
}
