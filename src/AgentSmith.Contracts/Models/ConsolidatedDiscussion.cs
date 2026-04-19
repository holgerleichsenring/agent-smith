using AgentSmith.Contracts.Services;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Output of PlanConsolidator for discussion/structured pipelines.
/// Replaces the pattern of constructing a fake Plan from consolidated text.
/// </summary>
public sealed record ConsolidatedDiscussion(
    string Title,
    IReadOnlyList<DiscussionFinding> Findings,
    IReadOnlyList<FindingAssessment> Assessments,
    string? RawSummary);

/// <summary>
/// A single finding/recommendation from a consolidated discussion.
/// Replaces the synthetic PlanStep list created by splitting summary text.
/// </summary>
public sealed record DiscussionFinding(int Order, string Content);
