using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds a single Coverage-Incomplete observation that a filter handler appends
/// to its result list when one or more batches failed to parse. The observation is
/// pipeline-meta (Concern=Correctness, Severity=Info, Category="meta") so it does
/// not pollute the security-finding counts but is still visibly delivered to
/// reviewers, who can see which batches the filter couldn't review.
/// </summary>
internal static class FilterCoverageObservationFactory
{
    internal static SkillObservation Build(
        string skillName, int failedBatches, int totalBatches, int unfilteredCount) =>
        new(
            Id: 0,
            Role: skillName,
            Concern: ObservationConcern.Correctness,
            Description:
                $"Filter coverage incomplete: {failedBatches} of {totalBatches} batches unparseable; "
                + $"{unfilteredCount} observations not reviewed by {skillName} and may include false positives.",
            Suggestion: "",
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            ReviewStatus: "not_reviewed",
            Category: "meta");
}
